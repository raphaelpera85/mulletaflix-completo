package org.mulletaflix.data.mapper

import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.MediaSourceDto
import org.mulletaflix.core.api.dto.MediaStreamDto
import org.mulletaflix.core.api.dto.PersonDto
import org.mulletaflix.data.db.MediaItemEntity
import org.mulletaflix.domain.model.*

fun BaseItemDto.toDomain(): MediaItem {
    val mappedType = when (type?.lowercase()) {
        "movie" -> MediaItemType.Movie
        "series" -> MediaItemType.Series
        "season" -> MediaItemType.Season
        "episode" -> MediaItemType.Episode
        "musicalbum" -> MediaItemType.MusicAlbum
        "audio" -> MediaItemType.Audio
        "musicartist" -> MediaItemType.MusicArtist
        "musicvideo" -> MediaItemType.MusicVideo
        "book" -> MediaItemType.Book
        "photo" -> MediaItemType.Photo
        "photoalbum" -> MediaItemType.PhotoAlbum
        "collectionfolder" -> MediaItemType.CollectionFolder
        "folder" -> MediaItemType.Folder
        "boxset" -> MediaItemType.BoxSet
        "livetvchannel" -> MediaItemType.LiveTvChannel
        "livetvprogram" -> MediaItemType.LiveTvProgram
        "recording" -> MediaItemType.Recording
        "playlist" -> MediaItemType.Playlist
        "trailer" -> MediaItemType.Trailer
        else -> MediaItemType.Unknown
    }

    val mappedImageTags = mutableMapOf<ImageType, String>()
    imageTags?.forEach { (key, value) ->
        val imageType = when (key.lowercase()) {
            "primary" -> ImageType.Primary
            "art" -> ImageType.Art
            "backdrop" -> ImageType.Backdrop
            "banner" -> ImageType.Banner
            "logo" -> ImageType.Logo
            "thumb" -> ImageType.Thumb
            "disc" -> ImageType.Disc
            "box" -> ImageType.Box
            "screenshot" -> ImageType.Screenshot
            "menu" -> ImageType.Menu
            "chapter" -> ImageType.Chapter
            "boxrear" -> ImageType.BoxRear
            "profile" -> ImageType.Profile
            else -> null
        }
        if (imageType != null) {
            mappedImageTags[imageType] = value
        }
    }

    val streams = mediaStreams?.map { it.toDomain() } ?: emptyList()
    val has4KVideo = streams.any { it.type == MediaStreamType.Video && (it.width ?: 0) >= 3840 }
    val hasHdVideo = streams.any { it.type == MediaStreamType.Video && (it.height ?: 0) >= 720 }
    val hasHdrVideo = streams.any { it.displayTitle?.contains("HDR", ignoreCase = true) == true }
    val hasDv = streams.any { it.displayTitle?.contains("DV", ignoreCase = true) == true || it.displayTitle?.contains("Dolby Vision", ignoreCase = true) == true }
    val hasAtmosAudio = streams.any { it.displayTitle?.contains("Atmos", ignoreCase = true) == true }

    return MediaItem(
        id = id,
        name = name ?: originalTitle ?: "",
        type = mappedType,
        overview = overview,
        tagline = taglines?.firstOrNull(),
        year = productionYear,
        runtimeTicks = runTimeTicks,
        genres = genres ?: emptyList(),
        officialRating = officialRating,
        communityRating = communityRating,
        imageTags = mappedImageTags,
        backdropImageTags = backdropImageTags ?: emptyList(),
        userProgress = userData?.let {
            UserProgress(
                isFavorite = it.isFavorite,
                isPlayed = it.played,
                playedPercentage = it.playedPercentage,
                playbackPositionTicks = it.playbackPositionTicks,
                playCount = it.playCount,
                lastPlayedDate = it.lastPlayedDate,
            )
        },
        seriesId = seriesId,
        seriesName = seriesName,
        seasonId = seasonId,
        seasonName = seasonName,
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        people = people?.map { it.toDomain() } ?: emptyList(),
        mediaStreams = streams,
        mediaSources = mediaSources?.map { it.toDomain() } ?: emptyList(),
        collectionType = collectionType,
        channelId = channelId,
        startDate = startDate,
        endDate = endDate,
        isFavorite = userData?.isFavorite ?: false,
        isPlayed = userData?.played ?: false,
        playedPercentage = userData?.playedPercentage,
        playbackPositionTicks = userData?.playbackPositionTicks,
        albumId = albumId,
        albumArtistId = albumArtist,
        has4K = has4KVideo,
        hasHD = hasHdVideo,
        hasDolbyVision = hasDv,
        hasHdr = hasHdrVideo,
        hasAtmos = hasAtmosAudio,
    )
}

fun MediaStreamDto.toDomain(): MediaStream {
    val streamType = when (type?.lowercase()) {
        "video" -> MediaStreamType.Video
        "audio" -> MediaStreamType.Audio
        "subtitle" -> MediaStreamType.Subtitle
        "embeddedimage" -> MediaStreamType.EmbeddedImage
        "attachment" -> MediaStreamType.Attachment
        else -> MediaStreamType.Data
    }

    return MediaStream(
        index = index,
        type = streamType,
        codec = codec,
        language = language,
        displayLanguage = displayLanguage,
        title = title,
        isDefault = isDefault,
        isForced = isForced,
        isExternal = isExternal,
        height = height,
        width = width,
        bitRate = bitRate,
        sampleRate = sampleRate,
        channels = channels,
        displayTitle = displayTitle,
    )
}

fun MediaSourceDto.toDomain(): MediaSource {
    return MediaSource(
        id = id ?: "",
        name = name,
        container = container,
        size = size,
        bitrate = bitrate,
        path = path,
        isRemote = isRemote,
        supportsDirectPlay = supportsDirectPlay,
        supportsDirectStream = supportsDirectStream,
        supportsTranscoding = supportsTranscoding,
        mediaStreams = mediaStreams?.map { it.toDomain() } ?: emptyList(),
    )
}

fun PersonDto.toDomain(): PersonInfo {
    return PersonInfo(
        id = id ?: "",
        name = name,
        type = type ?: "Actor",
        role = role,
        primaryImageTag = primaryImageTag,
    )
}

fun MediaItemEntity.toDomain(): MediaItem {
    return MediaItem(
        id = id,
        name = name,
        type = runCatching { MediaItemType.valueOf(type) }.getOrDefault(MediaItemType.Unknown),
        overview = overview,
        year = year,
        runtimeTicks = runtimeTicks,
        isFavorite = isFavorite,
        isPlayed = isPlayed,
        playedPercentage = playedPercentage,
        playbackPositionTicks = playbackPositionTicks,
        seriesId = seriesId,
        seriesName = seriesName,
        seasonId = seasonId,
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        imageTags = primaryImageTag?.let { mapOf(ImageType.Primary to it) } ?: emptyMap(),
        backdropImageTags = backdropImageTag?.let { listOf(it) } ?: emptyList(),
    )
}

fun MediaItem.toEntity(userId: String = ""): MediaItemEntity {
    return MediaItemEntity(
        id = id,
        name = name,
        type = type.name,
        overview = overview,
        year = year,
        runtimeTicks = runtimeTicks,
        isFavorite = isFavorite,
        isPlayed = isPlayed,
        playedPercentage = playedPercentage,
        playbackPositionTicks = playbackPositionTicks,
        primaryImageTag = imageTags[ImageType.Primary],
        backdropImageTag = backdropImageTags.firstOrNull(),
        seriesId = seriesId,
        seriesName = seriesName,
        seasonId = seasonId,
        indexNumber = indexNumber,
        parentIndexNumber = parentIndexNumber,
        userId = userId,
    )
}
