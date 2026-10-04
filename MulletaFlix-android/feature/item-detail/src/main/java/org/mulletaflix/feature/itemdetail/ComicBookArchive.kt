package org.mulletaflix.feature.itemdetail

import android.graphics.Bitmap
import android.graphics.BitmapFactory
import java.io.File
import java.io.FilterInputStream
import java.io.IOException
import java.io.InputStream
import java.util.zip.ZipEntry
import java.util.zip.ZipFile
import org.json.JSONObject
import org.readium.r2.shared.publication.Locator

/** Reads one page at a time from a CBZ archive without extracting its entries. */
internal class ComicBookArchive private constructor(
    private val file: File,
    private val pageEntries: List<String>,
) {
    val pageCount: Int get() = pageEntries.size

    fun pageName(index: Int): String = pageEntries[index]

    fun decodePage(index: Int, maxWidth: Int, maxHeight: Int): Bitmap {
        require(index in pageEntries.indices) { "Comic page is out of range." }
        require(maxWidth > 0 && maxHeight > 0) { "Comic page target size is invalid." }

        val entryName = pageEntries[index]
        ZipFile(file).use { archive ->
            val entry = archive.getEntry(entryName)
                ?: throw IOException("Comic page is missing from the archive.")
            validatePageEntry(entry)

            val bounds = BitmapFactory.Options().apply { inJustDecodeBounds = true }
            archive.getInputStream(entry).use { input ->
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
            return archive.getInputStream(entry).use { input ->
                BitmapFactory.decodeStream(BoundedPageInputStream(input, MAX_PAGE_BYTES), null, decodeOptions)
                    ?: throw IOException("Comic page could not be decoded.")
            }
        }
    }

    companion object {
        const val CONTENT_TYPE = "application/x-cbz"
        private const val MAX_ARCHIVE_ENTRIES = 10_000
        private const val MAX_PAGE_COUNT = 5_000
        private const val MAX_PAGE_BYTES = 128L * 1024L * 1024L
        private const val MAX_SOURCE_DIMENSION = 100_000
        private const val MAX_SOURCE_PIXELS = 100_000_000L
        private const val MAX_DECODED_PIXELS = 16_777_216L
        private const val PAGE_HREF_PREFIX = "mulletaflix:cbz:page:"
        private val imageExtensions = setOf("jpg", "jpeg", "png", "webp", "gif", "bmp")
        private val naturalSortTokens = Regex("\\d+|\\D+")

        fun supports(contentType: String?): Boolean = contentType
            ?.substringBefore(';')
            ?.trim()
            ?.lowercase()
            ?.let { it == CONTENT_TYPE || it == "application/vnd.comicbook+zip" } == true

        fun open(file: File): ComicBookArchive {
            if (!file.isFile || file.length() <= 0L) {
                throw IOException("Comic archive is empty or missing.")
            }

            val pages = ZipFile(file).use { archive ->
                if (archive.size() > MAX_ARCHIVE_ENTRIES) {
                    throw IOException("Comic archive has too many entries.")
                }

                val entries = mutableListOf<String>()
                val uniqueNames = HashSet<String>()
                val zipEntries = archive.entries()
                while (zipEntries.hasMoreElements()) {
                    val entry = zipEntries.nextElement()
                    if (entry.isDirectory || entry.name.substringAfterLast('.', "").lowercase() !in imageExtensions) {
                        continue
                    }
                    validatePageEntry(entry)
                    if (!uniqueNames.add(entry.name)) {
                        throw IOException("Comic archive contains duplicate page names.")
                    }
                    entries += entry.name
                    if (entries.size > MAX_PAGE_COUNT) {
                        throw IOException("Comic archive has too many pages.")
                    }
                }

                if (entries.isEmpty()) throw IOException("Comic archive contains no supported pages.")
                entries.sortedWith(::comparePageNames)
            }
            return ComicBookArchive(file, pages)
        }

        fun locatorForPage(index: Int, pageCount: Int): Locator {
            require(pageCount > 0 && index in 0 until pageCount)
            val progression = if (pageCount == 1) 0.0 else index.toDouble() / (pageCount - 1)
            return requireNotNull(
                Locator.fromJSON(
                    JSONObject(
                        """{"href":"$PAGE_HREF_PREFIX$index","locations":{"position":${index + 1},"totalProgression":$progression}}""",
                    ),
                ),
            )
        }

        fun pageIndexFromLocator(locator: Locator?, pageCount: Int): Int? {
            if (pageCount <= 0 || locator == null) return null
            val href = locator.href.toString()
            val index = href.removePrefix(PAGE_HREF_PREFIX)
                .takeIf { it != href }
                ?.toIntOrNull()
            return index?.takeIf { it in 0 until pageCount }
        }

        private fun validatePageEntry(entry: ZipEntry) {
            if (entry.size < 0L || entry.size > MAX_PAGE_BYTES) {
                throw IOException("Comic page exceeds the supported size limit.")
            }
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
                if (bytesRead >= maxBytes) {
                    if (super.read() >= 0) throw IOException("Comic page exceeds the supported size limit.")
                    return -1
                }
                return super.read().also { if (it >= 0) bytesRead++ }
            }

            override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
                if (length == 0) return 0
                if (bytesRead >= maxBytes) {
                    if (super.read() >= 0) throw IOException("Comic page exceeds the supported size limit.")
                    return -1
                }
                val allowedLength = minOf(length.toLong(), maxBytes - bytesRead).toInt()
                return super.read(buffer, offset, allowedLength).also { if (it > 0) bytesRead += it }
            }

            override fun skip(count: Long): Long {
                if (count <= 0L) return 0L
                if (bytesRead >= maxBytes) {
                    if (super.read() >= 0) throw IOException("Comic page exceeds the supported size limit.")
                    return 0L
                }
                return super.skip(minOf(count, maxBytes - bytesRead)).also { bytesRead += it }
            }
        }
    }
}
