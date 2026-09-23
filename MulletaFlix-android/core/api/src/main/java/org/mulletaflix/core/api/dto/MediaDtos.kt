package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

@JsonClass(generateAdapter = true)
data class BaseItemDtoQueryResultDto(
    @Json(name = "Items") val items: List<BaseItemDto> = emptyList(),
    @Json(name = "TotalRecordCount") val totalRecordCount: Int = 0,
    @Json(name = "StartIndex") val startIndex: Int = 0,
)

@JsonClass(generateAdapter = true)
data class PlaylistCreationResultDto(
    @Json(name = "Id") val id: String? = null,
)

@JsonClass(generateAdapter = true)
data class BaseItemDto(
    @Json(name = "Id") val id: String,
    @Json(name = "Name") val name: String? = null,
    @Json(name = "OriginalTitle") val originalTitle: String? = null,
    @Json(name = "ServerId") val serverId: String? = null,
    @Json(name = "Type") val type: String? = null,
    @Json(name = "Overview") val overview: String? = null,
    @Json(name = "Taglines") val taglines: List<String>? = null,
    @Json(name = "Genres") val genres: List<String>? = null,
    @Json(name = "CommunityRating") val communityRating: Float? = null,
    @Json(name = "OfficialRating") val officialRating: String? = null,
    @Json(name = "RunTimeTicks") val runTimeTicks: Long? = null,
    @Json(name = "ProductionYear") val productionYear: Int? = null,
    @Json(name = "PremiereDate") val premiereDate: String? = null,
    @Json(name = "RecursiveUnplayedItemCount") val recursiveUnplayedItemCount: Int? = null,
    @Json(name = "UserData") val userData: UserItemDataDto? = null,
    @Json(name = "SeriesName") val seriesName: String? = null,
    @Json(name = "SeriesId") val seriesId: String? = null,
    @Json(name = "SeasonId") val seasonId: String? = null,
    @Json(name = "SeasonName") val seasonName: String? = null,
    @Json(name = "IndexNumber") val indexNumber: Int? = null,
    @Json(name = "ParentIndexNumber") val parentIndexNumber: Int? = null,
    @Json(name = "Album") val album: String? = null,
    @Json(name = "AlbumId") val albumId: String? = null,
    @Json(name = "AlbumArtist") val albumArtist: String? = null,
    @Json(name = "Artists") val artists: List<String>? = null,
    @Json(name = "ImageTags") val imageTags: Map<String, String>? = null,
    @Json(name = "BackdropImageTags") val backdropImageTags: List<String>? = null,
    @Json(name = "PrimaryImageTag") val primaryImageTag: String? = null,
    @Json(name = "SeriesPrimaryImageTag") val seriesPrimaryImageTag: String? = null,
    @Json(name = "SeriesThumbImageTag") val seriesThumbImageTag: String? = null,
    @Json(name = "ParentThumbItemId") val parentThumbItemId: String? = null,
    @Json(name = "ParentThumbImageTag") val parentThumbImageTag: String? = null,
    @Json(name = "ParentBackdropItemId") val parentBackdropItemId: String? = null,
    @Json(name = "ParentBackdropImageTags") val parentBackdropImageTags: List<String>? = null,
    @Json(name = "MediaSources") val mediaSources: List<MediaSourceDto>? = null,
    @Json(name = "MediaStreams") val mediaStreams: List<MediaStreamDto>? = null,
    @Json(name = "People") val people: List<PersonDto>? = null,
    @Json(name = "Chapters") val chapters: List<ChapterInfoDto>? = null,
    @Json(name = "CollectionType") val collectionType: String? = null,
    @Json(name = "ChannelId") val channelId: String? = null,
    @Json(name = "ChannelName") val channelName: String? = null,
    @Json(name = "StartDate") val startDate: String? = null,
    @Json(name = "EndDate") val endDate: String? = null,
)

@JsonClass(generateAdapter = true)
data class UserItemDataDto(
    @Json(name = "Rating") val rating: Double? = null,
    @Json(name = "PlayedPercentage") val playedPercentage: Double? = null,
    @Json(name = "PlaybackPositionTicks") val playbackPositionTicks: Long = 0,
    @Json(name = "PlayCount") val playCount: Int = 0,
    @Json(name = "IsFavorite") val isFavorite: Boolean = false,
    @Json(name = "Likes") val likes: Boolean? = null,
    @Json(name = "LastPlayedDate") val lastPlayedDate: String? = null,
    @Json(name = "Played") val played: Boolean = false,
    @Json(name = "Key") val key: String? = null,
    @Json(name = "ItemId") val itemId: String? = null,
)

@JsonClass(generateAdapter = true)
data class MediaSourceDto(
    @Json(name = "Id") val id: String? = null,
    @Json(name = "Path") val path: String? = null,
    @Json(name = "Protocol") val protocol: String? = null,
    @Json(name = "Container") val container: String? = null,
    @Json(name = "Size") val size: Long? = null,
    @Json(name = "Name") val name: String? = null,
    @Json(name = "IsRemote") val isRemote: Boolean = false,
    @Json(name = "SupportsDirectPlay") val supportsDirectPlay: Boolean = true,
    @Json(name = "SupportsDirectStream") val supportsDirectStream: Boolean = true,
    @Json(name = "SupportsTranscoding") val supportsTranscoding: Boolean = true,
    // The server chooses these indices after applying the user's profile and the
    // media source policy. Keeping them is different from the `IsDefault` flag on
    // each stream: that flag is only one input to the server's selector.
    @Json(name = "DefaultAudioStreamIndex") val defaultAudioStreamIndex: Int? = null,
    @Json(name = "DefaultSubtitleStreamIndex") val defaultSubtitleStreamIndex: Int? = null,
    @Json(name = "MediaStreams") val mediaStreams: List<MediaStreamDto>? = null,
    @Json(name = "Bitrate") val bitrate: Int? = null,
    // A tuner channel arrives closed: `RequiresOpening` is true and `LiveStreamId` is
    // empty until something calls `LiveStreams/Open`. The id that call returns is what
    // the stream route needs to find the feed.
    @Json(name = "RequiresOpening") val requiresOpening: Boolean = false,
    @Json(name = "LiveStreamId") val liveStreamId: String? = null,
    @Json(name = "OpenToken") val openToken: String? = null,
)

/**
 * Body of `LiveStreams/Open`.
 *
 * The route reads every field from the query string *or* from this body
 * (`MediaInfoController.OpenLiveStream`), and the official web client sends the user,
 * the item and the session in the query while the open token and the direct-play flags
 * travel here.
 */
@JsonClass(generateAdapter = true)
data class OpenLiveStreamDto(
    @Json(name = "UserId") val userId: String,
    @Json(name = "ItemId") val itemId: String,
    @Json(name = "PlaySessionId") val playSessionId: String? = null,
    @Json(name = "OpenToken") val openToken: String? = null,
    @Json(name = "StartTimeTicks") val startTimeTicks: Long? = null,
    @Json(name = "AudioStreamIndex") val audioStreamIndex: Int? = null,
    @Json(name = "SubtitleStreamIndex") val subtitleStreamIndex: Int? = null,
    @Json(name = "MaxStreamingBitrate") val maxStreamingBitrate: Int? = null,
    @Json(name = "EnableDirectPlay") val enableDirectPlay: Boolean = true,
    @Json(name = "EnableDirectStream") val enableDirectStream: Boolean = true,
)

/** `LiveStreams/Open` answers with the opened media source. */
@JsonClass(generateAdapter = true)
data class LiveStreamResponseDto(
    @Json(name = "MediaSource") val mediaSource: MediaSourceDto? = null,
)

@JsonClass(generateAdapter = true)
data class MediaStreamDto(
    @Json(name = "Codec") val codec: String? = null,
    @Json(name = "Language") val language: String? = null,
    @Json(name = "TimeBase") val timeBase: String? = null,
    @Json(name = "Title") val title: String? = null,
    @Json(name = "DisplayTitle") val displayTitle: String? = null,
    @Json(name = "DisplayLanguage") val displayLanguage: String? = null,
    @Json(name = "Type") val type: String? = null, // Video, Audio, Subtitle
    @Json(name = "AspectRatio") val aspectRatio: String? = null,
    @Json(name = "Index") val index: Int = 0,
    @Json(name = "BitRate") val bitRate: Int? = null,
    @Json(name = "Channels") val channels: Int? = null,
    @Json(name = "SampleRate") val sampleRate: Int? = null,
    @Json(name = "IsDefault") val isDefault: Boolean = false,
    @Json(name = "IsForced") val isForced: Boolean = false,
    @Json(name = "IsExternal") val isExternal: Boolean = false,
    @Json(name = "Height") val height: Int? = null,
    @Json(name = "Width") val width: Int? = null,
    @Json(name = "AverageFrameRate") val averageFrameRate: Float? = null,
    @Json(name = "RealFrameRate") val realFrameRate: Float? = null,
    @Json(name = "DeliveryUrl") val deliveryUrl: String? = null,
)

@JsonClass(generateAdapter = true)
data class PersonDto(
    @Json(name = "Name") val name: String,
    @Json(name = "Id") val id: String? = null,
    @Json(name = "Role") val role: String? = null,
    @Json(name = "Type") val type: String? = null,
    @Json(name = "PrimaryImageTag") val primaryImageTag: String? = null,
)

@JsonClass(generateAdapter = true)
data class ChapterInfoDto(
    @Json(name = "StartPositionTicks") val startPositionTicks: Long = 0,
    @Json(name = "Name") val name: String? = null,
    @Json(name = "ImageTag") val imageTag: String? = null,
)
