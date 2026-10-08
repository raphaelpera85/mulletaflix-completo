package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import android.graphics.pdf.PdfRenderer
import android.os.ParcelFileDescriptor
import java.io.File
import java.io.IOException
import kotlin.math.min
import org.json.JSONObject
import org.readium.r2.shared.publication.Locator

/** Renders one PDF page at a time and closes each renderer promptly to bound native memory. */
internal class PdfBookDocument private constructor(
    private val file: File,
    override val pageCount: Int,
) : BookPageSource {
    override fun locatorForPage(index: Int): Locator {
        require(index in 0 until pageCount)
        val progression = if (pageCount == 1) 0.0 else index.toDouble() / (pageCount - 1)
        return requireNotNull(
            Locator.fromJSON(
                JSONObject(
                    """{"href":"$PAGE_HREF_PREFIX$index","type":"application/pdf","locations":{"position":${index + 1},"totalProgression":$progression}}""",
                ),
            ),
        )
    }

    override fun pageIndexFromLocator(locator: Locator?): Int? {
        if (locator == null) return null
        val pageIndex = locator.href.toString().removePrefix(PAGE_HREF_PREFIX)
            .takeIf { locator.href.toString().startsWith(PAGE_HREF_PREFIX) }
            ?.toIntOrNull()
        return pageIndex?.takeIf { it in 0 until pageCount }
    }

    override fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap {
        require(index in 0 until pageCount) { "PDF page is out of range." }
        require(maxWidth > 0 && maxHeight > 0) { "PDF page target size is invalid." }

        return openRenderer(file).use { renderer ->
            renderer.openPage(index).use { page ->
                val scale = min(maxWidth.toDouble() / page.width, maxHeight.toDouble() / page.height)
                val width = (page.width * scale).toInt().coerceIn(1, maxWidth)
                val height = (page.height * scale).toInt().coerceIn(1, maxHeight)
                if (width.toLong() * height > MAX_DECODED_PIXELS) {
                    throw IOException("PDF page exceeds the supported pixel limit.")
                }
                Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888).also { bitmap ->
                    try {
                        page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
                    } catch (failure: Exception) {
                        bitmap.recycle()
                        throw failure
                    }
                }
            }
        }
    }

    companion object {
        const val CONTENT_TYPE = "application/pdf"
        private const val PAGE_HREF_PREFIX = "mulletaflix-pdf-page-"
        private const val MAX_DECODED_PIXELS = 16_777_216L

        fun supports(contentType: String?): Boolean = contentType
            ?.substringBefore(';')
            ?.trim()
            ?.equals(CONTENT_TYPE, ignoreCase = true) == true

        fun open(file: File): PdfBookDocument {
            if (!file.isFile || file.length() <= 0L) throw IOException("PDF está vazio ou ausente.")
            val pageCount = openRenderer(file).use(PdfRenderer::getPageCount)
            if (pageCount <= 0) throw IOException("PDF não contém páginas para leitura.")
            return PdfBookDocument(file, pageCount)
        }

        private fun openRenderer(file: File): PdfRenderer =
            ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY).use { descriptor ->
                // PdfRenderer owns the descriptor until close, so duplicate before the `use` closes it.
                PdfRenderer(ParcelFileDescriptor.dup(descriptor.fileDescriptor))
            }
    }
}
