package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

@JsonClass(generateAdapter = true)
data class PlaybackInfoRequestDto(
    @Json(name = "UserId") val userId: String? = null,
    @Json(name = "MaxStreamingBitrate") val maxStreamingBitrate: Long? = null,
    @Json(name = "StartTimeTicks") val startTimeTicks: Long? = null,
    @Json(name = "AudioStreamIndex") val audioStreamIndex: Int? = null,
    @Json(name = "SubtitleStreamIndex") val subtitleStreamIndex: Int? = null,
    @Json(name = "MediaSourceId") val mediaSourceId: String? = null,
    @Json(name = "EnableDirectPlay") val enableDirectPlay: Boolean = true,
    @Json(name = "EnableDirectStream") val enableDirectStream: Boolean = true,
    @Json(name = "EnableTranscoding") val enableTranscoding: Boolean = true,
)

@JsonClass(generateAdapter = true)
data class PlaybackInfoResponseDto(
    @Json(name = "MediaSources") val mediaSources: List<MediaSourceDto> = emptyList(),
    @Json(name = "PlaySessionId") val playSessionId: String? = null,
    @Json(name = "ErrorCode") val errorCode: String? = null,
)

@JsonClass(generateAdapter = true)
data class PlaybackStartInfoDto(
    @Json(name = "ItemId") val itemId: String,
    @Json(name = "PlaySessionId") val playSessionId: String? = null,
    @Json(name = "MediaSourceId") val mediaSourceId: String? = null,
    @Json(name = "AudioStreamIndex") val audioStreamIndex: Int? = null,
    @Json(name = "SubtitleStreamIndex") val subtitleStreamIndex: Int? = null,
    @Json(name = "CanSeek") val canSeek: Boolean = true,
    @Json(name = "IsPaused") val isPaused: Boolean = false,
    @Json(name = "IsMuted") val isMuted: Boolean = false,
    @Json(name = "PositionTicks") val positionTicks: Long = 0,
    @Json(name = "VolumeLevel") val volumeLevel: Int = 100,
)

@JsonClass(generateAdapter = true)
data class PlaybackProgressInfoDto(
    @Json(name = "ItemId") val itemId: String,
    @Json(name = "PlaySessionId") val playSessionId: String? = null,
    @Json(name = "MediaSourceId") val mediaSourceId: String? = null,
    @Json(name = "AudioStreamIndex") val audioStreamIndex: Int? = null,
    @Json(name = "SubtitleStreamIndex") val subtitleStreamIndex: Int? = null,
    @Json(name = "CanSeek") val canSeek: Boolean = true,
    @Json(name = "IsPaused") val isPaused: Boolean = false,
    @Json(name = "IsMuted") val isMuted: Boolean = false,
    @Json(name = "PositionTicks") val positionTicks: Long = 0,
    @Json(name = "VolumeLevel") val volumeLevel: Int = 100,
    @Json(name = "EventName") val eventName: String? = "TimeUpdate",
)

@JsonClass(generateAdapter = true)
data class PlaybackStopInfoDto(
    @Json(name = "ItemId") val itemId: String,
    @Json(name = "PlaySessionId") val playSessionId: String? = null,
    @Json(name = "MediaSourceId") val mediaSourceId: String? = null,
    @Json(name = "PositionTicks") val positionTicks: Long = 0,
    @Json(name = "Failed") val failed: Boolean = false,
)

@JsonClass(generateAdapter = true)
data class MediaSegmentDto(
    @Json(name = "Id") val id: String? = null,
    @Json(name = "ItemId") val itemId: String? = null,
    @Json(name = "Type") val type: String? = null,
    @Json(name = "StartTicks") val startTicks: Long = 0L,
    @Json(name = "EndTicks") val endTicks: Long = 0L,
)

@JsonClass(generateAdapter = true)
data class MediaSegmentsQueryResultDto(
    @Json(name = "Items") val items: List<MediaSegmentDto> = emptyList(),
    @Json(name = "TotalRecordCount") val totalRecordCount: Int = 0,
)
