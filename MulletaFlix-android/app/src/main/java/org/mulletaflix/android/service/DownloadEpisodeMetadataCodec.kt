package org.mulletaflix.android.service

import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.DataInputStream
import java.io.DataOutputStream

private const val EPISODE_METADATA_MAGIC = 0x4D464550 // "MFEP"
private const val EPISODE_METADATA_VERSION = 1

internal fun encodeDownloadEpisodeMetadata(metadata: DownloadEpisodeMetadata?): ByteArray {
    if (metadata == null || metadata.seriesId.isBlank() || metadata.seasonNumber < 0 || metadata.episodeNumber <= 0) {
        return byteArrayOf()
    }
    return ByteArrayOutputStream().use { bytes ->
        DataOutputStream(bytes).use { output ->
            output.writeInt(EPISODE_METADATA_MAGIC)
            output.writeInt(EPISODE_METADATA_VERSION)
            output.writeUTF(metadata.seriesId)
            output.writeInt(metadata.seasonNumber)
            output.writeInt(metadata.episodeNumber)
        }
        bytes.toByteArray()
    }
}

internal fun decodeDownloadEpisodeMetadata(data: ByteArray): DownloadEpisodeMetadata? = runCatching {
    if (data.isEmpty()) return null
    DataInputStream(ByteArrayInputStream(data)).use { input ->
        require(input.readInt() == EPISODE_METADATA_MAGIC)
        require(input.readInt() == EPISODE_METADATA_VERSION)
        val metadata = DownloadEpisodeMetadata(
            seriesId = input.readUTF(),
            seasonNumber = input.readInt(),
            episodeNumber = input.readInt(),
        )
        require(metadata.seriesId.isNotBlank() && metadata.seasonNumber >= 0 && metadata.episodeNumber > 0)
        require(input.available() == 0)
        metadata
    }
}.getOrNull()
