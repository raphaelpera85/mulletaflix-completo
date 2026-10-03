package org.mulletaflix.feature.itemdetail

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

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

internal enum class DetailPrimaryAction { PlayVideo, ReadBook }

internal fun detailPrimaryAction(item: MediaItem): DetailPrimaryAction =
    if (item.type == MediaItemType.Book) DetailPrimaryAction.ReadBook else DetailPrimaryAction.PlayVideo

internal fun canReadBookOnDevice(item: MediaItem, isTelevision: Boolean): Boolean =
    item.type == MediaItemType.Book && !isTelevision

/**
 * Whether the "Reproduzir" action can do anything for [item]: containers without
 * a loaded episode would only open a player that cannot prepare the media.
 */
internal fun canPlayItem(item: MediaItem, episodes: List<MediaItem>): Boolean = when (item.type) {
    MediaItemType.Series, MediaItemType.Season -> episodes.isNotEmpty()
    MediaItemType.Book -> false
    else -> true
}
