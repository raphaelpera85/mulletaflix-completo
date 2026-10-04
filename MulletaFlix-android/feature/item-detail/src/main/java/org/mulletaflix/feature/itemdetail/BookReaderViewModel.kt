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
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.mulletaflix.core.api.MulletaFlixApiService
import org.readium.r2.shared.util.http.DefaultHttpClient
import org.readium.r2.shared.util.asset.AssetRetriever
import org.readium.r2.streamer.PublicationOpener
import org.readium.r2.streamer.parser.DefaultPublicationParser
import org.readium.r2.shared.publication.Publication
import retrofit2.HttpException
import javax.inject.Inject

data class BookReaderState(
    val isLoading: Boolean = true,
    val publication: Publication? = null,
    val error: String? = null,
)

@HiltViewModel
class BookReaderViewModel @Inject constructor(
    private val api: MulletaFlixApiService,
    @ApplicationContext private val context: Context,
) : ViewModel() {
    private val _state = MutableStateFlow(BookReaderState())
    val state: StateFlow<BookReaderState> = _state.asStateFlow()
    private var loadJob: Job? = null
    private val bookCacheFiles = BookReaderCacheFiles(File(context.cacheDir, "book-reader"))

    fun load(itemId: String) {
        if (loadJob?.isActive == true) return
        loadJob = viewModelScope.launch {
            _state.value = BookReaderState(isLoading = true)
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

                    val httpClient = DefaultHttpClient()
                    val retriever = AssetRetriever(context.contentResolver, httpClient)
                    val parser = DefaultPublicationParser(
                        context = context,
                        httpClient = httpClient,
                        assetRetriever = retriever,
                        pdfFactory = null,
                    )
                    val asset = retriever.retrieve(target).getOrNull()
                        ?: throw IllegalArgumentException("O arquivo do livro é inválido.")
                    PublicationOpener(parser).open(asset, allowUserInteraction = false)
                        .getOrNull() ?: throw IllegalArgumentException("Não foi possível interpretar este livro.")
                }
                _state.value = BookReaderState(isLoading = false, publication = publication)
            } catch (cancelled: CancellationException) {
                throw cancelled
            } catch (error: Exception) {
                _state.value = BookReaderState(
                    isLoading = false,
                    error = error.message ?: "Não foi possível abrir este livro.",
                )
            }
        }
    }

    override fun onCleared() {
        bookCacheFiles.deleteAfter(loadJob)
        super.onCleared()
    }
}
