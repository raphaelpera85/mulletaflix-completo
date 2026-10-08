package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import com.github.junrar.Archive
import com.github.junrar.ArchiveOptions
import com.github.junrar.rarfile.FileHeader
import java.io.File
import java.io.FilterInputStream
import java.io.IOException
import java.io.InputStream
import java.io.RandomAccessFile
import java.util.concurrent.CancellationException
import kotlin.coroutines.CoroutineContext
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import org.json.JSONObject
import org.readium.r2.shared.publication.Locator

/** Reads one image at a time from a CBR/RAR file without extracting archive entries to disk. */
internal class CbrBookArchive private constructor(
    private val archive: Archive,
    private val pageHeaders: List<FileHeader>,
) : BookPageSource {
    private var closed = false

    override val pageCount: Int get() = pageHeaders.size

    fun pageName(index: Int): String = pageHeaders[index].fileName

    override fun locatorForPage(index: Int): Locator {
        require(index in pageHeaders.indices)
        val pageName = pageHeaders[index].fileName
        val progression = if (pageCount == 1) 0.0 else index.toDouble() / (pageCount - 1)
        return requireNotNull(
            Locator.fromJSON(
                JSONObject(
                    """{"href":"$PAGE_HREF_PREFIX$index","type":"${pageMediaType(pageName)}","locations":{"position":${index + 1},"totalProgression":$progression}}""",
                ),
            ),
        )
    }

    override fun pageIndexFromLocator(locator: Locator?): Int? {
        if (pageHeaders.isEmpty() || locator == null) return null
        val index = locator.href.toString().removePrefix(PAGE_HREF_PREFIX)
            .takeIf { it != locator.href.toString() }
            ?.toIntOrNull()
        return index?.takeIf { it in pageHeaders.indices }
    }

    @Synchronized
    override fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap {
        check(!closed) { "Comic archive is closed." }
        require(index in pageHeaders.indices) { "Comic page is out of range." }
        require(maxWidth > 0 && maxHeight > 0) { "Comic page target size is invalid." }

        val header = pageHeaders[index]
        ensurePageDecodeNotInterrupted()
        validatePageEntry(header)

        val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        archive.getInputStream(header).use { input ->
            BitmapFactory.decodeStream(BoundedPageInputStream(input, MAX_PAGE_BYTES), null, bounds)
        }
        if (bounds.outWidth !in 1..MAX_SOURCE_DIMENSION || bounds.outHeight !in 1..MAX_SOURCE_DIMENSION) {
            throw IOException("Comic page has invalid or unsupported image dimensions.")
        }
        if (bounds.outWidth.toLong() * bounds.outHeight > MAX_SOURCE_PIXELS) {
            throw IOException("Comic page exceeds the supported pixel limit.")
        }

        val decodeOptions = BitmapFactory.Options().apply {
            inSampleSize = calculateSampleSize(bounds.outWidth, bounds.outHeight, maxWidth, maxHeight)
        }
        return archive.getInputStream(header).use { input ->
            BitmapFactory.decodeStream(BoundedPageInputStream(input, MAX_PAGE_BYTES), null, decodeOptions)
                ?: throw IOException("Comic page could not be decoded.")
        }
    }

    @Synchronized
    override fun close() {
        if (closed) return
        closed = true
        archive.close()
    }

    companion object {
        const val CONTENT_TYPE = "application/vnd.comicbook-rar"
        private const val MAX_ARCHIVE_ENTRIES = 10_000
        private const val MAX_PAGE_COUNT = 5_000
        private const val MAX_PAGE_BYTES = 128L * 1024L * 1024L
        private const val MAX_SOURCE_DIMENSION = 100_000
        private const val MAX_SOURCE_PIXELS = 100_000_000L
        private const val MAX_DECODED_PIXELS = 16_777_216L
        private const val MAX_DICTIONARY_BYTES = 64L * 1024L * 1024L
        private const val PAGE_HREF_PREFIX = "mulletaflix-cbr-page-"
        private val RAR_SIGNATURE_PREFIX = byteArrayOf(0x52, 0x61, 0x72, 0x21, 0x1a, 0x07)
        private val imageExtensions = setOf("jpg", "jpeg", "png", "webp", "gif", "bmp")
        private val naturalSortTokens = Regex("\\d+|\\D+")

        fun supports(contentType: String?): Boolean {
            val mimeType = contentType
                ?.substringBefore(';')
                ?.trim()
                ?.lowercase()
                ?: return false
            return mimeType in setOf(
                CONTENT_TYPE,
                "application/x-cbr",
                "application/x-rar-compressed",
                "application/vnd.rar",
            )
        }

        fun hasRarSignature(file: File): Boolean {
            if (!file.isFile || file.length() < RAR_SIGNATURE_PREFIX.size) return false
            return runCatching {
                RandomAccessFile(file, "r").use { input ->
                    val prefix = ByteArray(RAR_SIGNATURE_PREFIX.size)
                    input.readFully(prefix)
                    prefix.contentEquals(RAR_SIGNATURE_PREFIX)
                }
            }.getOrDefault(false)
        }

        fun hasRarSignaturePrefix(bytes: ByteArray): Boolean =
            bytes.size >= RAR_SIGNATURE_PREFIX.size && bytes.copyOfRange(0, RAR_SIGNATURE_PREFIX.size)
                .contentEquals(RAR_SIGNATURE_PREFIX)

        suspend fun open(file: File): CbrBookArchive {
            return open(file, currentCoroutineContext())
        }

        internal fun open(
            file: File,
            cancellationContext: CoroutineContext,
            beforeEachHeaderCheck: () -> Unit = {},
        ): CbrBookArchive {
            if (!file.isFile || file.length() <= 0L) {
                throw IOException("O arquivo CBR está vazio ou não foi encontrado.")
            }

            val archive = openArchive(file)
            try {
                // Junrar materializes its internal headers when opening the archive. Iterate its
                // cursor instead of creating another full FileHeader list during archive opening.
                val pages = mutableListOf<FileHeader>()
                val uniqueNames = HashSet<String>()
                var entryCount = 0
                while (true) {
                    beforeEachHeaderCheck()
                    cancellationContext.ensureActive()
                    val header = try {
                        archive.nextFileHeader()
                    } catch (cancelled: CancellationException) {
                        throw cancelled
                    } catch (error: Exception) {
                        throw IOException("O arquivo CBR está inválido ou não é compatível.", error)
                    } ?: break
                    entryCount++
                    if (entryCount > MAX_ARCHIVE_ENTRIES) {
                        throw IOException("Comic archive has too many entries.")
                    }
                    if (header.isDirectory || header.fileName.substringAfterLast('.', "").lowercase() !in imageExtensions) {
                        continue
                    }
                    validatePageEntry(header)
                    if (header.isEncrypted || header.isSplitBefore || header.isSplitAfter) {
                        throw IOException("Encrypted or split comic pages are not supported.")
                    }
                    if (!uniqueNames.add(header.fileName)) {
                        throw IOException("Comic archive contains duplicate page names.")
                    }
                    pages += header
                    if (pages.size > MAX_PAGE_COUNT) {
                        throw IOException("Comic archive has too many pages.")
                    }
                }

                if (pages.isEmpty()) throw IOException("O arquivo CBR não contém páginas de imagem compatíveis.")
                cancellationContext.ensureActive()
                val sortedPages = pages.sortedWith { left, right ->
                    comparePageNames(left.fileName, right.fileName)
                }
                cancellationContext.ensureActive()
                return CbrBookArchive(archive, sortedPages)
            } catch (error: Exception) {
                runCatching { archive.close() }
                throw error
            }
        }

        private fun openArchive(file: File): Archive = try {
            Archive(file, ArchiveOptions.builder().maxDictionarySize(MAX_DICTIONARY_BYTES).build())
        } catch (error: Exception) {
            throw IOException("O arquivo CBR está inválido ou não é compatível.", error)
        }

        private fun validatePageEntry(header: FileHeader) {
            val size = header.fullUnpackSize
            if (header.isUnpSizeUnknown || size < 0L || size > MAX_PAGE_BYTES) {
                throw IOException("Comic page exceeds the supported size limit.")
            }
        }

        private fun pageMediaType(pageName: String): String = when (pageName.substringAfterLast('.', "").lowercase()) {
            "jpg", "jpeg" -> "image/jpeg"
            "png" -> "image/png"
            "webp" -> "image/webp"
            "gif" -> "image/gif"
            "bmp" -> "image/bmp"
            else -> "application/octet-stream"
        }

        private fun calculateSampleSize(width: Int, height: Int, maxWidth: Int, maxHeight: Int): Int {
            var sampleSize = 1
            while (width / sampleSize > maxWidth ||
                height / sampleSize > maxHeight ||
                maxOf(1, width / sampleSize).toLong() * maxOf(1, height / sampleSize) > MAX_DECODED_PIXELS
            ) {
                sampleSize *= 2
            }
            return sampleSize
        }

        private fun comparePageNames(left: String, right: String): Int {
            val leftTokens = naturalSortTokens.findAll(left).map { it.value }.toList()
            val rightTokens = naturalSortTokens.findAll(right).map { it.value }.toList()
            for (index in 0 until minOf(leftTokens.size, rightTokens.size)) {
                val leftToken = leftTokens[index]
                val rightToken = rightTokens[index]
                val comparison = if (leftToken.firstOrNull()?.isDigit() == true && rightToken.firstOrNull()?.isDigit() == true) {
                    compareDigitTokens(leftToken, rightToken)
                } else {
                    leftToken.compareTo(rightToken, ignoreCase = true)
                }
                if (comparison != 0) return comparison
            }
            if (leftTokens.size != rightTokens.size) return leftTokens.size.compareTo(rightTokens.size)
            return left.compareTo(right, ignoreCase = true).takeIf { it != 0 } ?: left.compareTo(right)
        }

        private fun compareDigitTokens(left: String, right: String): Int {
            val leftSignificant = left.dropWhile { it == '0' }.ifEmpty { "0" }
            val rightSignificant = right.dropWhile { it == '0' }.ifEmpty { "0" }
            return leftSignificant.length.compareTo(rightSignificant.length).takeIf { it != 0 }
                ?: leftSignificant.compareTo(rightSignificant).takeIf { it != 0 }
                ?: left.length.compareTo(right.length)
        }

        private class BoundedPageInputStream(input: InputStream, private val maxBytes: Long) : FilterInputStream(input) {
            private var bytesRead = 0L

            override fun read(): Int {
                ensurePageDecodeNotInterrupted()
                if (bytesRead >= maxBytes) {
                    val extraByte = super.read()
                    ensurePageDecodeNotInterrupted()
                    if (extraByte >= 0) throw IOException("Comic page exceeds the supported size limit.")
                    return -1
                }
                return super.read().also {
                    ensurePageDecodeNotInterrupted()
                    if (it >= 0) bytesRead++
                }
            }

            override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
                if (length == 0) return 0
                ensurePageDecodeNotInterrupted()
                if (bytesRead >= maxBytes) {
                    val extraByte = super.read()
                    ensurePageDecodeNotInterrupted()
                    if (extraByte >= 0) throw IOException("Comic page exceeds the supported size limit.")
                    return -1
                }
                val allowedLength = minOf(length.toLong(), maxBytes - bytesRead).toInt()
                return super.read(buffer, offset, allowedLength).also {
                    ensurePageDecodeNotInterrupted()
                    if (it > 0) bytesRead += it
                }
            }

            override fun skip(count: Long): Long {
                if (count <= 0L) return 0L
                ensurePageDecodeNotInterrupted()
                if (bytesRead >= maxBytes) {
                    val extraByte = super.read()
                    ensurePageDecodeNotInterrupted()
                    if (extraByte >= 0) throw IOException("Comic page exceeds the supported size limit.")
                    return 0L
                }
                return super.skip(minOf(count, maxBytes - bytesRead)).also {
                    ensurePageDecodeNotInterrupted()
                    bytesRead += it
                }
            }
        }
    }
}
