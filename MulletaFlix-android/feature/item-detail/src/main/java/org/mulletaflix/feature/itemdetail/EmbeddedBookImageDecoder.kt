package org.mulletaflix.feature.itemdetail

import android.graphics.BitmapFactory

/** Decodes only small, self-contained raster image data URIs; never follows a network or file URL. */
internal object EmbeddedBookImageDecoder {
    const val DEFAULT_MAX_BYTES = 512 * 1024
    private const val MAX_DESCRIPTION_LENGTH = 256
    private const val MAX_IMAGE_DIMENSION = 8_192
    private const val MAX_IMAGE_PIXELS = 40_000_000L
    private val allowedMimeTypes = setOf("image/png", "image/jpeg", "image/gif", "image/webp")
    private val base64Pattern = Regex("[A-Za-z0-9+/]*={0,2}")

    data class ImageData(
        val mimeType: String,
        val description: String,
        val bytes: ByteArray,
    )

    fun decodeDataUri(source: String, description: String, maxBytes: Int = DEFAULT_MAX_BYTES): ImageData? {
        if (maxBytes <= 0 || !source.startsWith("data:", ignoreCase = true)) return null
        val comma = source.indexOf(',')
        if (comma < 0 || comma >= source.lastIndex) return null
        val metadata = source.substring(5, comma).split(';')
        val mimeType = metadata.firstOrNull()?.lowercase()?.takeIf(allowedMimeTypes::contains) ?: return null
        if (metadata.drop(1).none { it.equals("base64", ignoreCase = true) }) return null
        val encoded = source.substring(comma + 1)
        val maxEncodedBytes = ((maxBytes + 2) / 3) * 4
        if (encoded.length > maxEncodedBytes || encoded.length % 4 != 0 || !base64Pattern.matches(encoded)) return null
        val bytes = runCatching { android.util.Base64.decode(encoded, android.util.Base64.NO_WRAP) }
            .getOrNull()
            ?.takeIf { it.isNotEmpty() && it.size <= maxBytes }
            ?: return null
        if (!isSafeRaster(bytes)) return null
        return ImageData(mimeType, description.take(MAX_DESCRIPTION_LENGTH), bytes)
    }

    fun isSafeRaster(bytes: ByteArray): Boolean {
        val options = BitmapFactory.Options().apply { inJustDecodeBounds = true }
        if (runCatching { BitmapFactory.decodeByteArray(bytes, 0, bytes.size, options) }.isFailure) return false
        val width = options.outWidth
        val height = options.outHeight
        return width in 1..MAX_IMAGE_DIMENSION && height in 1..MAX_IMAGE_DIMENSION &&
            width.toLong() * height <= MAX_IMAGE_PIXELS
    }
}
