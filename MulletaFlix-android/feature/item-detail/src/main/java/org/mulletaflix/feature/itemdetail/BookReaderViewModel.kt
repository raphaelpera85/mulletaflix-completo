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

data class BookReaderState(
    val isLoading: Boolean = true,
    val publication: Publication? = null,
    val initialLocator: Locator? = null,
    val error: String? = null,
)

@HiltViewModel
class BookReaderViewModel @Inject constructor(
    private val api: MulletaFlixApiService,
    private val sessionRepository: SessionRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {
    private val _state = MutableStateFlow(BookReaderState())
    val state: StateFlow<BookReaderState> = _state.asStateFlow()
    private var loadJob: Job? = null
    private val bookCacheFiles = BookReaderCacheFiles(File(context.cacheDir, "book-reader"))
    private val progressStore = BookReaderProgressStore(context)
    private var loadedItemId: String? = null
    private var loadedProgressScope: HomeFeedCacheScope? = null
    private val loadGeneration = BookReaderRequestGeneration()
    private val progressSaveGeneration = BookReaderRequestGeneration()
    private var progressSaveJob: Job? = null
    private val progressSaveMutex = Mutex()

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
                    load(activeItemId)
                }
            }
        }
    }

    fun load(itemId: String) {
        if (loadJob?.isActive == true && loadedItemId == itemId) return
        val generation = loadGeneration.begin()
        loadJob?.cancel()
        loadedItemId = itemId
        loadedProgressScope = null
        loadJob = viewModelScope.launch {
            _state.value = BookReaderState(isLoading = true)
            val progressScope = recoverBookReaderStorageFailure {
                sessionRepository.getHomeFeedCacheScope().first()
            }
            if (!loadGeneration.isCurrent(generation)) return@launch
            loadedProgressScope = progressScope
            val initialLocator = progressScope?.let { progressStore.read(it, itemId) }
            try {
                val publication = withContext(Dispatchers.IO) {
                    val body = try {
                        api.getBookReaderEpub(itemId)
                    } catch (failure: HttpException) {
                        error(bookReaderHttpFailureMessage(failure.code()))
                    }
                    val contentType = body.contentType()?.let { "${it.type}/${it.subtype}" }.orEmpty()
                    if (isClearlyNotEpubContentType(contentType)) {
                        body.close()
                        error("O servidor não enviou um arquivo de livro compatível para leitura.")
                    }

                    val target = bookCacheFiles.create()
                    var copyCompleted = false
                    try {
                        body.byteStream().use { input -> target.outputStream().use(input::copyTo) }
                        currentCoroutineContext().ensureActive()
                        copyCompleted = true
                    } finally {
                        if (!copyCompleted) bookCacheFiles.delete(target)
                    }

                    openBookPublication(context, target)
                }
                val restorableLocator = initialLocator?.takeIf { locator ->
                    publication.readingOrder.any { link -> link.href == locator.href }
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
                _state.value = BookReaderState(
                    isLoading = false,
                    publication = publication,
                    initialLocator = restorableLocator,
                )
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (error: Exception) {
                if (!loadGeneration.isCurrent(generation)) return@launch
                _state.value = BookReaderState(
                    isLoading = false,
                    error = error.message ?: "Não foi possível abrir este livro.",
                )
            }
        }
    }

    fun saveReadingProgression(itemId: String, locator: Locator) {
        val expectedScope = loadedProgressScope ?: return
        if (loadedItemId != itemId || _state.value.publication == null) return
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

    override fun onCleared() {
        bookCacheFiles.deleteAfter(loadJob)
        super.onCleared()
    }
}
