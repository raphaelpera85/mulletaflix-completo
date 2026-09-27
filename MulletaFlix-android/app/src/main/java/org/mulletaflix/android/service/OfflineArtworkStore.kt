package org.mulletaflix.android.service

import java.io.File
import java.io.IOException
import java.io.InputStream
import java.security.MessageDigest

/** Private, durable poster storage for items in the Media3 offline queue. */
internal class OfflineArtworkStore(
    private val directory: File,
    private val maxArtworkBytes: Long = MAX_ARTWORK_BYTES,
) {
    init {
        directory.listFiles()?.filter { it.name.endsWith(".pending") }?.forEach(File::delete)
    }

    fun uriFor(requestId: String): String? = artworkFiles(requestId).firstOrNull { it.isFile }?.toURI()?.toString()

    @Throws(IOException::class)
    fun store(requestId: String, source: InputStream, extension: String?): String {
        if (!directory.exists() && !directory.mkdirs()) throw IOException("Não foi possível criar o cache de capas.")
        val key = keyFor(requestId)
        val temporary = File(directory, "$key.pending")
        val target = File(directory, "$key.${safeExtension(extension)}")

        try {
            source.use { input ->
                temporary.outputStream().buffered().use { output ->
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    var totalBytes = 0L
                    while (true) {
                        val count = input.read(buffer)
                        if (count < 0) break
                        totalBytes += count
                        if (totalBytes > maxArtworkBytes) throw IOException("A capa excede o limite de tamanho.")
                        output.write(buffer, 0, count)
                    }
                    if (totalBytes == 0L) throw IOException("O servidor retornou uma capa vazia.")
                }
            }

            artworkFiles(requestId).filter { it != temporary }.forEach { oldFile ->
                if (!oldFile.delete()) throw IOException("Não foi possível substituir a capa offline.")
            }
            if (!temporary.renameTo(target)) throw IOException("Não foi possível finalizar a capa offline.")
            return target.toURI().toString()
        } catch (failure: Throwable) {
            temporary.delete()
            throw failure
        }
    }

    fun remove(requestId: String) {
        artworkFiles(requestId).forEach(File::delete)
    }

    private fun artworkFiles(requestId: String): List<File> {
        val prefix = "${keyFor(requestId)}."
        return directory.listFiles()?.filter { it.name.startsWith(prefix) }.orEmpty()
    }

    private fun keyFor(requestId: String): String = MessageDigest.getInstance("SHA-256")
        .digest(requestId.toByteArray(Charsets.UTF_8))
        .joinToString(separator = "") { byte -> "%02x".format(byte.toInt() and 0xff) }

    private fun safeExtension(extension: String?): String = when (extension?.lowercase()) {
        "jpg", "jpeg" -> "jpg"
        "png" -> "png"
        "webp" -> "webp"
        "gif" -> "gif"
        "bmp" -> "bmp"
        "avif" -> "avif"
        else -> "img"
    }

    private companion object {
        const val MAX_ARTWORK_BYTES = 12L * 1024 * 1024
    }
}

internal fun artworkExtensionForContentType(contentType: String?): String? = when (contentType?.lowercase()) {
    "image/jpeg", "image/jpg" -> "jpg"
    "image/png" -> "png"
    "image/webp" -> "webp"
    "image/gif" -> "gif"
    "image/bmp" -> "bmp"
    "image/avif" -> "avif"
    else -> null
}
