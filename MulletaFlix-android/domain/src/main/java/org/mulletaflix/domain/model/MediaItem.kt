package org.mulletaflix.domain.model

/**
 * Pure Kotlin domain model for a media item.
 *
 * No framework annotations, no Android imports — pure domain layer per
 * android-clean-architecture skill: "domain must NEVER depend on data,
 * presentation, or any framework."
 */
data class MediaItem(
    val id: String,
    val name: String,
    val type: MediaItemType,
    val overview: String? = null,
    val tagline: String? = null,
    val year: Int? = null,
    val runtimeTicks: Long? = null,   // 1 tick = 100 nanoseconds
    val genres: List<String> = emptyList(),
    val officialRating: String? = null,
    val communityRating: Float? = null,
    val criticRating: Float? = null,
    val imageTags: Map<ImageType, String> = emptyMap(),
    val backdropImageTags: List<String> = emptyList(),
    val userProgress: UserProgress? = null,
    val seriesId: String? = null,
    val seriesName: String? = null,
    val seasonId: String? = null,
    val seasonName: String? = null,
    val indexNumber: Int? = null,     // episode/track number
    val parentIndexNumber: Int? = null, // season number
    val studios: List<Studio> = emptyList(),
    val people: List<PersonInfo> = emptyList(),
    val mediaStreams: List<MediaStream> = emptyList(),
    val chapters: List<Chapter> = emptyList(),
    val mediaSources: List<MediaSource> = emptyList(),
    val childCount: Int? = null,
    val unplayedItemCount: Int? = null,
    val isFolder: Boolean = false,
    val collectionType: String? = null,
    val primaryImageAspectRatio: Double? = null,
    val canDelete: Boolean = false,
    val canDownload: Boolean = false,
    val isFavorite: Boolean = false,
    val isPlayed: Boolean = false,
    val playedPercentage: Double? = null,
    val playbackPositionTicks: Long? = null,
    val albumId: String? = null,
    val albumArtistId: String? = null,
    val channelId: String? = null,
    val startDate: String? = null,  // ISO-8601 for Live TV / recordings
    val endDate: String? = null,
    val isLive: Boolean = false,
    val videoType: VideoType? = null,
    val has4K: Boolean = false,
    val hasHD: Boolean = false,
    val hasDolbyVision: Boolean = false,
    val hasHdr: Boolean = false,
    val hasAtmos: Boolean = false,
)

enum class MediaItemType {
    Movie, Series, Season, Episode,
    MusicAlbum, Audio, MusicArtist, MusicVideo,
    Book, Photo, PhotoAlbum,
    CollectionFolder, Folder, BoxSet,
    LiveTvChannel, LiveTvProgram, Recording,
    Playlist, Trailer, Unknown
}

enum class ImageType {
    Primary, Art, Backdrop, Banner, Logo, Thumb, Disc, Box, Screenshot, Menu, Chapter, BoxRear, Profile
}

enum class VideoType { VideoFile, Iso, Dvd, BluRay }

data class UserProgress(
    val isFavorite: Boolean = false,
    val isPlayed: Boolean = false,
    val playedPercentage: Double? = null,
    val playbackPositionTicks: Long? = null,
    val unplayedItemCount: Int? = null,
    val lastPlayedDate: String? = null,
    val playCount: Int = 0,
)

data class Studio(val id: String, val name: String)

data class PersonInfo(
    val id: String,
    val name: String,
    val type: String,  // Actor, Director, Writer, Producer, ...
    val role: String? = null,
    val primaryImageTag: String? = null,
)

data class MediaStream(
    val index: Int,
    val type: MediaStreamType,
    val codec: String? = null,
    val language: String? = null,
    val displayLanguage: String? = null,
    val title: String? = null,
    val isDefault: Boolean = false,
    val isForced: Boolean = false,
    val isExternal: Boolean = false,
    val height: Int? = null,
    val width: Int? = null,
    val bitRate: Int? = null,
    val sampleRate: Int? = null,
    val channels: Int? = null,
    val displayTitle: String? = null,
)

enum class MediaStreamType { Video, Audio, Subtitle, EmbeddedImage, Attachment, Data }

data class MediaSource(
    val id: String,
    val name: String? = null,
    val container: String? = null,
    val size: Long? = null,
    val bitrate: Int? = null,
    val path: String? = null,
    val isRemote: Boolean = false,
    val supportsDirectPlay: Boolean = false,
    val supportsDirectStream: Boolean = false,
    val supportsTranscoding: Boolean = false,
    val transcodeUrl: String? = null,
    val directStreamUrl: String? = null,
    val mediaStreams: List<MediaStream> = emptyList(),
    val defaultAudioStreamIndex: Int? = null,
    val defaultSubtitleStreamIndex: Int? = null,
)

data class Chapter(
    val startPositionTicks: Long,
    val name: String? = null,
    val imageTag: String? = null,
)
