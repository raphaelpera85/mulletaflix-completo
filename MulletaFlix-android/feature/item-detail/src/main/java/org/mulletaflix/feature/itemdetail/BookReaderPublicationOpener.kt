package org.mulletaflix.feature.itemdetail

import android.content.Context
import java.io.File
import org.readium.r2.shared.publication.Publication
import org.readium.r2.shared.util.asset.AssetRetriever
import org.readium.r2.shared.util.http.DefaultHttpClient
import org.readium.r2.streamer.PublicationOpener
import org.readium.r2.streamer.parser.DefaultPublicationParser

internal suspend fun openBookPublication(context: Context, file: File): Publication {
    val httpClient = DefaultHttpClient()
    val assetRetriever = AssetRetriever(context.contentResolver, httpClient)
    val parser = DefaultPublicationParser(
        context = context,
        httpClient = httpClient,
        assetRetriever = assetRetriever,
        pdfFactory = null,
    )
    val asset = assetRetriever.retrieve(file).getOrNull()
        ?: throw IllegalArgumentException("O arquivo do livro é inválido.")

    val publication = try {
        PublicationOpener(parser).open(asset, allowUserInteraction = false).getOrNull()
    } catch (failure: AssertionError) {
        // Readium 3.4 can assert while decoding malformed EPUB XML instead of returning a failed Try.
        throw IllegalArgumentException("Não foi possível interpretar este livro.", failure)
    }
    return publication ?: throw IllegalArgumentException("Não foi possível interpretar este livro.")
}
