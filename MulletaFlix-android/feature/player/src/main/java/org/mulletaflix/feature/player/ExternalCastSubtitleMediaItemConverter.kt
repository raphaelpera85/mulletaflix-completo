package org.mulletaflix.feature.player

import androidx.media3.cast.DefaultMediaItemConverter
import androidx.media3.cast.MediaItemConverter
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.util.UnstableApi
import com.google.android.gms.cast.MediaInfo
import com.google.android.gms.cast.MediaQueueItem
import com.google.android.gms.cast.MediaTrack

/** Adds receiver-compatible sidecars while preserving Media3's regular Cast payload. */
@UnstableApi
internal class ExternalCastSubtitleMediaItemConverter(
    private val delegate: MediaItemConverter = DefaultMediaItemConverter(),
) : MediaItemConverter {
    override fun toMediaItem(mediaQueueItem: MediaQueueItem): MediaItem =
        delegate.toMediaItem(mediaQueueItem)

    override fun toMediaQueueItem(mediaItem: MediaItem): MediaQueueItem {
        val converted = delegate.toMediaQueueItem(mediaItem)
        val baseInfo = converted.media ?: return converted
        val configurations = mediaItem.localConfiguration?.subtitleConfigurations.orEmpty()
        val serverIndicesByUrl = castSubtitleServerIndicesByUrl(
            configurations.mapNotNull { configuration ->
                externalSubtitleServerIndex(configuration.id)?.let { it to configuration }
            }.toMap(),
        )
        val sidecars = configurations.filter { configuration ->
            val serverIndex = externalSubtitleServerIndex(configuration.id)
            serverIndex != null && serverIndicesByUrl[configuration.uri.toString()] == serverIndex
        }.mapNotNull(::toCastTrack)
        if (sidecars.isEmpty()) return converted

        val mediaTracks = (baseInfo.mediaTracks.orEmpty() + sidecars)
        val mediaInfo = MediaInfo.Builder(baseInfo.contentId)
            .setContentType(baseInfo.contentType)
            .apply { baseInfo.contentUrl?.let(::setContentUrl) }
            .setStreamType(baseInfo.streamType)
            .setMetadata(baseInfo.metadata)
            .setCustomData(baseInfo.customData)
            .setMediaTracks(mediaTracks)
            .apply {
                if (baseInfo.streamDuration >= 0L) setStreamDuration(baseInfo.streamDuration)
                baseInfo.textTrackStyle?.let(::setTextTrackStyle)
                baseInfo.entity?.let(::setEntity)
                baseInfo.vmapAdsRequest?.let(::setVmapAdsRequest)
                baseInfo.adBreaks?.takeIf { it.isNotEmpty() }?.let(::setAdBreaks)
                baseInfo.adBreakClips?.takeIf { it.isNotEmpty() }?.let(::setAdBreakClips)
                baseInfo.hlsSegmentFormat?.let(::setHlsSegmentFormat)
                baseInfo.hlsVideoSegmentFormat?.let(::setHlsVideoSegmentFormat)
            }
            .build()

        // Media3's default converter also uses a default MediaQueueItem.Builder(mediaInfo).
        // Keep that queue behavior while replacing only the MediaInfo's track list.
        return MediaQueueItem.Builder(mediaInfo)
            .setAutoplay(converted.autoplay)
            .build()
    }

    private fun toCastTrack(configuration: MediaItem.SubtitleConfiguration): MediaTrack? {
        val contentType = castSubtitleContentType(configuration.mimeType) ?: return null
        val serverIndex = externalSubtitleServerIndex(configuration.id) ?: return null
        val trackId = castSubtitleTrackId(serverIndex) ?: return null
        val url = configuration.uri.toString().takeIf(String::isNotBlank) ?: return null
        val builder = MediaTrack.Builder(trackId, MediaTrack.TYPE_TEXT)
            .setContentId(url)
            .setContentType(contentType)
            .setSubtype(MediaTrack.SUBTYPE_SUBTITLES)
        configuration.language?.takeIf(String::isNotBlank)?.let(builder::setLanguage)
        configuration.label?.takeIf(String::isNotBlank)?.let(builder::setName)
        if (configuration.selectionFlags and C.SELECTION_FLAG_FORCED != 0) {
            builder.setRoles(listOf(MediaTrack.ROLE_FORCED_SUBTITLE))
        } else if (configuration.selectionFlags and C.SELECTION_FLAG_DEFAULT != 0) {
            builder.setRoles(listOf(MediaTrack.ROLE_MAIN))
        }
        return builder.build()
    }
}
