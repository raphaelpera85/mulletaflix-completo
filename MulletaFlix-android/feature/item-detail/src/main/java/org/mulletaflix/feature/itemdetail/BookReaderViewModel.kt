package org.mulletaflix.feature.itemdetail

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
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
    private var bookFile: File? = null

    fun load(itemId: String) {
        if (loadJob?.isActive == true) return
        loadJob = viewModelScope.launch {
            _state.value = BookReaderState(isLoading = true)
            try {
                val publication = withContext(Dispatchers.IO) {
                    val body = try {
                        api.getBookReaderEpub(itemId)
                    } catch (failure: HttpException) {
                        if (failure.code() == 415) {
                            error("Este formato não pode ser lido pelo aplicativo.")
                        } else {
                            error("Não foi possível carregar o livro (HTTP ${failure.code()}).")
                        }
                    }
                    val contentType = body.contentType()?.let { "${it.type}/${it.subtype}" }.orEmpty()
                    if (!contentType.contains("epub", ignoreCase = true)) {
                        body.close()
                        error("Este arquivo não é EPUB. O leitor móvel ainda não oferece este formato.")
                    }

                    val directory = File(context.cacheDir, "book-reader").apply { mkdirs() }
                    directory.listFiles().orEmpty().forEach(File::delete)
                    val target = File(directory, "$itemId.epub")
                    body.byteStream().use { input -> target.outputStream().use(input::copyTo) }
                    bookFile = target

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
            } catch (error: Exception) {
                _state.value = BookReaderState(
                    isLoading = false,
                    error = error.message ?: "Não foi possível abrir este livro.",
                )
            }
        }
    }

    override fun onCleared() {
        bookFile?.delete()
        File(context.cacheDir, "book-reader").takeIf { it.isDirectory }
            ?.listFiles().orEmpty().filter { it.length() == 0L }.forEach(File::delete)
        super.onCleared()
    }
}
