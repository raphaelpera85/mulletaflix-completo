package org.mulletaflix.feature.itemdetail

import android.util.Base64
import java.io.IOException

/** Resolves local KF8 kindle:embed references from bounded PDB image-resource records. */
internal object Kf8ImageResources {
    private const val MAX_RESOURCE_RECORD_BYTES = 1_048_576
    private const val MAX_REFERENCED_IMAGES = 128
    private const val MAX_TOTAL_IMAGE_BYTES = 4 * 1024 * 1024
    private const val MAX_TOTAL_EXPANDED_IMAGE_CHARS = 8 * 1024 * 1024
    private val imageReference = Regex("kindle:embed:([0-9A-V]+)(?:\\?mime=image/[^\\\"'\\s)]+)?", RegexOption.IGNORE_CASE)

    fun inlineReferences(
        html: String,
        firstImageRecord: Int,
        recordCount: Int,
        readRecord: (Int) -> ByteArray,
        precedingResourceRanges: List<IntRange> = emptyList(),
        maxTotalImageBytes: Int = MAX_TOTAL_IMAGE_BYTES,
        maxTotalExpandedImageChars: Int = MAX_TOTAL_EXPANDED_IMAGE_CHARS,
    ): String {
        if (firstImageRecord !in 0 until recordCount) return html
        val resolved = HashMap<Int, String?>()
        var references = 0
        var expandedBytes = 0
        var expandedImageChars = 0
        val precedingResourceCount = precedingResourceRanges.sumOf { range ->
            if (range.isEmpty()) 0 else range.last - range.first + 1
        }
        return imageReference.replace(html) { match ->
            if (++references > MAX_REFERENCED_IMAGES) return@replace ""
            val resourceNumber = match.groupValues[1].toIntOrNull(32)
                ?.takeIf { it in 1..MAX_REFERENCED_IMAGES }
                ?: return@replace ""
            val mappedRecord = if (resourceNumber <= precedingResourceCount) {
                var remaining = resourceNumber
                precedingResourceRanges.firstNotNullOfOrNull { range ->
                    val count = if (range.isEmpty()) 0 else range.last - range.first + 1
                    if (remaining > count) {
                        remaining -= count
                        null
                    } else {
                        range.first to (range.first + remaining - 1)
                    }
                }
            } else {
                firstImageRecord to (firstImageRecord + resourceNumber - precedingResourceCount - 1)
            } ?: return@replace ""
            val (resourceStart, recordIndex) = mappedRecord
            if (recordIndex !in 0 until recordCount) return@replace ""
            val dataUri = resolved.getOrPut(resourceNumber) {
                val image = readImageResource(resourceStart, recordIndex, readRecord) ?: return@getOrPut null
                if (image.bytes.size > maxTotalImageBytes - expandedBytes) return@getOrPut null
                expandedBytes += image.bytes.size
                "data:${image.mimeType};base64," + Base64.encodeToString(image.bytes, Base64.NO_WRAP)
            }
            val uri = dataUri.orEmpty()
            if (uri.length > maxTotalExpandedImageChars - expandedImageChars) return@replace ""
            expandedImageChars += uri.length
            uri
        }
    }

    private fun readImageResource(
        firstImageRecord: Int,
        targetRecord: Int,
        readRecord: (Int) -> ByteArray,
    ): EmbeddedBookImageDecoder.ImageData? {
        var imageContainer = false
        var resource: ByteArray? = null
        for (recordIndex in firstImageRecord..targetRecord) {
            val record = readRecord(recordIndex)
            if (record.isEmpty() || record.size > MAX_RESOURCE_RECORD_BYTES) {
                throw IOException("Um recurso de imagem KF8 excede o limite de leitura.")
            }
            when {
                record.startsWithAscii("CONT") -> imageContainer = isImageContainer(record)
                record.startsWithAscii("CRES") -> {
                    resource = if (imageContainer && record.size > 12) record.copyOfRange(12, record.size) else null
                }
                record.size == 4 && record.contentEquals(byteArrayOf(0xA0.toByte(), 0xA0.toByte(), 0xA0.toByte(), 0xA0.toByte())) ->
                    resource = null
                imageContainer -> resource = null
                else -> resource = record
            }
            if (recordIndex == targetRecord) break
        }
        val bytes = resource ?: return null
        val mimeType = when {
            bytes.startsWithBytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A) -> "image/png"
            bytes.startsWithBytes(0xFF, 0xD8, 0xFF) -> "image/jpeg"
            bytes.startsWithAscii("GIF87a") || bytes.startsWithAscii("GIF89a") -> "image/gif"
            bytes.size >= 12 && bytes.startsWithAscii("RIFF", 0) && bytes.startsWithAscii("WEBP", 8) -> "image/webp"
            else -> return null
        }
        if (!EmbeddedBookImageDecoder.isSafeRaster(bytes)) return null
        return bytes.takeIf { it.size <= EmbeddedBookImageDecoder.DEFAULT_MAX_BYTES }
            ?.let { EmbeddedBookImageDecoder.ImageData(mimeType, "", it) }
    }

    fun firstImageRecord(
        headerImageIndex: Long,
        headerRecordOffset: Int,
        firstTextRecordIndex: Int,
        textRecordCount: Int,
        totalRecordCount: Int,
    ): Int {
        val fallback = firstTextRecordIndex.toLong() + textRecordCount
        val adjusted = headerImageIndex + headerRecordOffset
        return if (headerImageIndex != 0xFFFFFFFFL && adjusted >= fallback && adjusted < totalRecordCount) {
            adjusted.toInt()
        } else {
            fallback.toInt()
        }
    }

    fun precedingResourceRanges(
        legacyFirstImageIndex: Long,
        legacyTextRecordCount: Int,
        boundaryRecordIndex: Int,
        totalRecordCount: Int,
    ): List<IntRange> {
        val start = if (legacyFirstImageIndex == 0xFFFFFFFFL) 1L + legacyTextRecordCount else legacyFirstImageIndex
        return if (start in 0 until boundaryRecordIndex.toLong() && boundaryRecordIndex <= totalRecordCount) {
            listOf(start.toInt() until boundaryRecordIndex)
        } else {
            emptyList()
        }
    }

    private fun isImageContainer(record: ByteArray): Boolean {
        if (record.size < 60 || !record.startsWithAscii("EXTH", 48)) return false
        val headerLength = record.readUnsignedInt(52) ?: return false
        val count = record.readUnsignedInt(56) ?: return false
        if (headerLength !in 8..record.size.toLong() - 52 || count > 4096) return false
        var cursor = 60
        val end = 52 + headerLength.toInt()
        repeat(count.toInt()) {
            if (cursor > end - 8) return false
            val type = record.readUnsignedInt(cursor) ?: return false
            val size = record.readUnsignedInt(cursor + 4) ?: return false
            if (size < 8 || size > end - cursor) return false
            val dataStart = cursor + 8
            val dataEnd = cursor + size.toInt()
            if (type == 539L) {
                return record.copyOfRange(dataStart, dataEnd).contentEquals("application/image".toByteArray())
            }
            cursor = dataEnd
        }
        return false
    }

    private fun ByteArray.readUnsignedInt(offset: Int): Long? {
        if (offset < 0 || offset > size - 4) return null
        return ((this[offset].toLong() and 0xFF) shl 24) or
            ((this[offset + 1].toLong() and 0xFF) shl 16) or
            ((this[offset + 2].toLong() and 0xFF) shl 8) or
            (this[offset + 3].toLong() and 0xFF)
    }

    private fun ByteArray.startsWithAscii(value: String, offset: Int = 0): Boolean =
        offset >= 0 && offset <= size - value.length && value.indices.all { this[offset + it].toInt().toChar() == value[it] }

    private fun ByteArray.startsWithBytes(vararg values: Int): Boolean =
        size >= values.size && values.indices.all { this[it].toInt() and 0xFF == values[it] }
}
