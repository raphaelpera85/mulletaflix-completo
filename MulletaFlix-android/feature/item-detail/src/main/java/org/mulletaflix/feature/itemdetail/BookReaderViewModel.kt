package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.SessionRepository
import org.readium.r2.shared.publication.Locator
import org.readium.r2.shared.publication.Publication
import retrofit2.HttpException
import javax.inject.Inject

internal data class BookReaderState(
    val isLoading: Boolean = true,
    val publication: Publication? = null,
    val pageBook: BookPageSource? = null,
    val initialLocator: Locator? = null,
    val fontSizePercent: Int = BookReaderFontSize.DEFAULT_PERCENT,
    val bookmarks: List<BookReaderBookmark> = emptyList(),
    val bookmarkMessage: String? = null,
    val error: String? = null,
)

private data class LoadedBookContent(
    val publication: Publication? = null,
    val pageBook: BookPageSource? = null,
    val cacheFile: File,
)

@HiltViewModel
class BookReaderViewModel @Inject constructor(
    private val api: MulletaFlixApiService,
    private val sessionRepository: SessionRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {
    private val _state = MutableStateFlow(BookReaderState())
    internal val state: StateFlow<BookReaderState> = _state.asStateFlow()
    private var loadJob: Job? = null
    private val bookCacheFiles = BookReaderCacheFiles(File(context.cacheDir, "book-reader"))
    private val progressStore = BookReaderProgressStore(context)
    private var loadedItemId: String? = null
    private var loadedProgressScope: HomeFeedCacheScope? = null
    private val loadGeneration = BookReaderRequestGeneration()
    private val progressSaveGeneration = BookReaderRequestGeneration()
    private var progressSaveJob: Job? = null
    private val progressSaveMutex = Mutex()
    private val fontSizeSaveGeneration = BookReaderRequestGeneration()
    private var fontSizeSaveJob: Job? = null

    init {
        viewModelScope.launch {
            var hasObservedScope = false
            var observedScope: HomeFeedCacheScope? = null
            sessionRepository.getHomeFeedCacheScope().distinctUntilChanged().collect { scope ->
                val changed = hasObservedScope && observedScope != scope
                hasObservedScope = true
                observedScope = scope
                val activeItemId = loadedItemId
                if (changed && activeItemId != null) {
                    // A load for the previous account/server must never render after a session switch.
                    loadedProgressScope = null
                    loadedItemId = null
                    _state.value = BookReaderState(isLoading = true)
                    progressSaveGeneration.begin()
                    progressSaveJob?.cancel()
                    fontSizeSaveGeneration.begin()
                    fontSizeSaveJob?.cancel()
                    load(activeItemId)
                }
            }
        }
    }

    fun load(itemId: String) {
        if (loadJob?.isActive == true && loadedItemId == itemId) return
        val generation = loadGeneration.begin()
        val previousLoadJob = loadJob
        previousLoadJob?.cancel()
        loadedItemId = itemId
        loadedProgressScope = null
        loadJob = viewModelScope.launch {
            _state.value = BookReaderState(isLoading = true)
            previousLoadJob?.join()
            val progressScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            }
            if (!loadGeneration.isCurrent(generation)) return@launch
            loadedProgressScope = progressScope
            val initialLocator = progressScope?.let { progressStore.read(it, itemId) }
            val bookmarks = progressScope?.let { progressStore.readBookmarks(it, itemId) }.orEmpty()
            val fontSizePercent = progressScope?.let { progressStore.readFontSizePercent(it) }
                ?: BookReaderFontSize.DEFAULT_PERCENT
            try {
                val content = withContext(Dispatchers.IO) {
                    val body = try {
                        api.getBookReaderEpub(itemId)
                    } catch (failure: HttpException) {
                        error(bookReaderHttpFailureMessage(failure.code()))
                    }
                    try {
                        if (body.contentLength() > MAX_BOOK_PAYLOAD_BYTES) {
                            error("Book file exceeds the supported size limit.")
                        }
                        val contentType = body.contentType()?.let { "${it.type}/${it.subtype}" }.orEmpty()
                        if (isClearlyNotSupportedBookContentType(contentType)) {
                            error("O servidor não enviou um arquivo de livro compatível para leitura.")
                        }

                        val target = bookCacheFiles.create()
                        var cacheTarget = target
                        var contentOpened = false
                        try {
                            body.byteStream().use { input ->
                                target.outputStream().use { output -> copyBookReaderPayload(input, output) }
                            }
                            currentCoroutineContext().ensureActive()
                            val payloadFormat = detectBookPayloadFormat(target, contentType)
                            val extension = when (payloadFormat) {
                                BookPayloadFormat.EPUB -> ".epub"
                                BookPayloadFormat.PDF -> ".pdf"
                                BookPayloadFormat.CBZ -> ".cbz"
                            }
                            cacheTarget = bookCacheFiles.withExtension(target, extension)
                            val loadedContent = when {
                                payloadFormat == BookPayloadFormat.CBZ -> LoadedBookContent(
                                    pageBook = ComicBookArchive.open(cacheTarget),
                                    cacheFile = cacheTarget,
                                )
                                payloadFormat == BookPayloadFormat.PDF -> LoadedBookContent(
                                    pageBook = PdfBookDocument.open(cacheTarget),
                                    cacheFile = cacheTarget,
                                )
                                else -> LoadedBookContent(
                                    publication = openBookPublication(context, cacheTarget),
                                    cacheFile = cacheTarget,
                                )
                            }
                            contentOpened = true
                            loadedContent
                        } finally {
                            if (!contentOpened) bookCacheFiles.delete(cacheTarget)
                        }
                    } finally {
                        body.close()
                    }
                }
                val restorableLocator = initialLocator?.takeIf { locator ->
                    if (content.pageBook != null) {
                        content.pageBook.pageIndexFromLocator(locator) != null
                    } else {
                        content.publication?.readingOrder?.any { link -> link.href == locator.href } == true
                    }
                }
                if (!loadGeneration.isCurrent(generation)) return@launch
                val currentScope = recoverBookReaderStorageFailure {
                    sessionRepository.getHomeFeedCacheScope().first()
                }
                if (!loadGeneration.isCurrent(generation)) return@launch
                if (progressScope?.let { progressStore.entryKey(it, itemId) } !=
                    currentScope?.let { progressStore.entryKey(it, itemId) }
                ) {
                    loadedItemId = null
                    load(itemId)
                    return@launch
                }
                bookCacheFiles.deleteAllOwnedExcept(content.cacheFile)
                _state.value = BookReaderState(
                    isLoading = false,
                    publication = content.publication,
                    pageBook = content.pageBook,
                    initialLocator = restorableLocator,
                    fontSizePercent = fontSizePercent,
                    bookmarks = bookmarks,
                )
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (error: Exception) {
                if (!loadGeneration.isCurrent(generation)) return@launch
                bookCacheFiles.deleteAllOwned()
                _state.value = BookReaderState(
                    isLoading = false,
                    error = error.message ?: "Não foi possível abrir este livro.",
                )
            }
        }
    }

    fun saveReadingProgression(itemId: String, locator: Locator) {
        val expectedScope = loadedProgressScope ?: return
        if (loadedItemId != itemId || (_state.value.publication == null && _state.value.pageBook == null)) return
        val generation = progressSaveGeneration.begin()
        progressSaveJob?.cancel()
        progressSaveJob = viewModelScope.launch {
            val currentScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            }
                ?: return@launch
            if (progressStore.entryKey(currentScope, itemId) != progressStore.entryKey(expectedScope, itemId)) {
                return@launch
            }
            progressSaveMutex.withLock {
                if (!progressSaveGeneration.isCurrent(generation) || loadedItemId != itemId) return@withLock
                progressStore.write(expectedScope, itemId, locator)
            }
        }
    }

    fun setFontSizePercent(itemId: String, percent: Int) {
        val expectedScope = loadedProgressScope ?: return
        val currentState = _state.value
        if (loadedItemId != itemId || currentState.publication == null) return

        val normalizedPercent = BookReaderFontSize.normalize(percent)
        if (currentState.fontSizePercent == normalizedPercent) return
        _state.value = currentState.copy(fontSizePercent = normalizedPercent)

        val generation = fontSizeSaveGeneration.begin()
        fontSizeSaveJob?.cancel()
        fontSizeSaveJob = viewModelScope.launch {
            val currentScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (progressStore.entryKey(currentScope, FONT_SIZE_SCOPE_ITEM_ID) !=
                progressStore.entryKey(expectedScope, FONT_SIZE_SCOPE_ITEM_ID)
            ) {
                return@launch
            }
            if (!fontSizeSaveGeneration.isCurrent(generation)) return@launch
            progressStore.writeFontSizePercent(expectedScope, normalizedPercent)
        }
    }

    fun saveBookmark(itemId: String, locator: Locator, label: String) {
        val expectedScope = loadedProgressScope ?: return
        if (loadedItemId != itemId || (_state.value.publication == null && _state.value.pageBook == null)) return
        viewModelScope.launch {
            val currentScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (progressStore.entryKey(currentScope, itemId) != progressStore.entryKey(expectedScope, itemId)) return@launch

            val result = progressStore.addBookmark(expectedScope, itemId, label, locator)
            val latestScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (loadedItemId != itemId ||
                progressStore.entryKey(latestScope, itemId) != progressStore.entryKey(expectedScope, itemId)
            ) return@launch

            val message = when {
                result == null -> "Não foi possível salvar o marcador."
                result.limitReached -> "Limite de ${BookReaderProgressStore.MAX_BOOKMARKS_PER_BOOK} marcadores por livro atingido."
                result.added -> "Marcador salvo."
                else -> "Esta posição já tem um marcador."
            }
            _state.value = _state.value.copy(
                bookmarks = result?.bookmarks ?: _state.value.bookmarks,
                bookmarkMessage = message,
            )
        }
    }

    fun deleteBookmark(itemId: String, bookmarkId: String) {
        val expectedScope = loadedProgressScope ?: return
        if (loadedItemId != itemId) return
        viewModelScope.launch {
            val currentScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (progressStore.entryKey(currentScope, itemId) != progressStore.entryKey(expectedScope, itemId)) return@launch
            val removed = progressStore.removeBookmark(expectedScope, itemId, bookmarkId)
            val latestScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (loadedItemId != itemId ||
                progressStore.entryKey(latestScope, itemId) != progressStore.entryKey(expectedScope, itemId)
            ) return@launch
            val currentState = _state.value
            _state.value = currentState.copy(
                bookmarks = if (removed) progressStore.readBookmarks(expectedScope, itemId).orEmpty() else currentState.bookmarks,
                bookmarkMessage = if (removed) "Marcador removido." else "Não foi possível remover o marcador.",
            )
        }
    }

    fun renameBookmark(itemId: String, bookmarkId: String, label: String) {
        val expectedScope = loadedProgressScope ?: return
        if (loadedItemId != itemId) return
        viewModelScope.launch {
            val currentScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (progressStore.entryKey(currentScope, itemId) != progressStore.entryKey(expectedScope, itemId)) return@launch
            val renamed = progressStore.renameBookmark(expectedScope, itemId, bookmarkId, label)
            val latestScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            } ?: return@launch
            if (loadedItemId != itemId ||
                progressStore.entryKey(latestScope, itemId) != progressStore.entryKey(expectedScope, itemId)
            ) return@launch
            val currentState = _state.value
            _state.value = currentState.copy(
                bookmarks = if (renamed) progressStore.readBookmarks(expectedScope, itemId).orEmpty() else currentState.bookmarks,
                bookmarkMessage = if (renamed) "Marcador renomeado." else "Não foi possível renomear o marcador.",
            )
        }
    }

    fun clearBookmarkMessage() {
        _state.value = _state.value.copy(bookmarkMessage = null)
    }

    fun showBookmarkMessage(message: String) {
        _state.value = _state.value.copy(bookmarkMessage = message)
    }

    fun restartReadingFromBeginning(itemId: String) {
        val scope = loadedProgressScope ?: return
        if (loadedItemId != itemId) return

        val generation = loadGeneration.begin()
        loadJob?.cancel()
        loadedItemId = null
        loadedProgressScope = null
        progressSaveGeneration.begin()
        progressSaveJob?.cancel()
        _state.value = BookReaderState(isLoading = true)
        loadJob = viewModelScope.launch {
            try {
                progressStore.remove(scope, itemId)
                if (!loadGeneration.isCurrent(generation)) return@launch
                // Reload after the durable delete so the reader cannot restore the old locator.
                load(itemId)
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (_: Exception) {
                if (!loadGeneration.isCurrent(generation)) return@launch
                _state.value = BookReaderState(
                    isLoading = false,
                    error = "Não foi possível reiniciar a leitura. Tente novamente.",
                )
            }
        }
    }

    override fun onCleared() {
        fontSizeSaveGeneration.begin()
        fontSizeSaveJob?.cancel()
        bookCacheFiles.deleteAfter(loadJob)
        super.onCleared()
    }

    private companion object {
        const val FONT_SIZE_SCOPE_ITEM_ID = "__reader_font_size_preference__"
    }
}
