package org.mulletaflix.android.service

import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import org.mulletaflix.domain.repository.OfflineSubtitleEntry
import java.io.File
import java.io.IOException
import java.io.InputStream
import java.security.MessageDigest

/** Stores complete subtitle sidecars privately and isolates them by account, server, and download. */
internal class OfflineSubtitleStore(
    private val directory: File,
    private val maxSubtitleBytes: Long = MAX_SUBTITLE_BYTES,
    private val maxDownloadBytes: Long = MAX_DOWNLOAD_SUBTITLE_BYTES,
) {
    init {
        directory.listFiles()?.filter { it.name.endsWith(".pending") }?.forEach(File::delete)
    }

    fun uriFor(serverId: String, userId: String, requestId: String, subtitle: DownloadSubtitleMetadata): String? =
        subtitleFiles(serverId, userId, requestId, subtitle.streamIndex)
            .firstOrNull { it.isFile }
            ?.toURI()
            ?.toString()

    fun entries(
        serverId: String,
        userId: String,
        requestId: String,
        subtitles: List<DownloadSubtitleMetadata>,
    ): List<OfflineSubtitleEntry> = subtitles.mapNotNull { subtitle ->
        uriFor(serverId, userId, requestId, subtitle)?.let { uri ->
            OfflineSubtitleEntry(
                streamIndex = subtitle.streamIndex,
                uri = uri,
                mimeType = subtitle.mimeType,
                language = subtitle.language,
                label = subtitle.label,
                isDefault = subtitle.isDefault,
                isForced = subtitle.isForced,
            )
        }
    }

    @Throws(IOException::class)
    fun store(
        serverId: String,
        userId: String,
        requestId: String,
        subtitle: DownloadSubtitleMetadata,
        source: InputStream,
    ): String {
        require(serverId.isNotBlank() && userId.isNotBlank() && requestId.isNotBlank())
        require(subtitle.streamIndex in 0..100_000 && subtitle.mimeType.lowercase() in SUPPORTED_MIME_TYPES)
        if (!directory.exists() && !directory.mkdirs()) throw IOException("Não foi possível criar o cache de legendas.")

        val files = subtitleFiles(serverId, userId, requestId, subtitle.streamIndex)
        files.firstOrNull { it.isFile }?.let { return it.toURI().toString() }
        val existingScopeBytes = scopeFiles(serverId, userId, requestId)
            .filterNot { old -> files.any { it == old } }
            .sumOf { it.length() }
        val target = File(directory, "${keyFor(serverId, userId, requestId, subtitle.streamIndex)}.${extensionFor(subtitle.mimeType)}")
        val temporary = File(directory, "${target.name}.${System.nanoTime()}.pending")
        try {
            source.use { input ->
                temporary.outputStream().buffered().use { output ->
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    var totalBytes = 0L
                    var prefix = ByteArray(0)
                    while (true) {
                        val count = input.read(buffer)
                        if (count < 0) break
                        totalBytes += count
                        if (totalBytes > maxSubtitleBytes) throw IOException("A legenda excede o limite de tamanho.")
                        if (existingScopeBytes + totalBytes > maxDownloadBytes) {
                            throw IOException("As legendas deste download excedem o limite de armazenamento.")
                        }
                        if (prefix.size < 512) {
                            val take = minOf(count, 512 - prefix.size)
                            prefix += buffer.copyOfRange(0, take)
                        }
                        output.write(buffer, 0, count)
                    }
                    if (totalBytes == 0L) throw IOException("O servidor retornou uma legenda vazia.")
                    if (prefix.any { it == 0.toByte() } || looksLikeHtml(prefix)) {
                        throw IOException("O conteúdo retornado não parece ser uma legenda de texto.")
                    }
                }
            }
            files.filter { it != target && it.exists() }.forEach { old ->
                if (!old.delete()) throw IOException("Não foi possível substituir a legenda offline.")
            }
            if (!temporary.renameTo(target)) throw IOException("Não foi possível finalizar a legenda offline.")
            return target.toURI().toString()
        } catch (failure: Throwable) {
            temporary.delete()
            throw failure
        }
    }

    fun remove(serverId: String, userId: String, requestId: String, subtitles: List<DownloadSubtitleMetadata>) {
        subtitles.forEach { subtitle ->
            subtitleFiles(serverId, userId, requestId, subtitle.streamIndex).forEach(File::delete)
        }
    }

    private fun subtitleFiles(serverId: String, userId: String, requestId: String, streamIndex: Int): List<File> {
        val prefix = "${keyFor(serverId, userId, requestId, streamIndex)}."
        return directory.listFiles()?.filter { it.name.startsWith(prefix) }.orEmpty()
    }

    private fun scopeFiles(serverId: String, userId: String, requestId: String): List<File> {
        val prefix = "${scopeKey(serverId, userId, requestId)}-"
        return directory.listFiles()?.filter { it.name.startsWith(prefix) && !it.name.endsWith(".pending") }.orEmpty()
    }

    private fun keyFor(serverId: String, userId: String, requestId: String, streamIndex: Int): String =
        "${scopeKey(serverId, userId, requestId)}-$streamIndex"

    private fun scopeKey(serverId: String, userId: String, requestId: String): String =
        MessageDigest.getInstance("SHA-256")
            .digest("$serverId\u0000$userId\u0000$requestId".toByteArray(Charsets.UTF_8))
            .joinToString(separator = "") { byte -> "%02x".format(byte.toInt() and 0xff) }

    private fun extensionFor(mimeType: String): String = when (mimeType.lowercase()) {
        "application/x-subrip" -> "srt"
        "text/vtt" -> "vtt"
        "text/x-ssa" -> "ass"
        "application/ttml+xml" -> "ttml"
        else -> error("Unsupported subtitle MIME type")
    }

    private fun looksLikeHtml(prefix: ByteArray): Boolean =
        prefix.toString(Charsets.UTF_8).trimStart().startsWith("<html", ignoreCase = true) ||
            prefix.toString(Charsets.UTF_8).trimStart().startsWith("<!doctype html", ignoreCase = true)

    private companion object {
        const val MAX_SUBTITLE_BYTES = 4L * 1024 * 1024
        const val MAX_DOWNLOAD_SUBTITLE_BYTES = 32L * 1024 * 1024
        val SUPPORTED_MIME_TYPES = setOf(
            "application/x-subrip", "text/vtt", "text/x-ssa", "application/ttml+xml",
        )
    }
}
