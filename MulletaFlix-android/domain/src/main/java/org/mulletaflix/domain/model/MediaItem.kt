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
    val premiereDate: String? = null,
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
    val primaryImageTag: String? = null,
    val seriesPrimaryImageTag: String? = null,
    val seriesThumbImageTag: String? = null,
    val parentThumbItemId: String? = null,
    val parentThumbImageTag: String? = null,
    val parentBackdropItemId: String? = null,
    val parentBackdropImageTags: List<String> = emptyList(),
)

/** Year label used by cards and detail metadata. */
fun MediaItem.displayYearRange(): String? {
    val startYear = year ?: premiereDate?.take(4)?.toIntOrNull()
    if (startYear == null) return null
    if (type != MediaItemType.Series) return startYear.toString()

    val endYear = endDate?.take(4)?.toIntOrNull()
    return when {
        endYear != null && endYear != startYear -> "$startYear - $endYear"
        endYear == null -> "$startYear - Presente"
        else -> startYear.toString()
    }
}

enum class MediaItemType {
    Movie, Series, Season, Episode,
    MusicAlbum, Audio, MusicArtist, MusicVideo,
    Book, Photo, PhotoAlbum,
    CollectionFolder, Folder, BoxSet,
    LiveTvChannel, LiveTvProgram, Recording,
    Playlist, Trailer, Unknown
}

/**
 * Identifies item types whose canonical artwork is a vertical title poster.
 * Episodes and live content intentionally keep their landscape presentation.
 */
fun MediaItemType.usesPosterArtwork(): Boolean = when (this) {
    MediaItemType.Movie,
    MediaItemType.Series,
    MediaItemType.MusicAlbum,
    MediaItemType.Book -> true
    else -> false
}

/** Converts the server percentage to the safe fraction expected by Compose. */
fun MediaItem.playbackProgressFraction(): Float {
    val percentage = playedPercentage ?: return 0f
    if (!percentage.isFinite()) return 0f
    return (percentage / 100.0).coerceIn(0.0, 1.0).toFloat()
}

/** Compact metadata shared by every card surface for episodic items. */
fun MediaItem.cardMetadata(): String? = when (type) {
    MediaItemType.Episode -> {
        if (parentIndexNumber == null || indexNumber == null) null
        else "T${parentIndexNumber.toString().padStart(2, '0')} · E${indexNumber.toString().padStart(2, '0')}"
    }
    MediaItemType.Season -> parentIndexNumber?.let { "Temporada $it" }
    else -> null
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

/**
 * Standard primary image URL with full fallback cascade.
 * Ensures older media without primary tags, episodes with series tags, or thumb-only items display properly.
 */
val MediaItem.primaryImageUrl: String? get() = bestImageUrl(preferBackdrop = false)

/**
 * Backdrop image URL falling back to primary/thumb if no backdrop is present.
 */
val MediaItem.backdropImageUrl: String? get() = bestImageUrl(preferBackdrop = true)

/**
 * Unified image resolution cascade matching MulletaFlix Web & Jellyfin client standards.
 */
fun MediaItem.bestImageUrl(preferBackdrop: Boolean = false): String? {
    if (preferBackdrop) {
        val backdrop = backdropImageTags.firstOrNull() ?: imageTags[ImageType.Backdrop]
        if (backdrop != null) return "Items/$id/Images/Backdrop?tag=$backdrop"
        if (parentBackdropImageTags.isNotEmpty() && !parentBackdropItemId.isNullOrBlank()) {
            return "Items/$parentBackdropItemId/Images/Backdrop?tag=${parentBackdropImageTags.first()}"
        }
    }

    // 1. Direct primary image tag
    imageTags[ImageType.Primary]?.let { return "Items/$id/Images/Primary?tag=$it" }
    primaryImageTag?.let { return "Items/$id/Images/Primary?tag=$it" }

    // 2. Series primary image tag (crucial for episodes / seasons)
    if (!seriesPrimaryImageTag.isNullOrBlank() && !seriesId.isNullOrBlank()) {
        return "Items/$seriesId/Images/Primary?tag=$seriesPrimaryImageTag"
    }

    // 3. Thumb tag
    imageTags[ImageType.Thumb]?.let { return "Items/$id/Images/Thumb?tag=$it" }

    // 4. Backdrop fallback (vital for older media in Jellyfin where primary poster was never generated)
    val backdrop = backdropImageTags.firstOrNull() ?: imageTags[ImageType.Backdrop]
    if (backdrop != null) return "Items/$id/Images/Backdrop?tag=$backdrop"

    // 5. Series thumb tag
    if (!seriesThumbImageTag.isNullOrBlank() && !seriesId.isNullOrBlank()) {
        return "Items/$seriesId/Images/Thumb?tag=$seriesThumbImageTag"
    }

    // 6. Parent thumb tag
    if (!parentThumbImageTag.isNullOrBlank() && !parentThumbItemId.isNullOrBlank()) {
        return "Items/$parentThumbItemId/Images/Thumb?tag=$parentThumbImageTag"
    }

    // 7. Parent backdrop tag
    if (parentBackdropImageTags.isNotEmpty() && !parentBackdropItemId.isNullOrBlank()) {
        return "Items/$parentBackdropItemId/Images/Backdrop?tag=${parentBackdropImageTags.first()}"
    }

    // 8. Fallback to direct Primary endpoint on Jellyfin server
    return "Items/$id/Images/Primary"
}
