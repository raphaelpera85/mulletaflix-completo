package org.mulletaflix.android.service

import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.DataInputStream
import java.io.DataOutputStream

private const val EPISODE_METADATA_MAGIC = 0x4D464550 // "MFEP"
private const val EPISODE_METADATA_VERSION = 2
private const val DOWNLOAD_METADATA_MAGIC = 0x4D46444D // "MFDM"
private const val DOWNLOAD_METADATA_VERSION = 2
private const val MAX_SUBTITLE_STREAMS = 16
private val SUPPORTED_SUBTITLE_MIME_TYPES = setOf(
    "application/x-subrip",
    "text/vtt",
    "text/x-ssa",
    "application/ttml+xml",
)

internal data class DownloadRequestMetadata(
    val episode: DownloadEpisodeMetadata? = null,
    val media: DownloadMediaMetadata? = null,
)

internal fun encodeDownloadEpisodeMetadata(metadata: DownloadEpisodeMetadata?): ByteArray {
    if (metadata == null || !isValidEpisodeMetadata(metadata)) {
        return byteArrayOf()
    }
    return ByteArrayOutputStream().use { bytes ->
        DataOutputStream(bytes).use { output ->
            output.writeInt(EPISODE_METADATA_MAGIC)
            output.writeInt(EPISODE_METADATA_VERSION)
            output.writeEpisodeMetadata(metadata)
        }
        bytes.toByteArray()
    }
}

internal fun decodeDownloadEpisodeMetadata(data: ByteArray): DownloadEpisodeMetadata? = runCatching {
    decodeDownloadRequestMetadata(data)?.episode
}.getOrNull()

internal fun encodeDownloadRequestMetadata(
    episode: DownloadEpisodeMetadata?,
    media: DownloadMediaMetadata?,
): ByteArray {
    val supportedMedia = media?.takeIf { validDownloadMediaMetadata(it) }
    if (supportedMedia == null) return encodeDownloadEpisodeMetadata(episode)
    return ByteArrayOutputStream().use { bytes ->
        DataOutputStream(bytes).use { output ->
            output.writeInt(DOWNLOAD_METADATA_MAGIC)
            output.writeInt(DOWNLOAD_METADATA_VERSION)
            output.writeBoolean(episode?.let(::isValidEpisodeMetadata) == true)
            episode?.takeIf(::isValidEpisodeMetadata)?.let {
                output.writeEpisodeMetadata(it)
            }
            output.writeUTF(supportedMedia.serverId)
            output.writeBoolean(!supportedMedia.mediaSourceId.isNullOrBlank())
            supportedMedia.mediaSourceId?.takeIf(String::isNotBlank)?.let(output::writeUTF)
            output.writeInt(supportedMedia.subtitles.size)
            supportedMedia.subtitles.forEach { subtitle ->
                output.writeInt(subtitle.streamIndex)
                output.writeUTF(subtitle.mimeType)
                output.writeNullableUtf(subtitle.language)
                output.writeNullableUtf(subtitle.label?.takeUnless(::looksSensitiveSubtitleLabel))
                output.writeBoolean(subtitle.isDefault)
                output.writeBoolean(subtitle.isForced)
            }
        }
        bytes.toByteArray()
    }
}

internal fun decodeDownloadRequestMetadata(data: ByteArray): DownloadRequestMetadata? = runCatching {
    if (data.isEmpty()) return DownloadRequestMetadata()
    DataInputStream(ByteArrayInputStream(data)).use { input ->
        val magic = input.readInt()
        if (magic == EPISODE_METADATA_MAGIC) {
            val version = input.readInt().also { require(it in 1..EPISODE_METADATA_VERSION) }
            val episode = input.readEpisodeMetadata(includeSeriesName = version >= 2)
            require(input.available() == 0)
            return DownloadRequestMetadata(episode = episode)
        }
        require(magic == DOWNLOAD_METADATA_MAGIC)
        val version = input.readInt().also { require(it in 1..DOWNLOAD_METADATA_VERSION) }
        val episode = if (input.readBoolean()) input.readEpisodeMetadata(includeSeriesName = version >= 2) else null
        val serverId = input.readUTF().also { require(it.isNotBlank()) }
        val mediaSourceId = if (input.readBoolean()) input.readUTF() else null
        val count = input.readInt().also { require(it in 0..MAX_SUBTITLE_STREAMS) }
        val subtitles = List(count) {
            DownloadSubtitleMetadata(
                streamIndex = input.readInt(),
                mimeType = input.readUTF(),
                language = input.readNullableUtf(),
                label = input.readNullableUtf(),
                isDefault = input.readBoolean(),
                isForced = input.readBoolean(),
            )
        }
        val media = DownloadMediaMetadata(serverId, mediaSourceId, subtitles)
        require(validDownloadMediaMetadata(media))
        require(episode == null || isValidEpisodeMetadata(episode))
        require(input.available() == 0)
        DownloadRequestMetadata(episode, media)
    }
}.getOrNull()

private fun DataOutputStream.writeEpisodeMetadata(metadata: DownloadEpisodeMetadata) {
    writeUTF(metadata.seriesId)
    writeInt(metadata.seasonNumber)
    writeInt(metadata.episodeNumber)
    val seriesName = metadata.seriesName?.takeIf(String::isNotBlank)
    writeBoolean(seriesName != null)
    seriesName?.let(::writeUTF)
}

private fun DataInputStream.readEpisodeMetadata(includeSeriesName: Boolean): DownloadEpisodeMetadata {
    val seriesId = readUTF()
    val seasonNumber = readInt()
    val episodeNumber = readInt()
    val seriesName = if (includeSeriesName && readBoolean()) readUTF() else null
    return DownloadEpisodeMetadata(seriesId, seasonNumber, episodeNumber, seriesName)
        .also { require(isValidEpisodeMetadata(it)) }
}

private fun DataOutputStream.writeNullableUtf(value: String?) {
    writeBoolean(!value.isNullOrBlank())
    value?.takeIf(String::isNotBlank)?.let(::writeUTF)
}

private fun DataInputStream.readNullableUtf(): String? = if (readBoolean()) readUTF() else null

private fun isValidEpisodeMetadata(metadata: DownloadEpisodeMetadata): Boolean =
    metadata.seriesId.isNotBlank() && metadata.seasonNumber >= 0 && metadata.episodeNumber > 0 &&
        metadata.seriesName.orEmpty().length <= 512

private fun validDownloadMediaMetadata(metadata: DownloadMediaMetadata): Boolean =
    metadata.serverId.isNotBlank() && metadata.serverId.length <= 512 &&
        metadata.mediaSourceId.orEmpty().length <= 512 && metadata.subtitles.size in 0..MAX_SUBTITLE_STREAMS &&
        metadata.subtitles.all { subtitle ->
            subtitle.streamIndex in 0..100_000 &&
                subtitle.mimeType.lowercase() in SUPPORTED_SUBTITLE_MIME_TYPES &&
                subtitle.language.orEmpty().length <= 128 && subtitle.label.orEmpty().length <= 512
        }

private fun looksSensitiveSubtitleLabel(label: String): Boolean =
    label.contains("://") || Regex("(?i)(api[_-]?key|access[_-]?token|password|authorization|secret)=")
        .containsMatchIn(label)
