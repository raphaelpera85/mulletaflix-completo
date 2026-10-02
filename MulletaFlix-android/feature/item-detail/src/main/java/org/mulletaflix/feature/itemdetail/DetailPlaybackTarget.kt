package org.mulletaflix.feature.itemdetail

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

internal sealed interface DetailPlaybackTarget {
    data class PlayVideo(
        val itemId: String,
        val enabled: Boolean = true,
    ) : DetailPlaybackTarget

    data class ReadBook(val itemId: String) : DetailPlaybackTarget

    data object Unavailable : DetailPlaybackTarget
}

private val BOOK_READER_FORMATS = setOf("epub", "pdf", "mobi", "azw", "azw3", "txt", "html", "htm")

internal fun canReadBook(item: MediaItem): Boolean {
    if (item.type != MediaItemType.Book) return false
    return item.mediaSources.any { source ->
        val container = source.container?.trim()?.trimStart('.')?.lowercase()
        val pathExtension = source.path
            ?.substringAfterLast('.', missingDelimiterValue = "")
            ?.substringBefore('?')
            ?.lowercase()
        container in BOOK_READER_FORMATS || pathExtension in BOOK_READER_FORMATS
    }
}

internal fun detailPlaybackTarget(
    item: MediaItem,
    episodes: List<MediaItem>,
    isTelevision: Boolean,
): DetailPlaybackTarget = when (item.type) {
    MediaItemType.Book -> if (isTelevision || !canReadBook(item)) {
        DetailPlaybackTarget.Unavailable
    } else {
        DetailPlaybackTarget.ReadBook(item.id)
    }
    MediaItemType.Series, MediaItemType.Season -> DetailPlaybackTarget.PlayVideo(
        itemId = episodes.firstOrNull()?.id ?: item.id,
        enabled = episodes.isNotEmpty(),
    )
    else -> DetailPlaybackTarget.PlayVideo(item.id)
}

/**
 * Item id handed to the player when the user presses "Reproduzir" on a detail page.
 *
 * Series and seasons are containers: the player can only stream a playable item,
 * so a container plays its first loaded episode (and falls back to its own id,
 * which the screen disables, when nothing is loaded). Everything else — movies,
 * episodes, albums, channels — plays itself.
 */
internal fun playbackTargetId(item: MediaItem, episodes: List<MediaItem>): String = when (item.type) {
    MediaItemType.Series, MediaItemType.Season -> episodes.firstOrNull()?.id ?: item.id
    else -> item.id
}

/**
 * Whether the "Reproduzir" action can do anything for [item]: containers without
 * a loaded episode would only open a player that cannot prepare the media.
 */
internal fun canPlayItem(item: MediaItem, episodes: List<MediaItem>): Boolean = when (item.type) {
    MediaItemType.Series, MediaItemType.Season -> episodes.isNotEmpty()
    else -> true
}
