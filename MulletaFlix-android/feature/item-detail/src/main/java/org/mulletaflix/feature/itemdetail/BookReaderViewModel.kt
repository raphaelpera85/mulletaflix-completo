package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import javax.inject.Inject
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.mulletaflix.core.api.MulletaFlixApiService
import org.readium.r2.shared.publication.Locator
import org.readium.r2.shared.publication.Publication
import org.readium.r2.shared.util.asset.AssetRetriever
import org.readium.r2.shared.util.format.Specification
import org.readium.r2.shared.util.http.DefaultHttpClient
import org.readium.r2.streamer.PublicationOpener
import org.readium.r2.streamer.parser.DefaultPublicationParser

internal data class BookReaderUiState(
    val isLoading: Boolean = false,
    val publication: Publication? = null,
    val errorMessage: String? = null,
    val lastLocation: Locator? = null,
)

internal const val EPUB_CONTENT_TYPE = "application/epub+zip"
internal const val BOOK_READER_CACHE_DIR = "book-reader"

internal fun isEpubContentType(contentType: String?): Boolean =
    contentType?.substringBefore(';')?.trim()?.lowercase() == EPUB_CONTENT_TYPE

internal fun bookReaderHttpErrorMessage(code: Int): String = when (code) {
    401, 403 -> "Sua sessão não tem autorização para abrir este livro. Entre novamente e tente outra vez."
    404 -> "Este livro não foi encontrado no servidor."
    415 -> "Este formato de livro ainda não pode ser aberto no leitor."
    else -> "Não foi possível baixar o livro (erro $code). Tente novamente."
}

@HiltViewModel
internal class BookReaderViewModel @Inject constructor(
    @ApplicationContext private val context: Context,
    private val api: MulletaFlixApiService,
) : ViewModel() {

    private val httpClient = DefaultHttpClient()
    private val assetRetriever = AssetRetriever(context.contentResolver, httpClient)
    private val publicationOpener = PublicationOpener(
        publicationParser = DefaultPublicationParser(
            context = context,
            httpClient = httpClient,
            assetRetriever = assetRetriever,
            pdfFactory = null,
        ),
    )

    private val _state = MutableStateFlow(BookReaderUiState())
    val state: StateFlow<BookReaderUiState> = _state.asStateFlow()

    private var loadJob: Job? = null
    private var loadedItemId: String? = null
    private var currentPublication: Publication? = null
    private var currentBookFile: File? = null
    private var currentLocation: Locator? = null
    private val bookCacheDir = File(context.cacheDir, BOOK_READER_CACHE_DIR).apply {
        mkdirs()
        listFiles().orEmpty().forEach { staleFile ->
            if (staleFile.isFile && staleFile.name.endsWith(".epub", ignoreCase = true)) {
                staleFile.delete()
            }
        }
    }

    fun load(itemId: String, force: Boolean = false) {
        if (!force && loadedItemId == itemId && currentPublication != null) return

        val previousJob = loadJob
        previousJob?.cancel()
        if (loadedItemId != itemId) {
            currentLocation = null
        }
        loadedItemId = itemId
        loadJob = viewModelScope.launch {
            // Retry/new navigation must not overlap the cleanup of the previous transfer.
            previousJob?.join()
            closeCurrentBook()
            _state.value = BookReaderUiState(isLoading = true, lastLocation = currentLocation)

            try {
                val publication = downloadAndOpen(itemId)
                currentPublication = publication
                _state.value = BookReaderUiState(
                    publication = publication,
                    lastLocation = currentLocation,
                )
            } catch (cancelled: CancellationException) {
                closeCurrentBook()
                throw cancelled
            } catch (error: Throwable) {
                closeCurrentBook()
                // Closing a blocked OkHttp body can surface as IOException before the
                // cancelled coroutine throws CancellationException. Do not publish that
                // transport exception as a stale reader error during retry/navigation.
                currentCoroutineContext().ensureActive()
                _state.value = BookReaderUiState(
                    errorMessage = error.message ?: "Não foi possível abrir este livro.",
                    lastLocation = currentLocation,
                )
            }
        }
    }

    fun retry(itemId: String) = load(itemId, force = true)

    fun updateLocation(locator: Locator) {
        currentLocation = locator
        _state.value = _state.value.copy(lastLocation = locator)
    }

    private suspend fun downloadAndOpen(itemId: String): Publication {
        val response = api.getBookEpub(itemId)
        if (!response.isSuccessful) {
            response.errorBody()?.close()
            throw IllegalStateException(bookReaderHttpErrorMessage(response.code()))
        }

        val body = response.body()
            ?: throw IllegalStateException("O servidor retornou um livro vazio.")

        body.use { responseBody ->
            val contentType = responseBody.contentType()?.toString()
            if (!isEpubContentType(contentType)) {
                throw IllegalStateException("O servidor retornou um formato incompatível com EPUB.")
            }

            val target = File.createTempFile("mulletaflix-book-", ".epub", bookCacheDir)
            currentBookFile = target

            withContext(Dispatchers.IO) {
                coroutineScope {
                    // InputStream.read() is blocking and does not observe coroutine cancellation
                    // on its own. Keep a suspended sibling ready to close the OkHttp body as soon
                    // as this scope is cancelled; closing the source unblocks a stalled socket read.
                    val closeOnCancellation = launch(start = CoroutineStart.UNDISPATCHED) {
                        try {
                            awaitCancellation()
                        } finally {
                            responseBody.close()
                        }
                    }
                    try {
                        responseBody.byteStream().use { input ->
                            target.outputStream().buffered().use { output ->
                                val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                                while (true) {
                                    currentCoroutineContext().ensureActive()
                                    val read = input.read(buffer)
                                    if (read < 0) break
                                    if (read > 0) output.write(buffer, 0, read)
                                }
                            }
                        }
                    } finally {
                        closeOnCancellation.cancel()
                    }
                }
            }

            if (target.length() == 0L) {
                throw IllegalStateException("O servidor retornou um livro vazio.")
            }

            val asset = assetRetriever.retrieve(target).getOrNull()
                ?: throw IllegalStateException("O arquivo recebido não pôde ser reconhecido como EPUB.")

            if (!asset.format.conformsTo(Specification.Epub)) {
                asset.close()
                throw IllegalStateException("O arquivo recebido não é um EPUB válido.")
            }

            return publicationOpener.open(
                asset = asset,
                allowUserInteraction = false,
            ).getOrNull() ?: run {
                asset.close()
                throw IllegalStateException("Não foi possível interpretar o conteúdo deste EPUB.")
            }
        }
    }

    private fun closeCurrentBook() {
        currentPublication?.close()
        currentPublication = null
        currentBookFile?.delete()
        currentBookFile = null
    }

    override fun onCleared() {
        loadJob?.cancel()
        closeCurrentBook()
        super.onCleared()
    }
}
