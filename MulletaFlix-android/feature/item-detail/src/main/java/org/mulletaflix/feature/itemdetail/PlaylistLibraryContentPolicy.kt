package org.mulletaflix.feature.itemdetail

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.isBookContent

internal fun playlistItemsForDevice(items: List<MediaItem>, isTelevision: Boolean): List<MediaItem> =
    if (isTelevision) items.filterNot { it.type.isBookContent() } else items

internal fun playlistVisibleCountLabel(count: Int): String =
    if (count == 1) "1 título visível" else "$count títulos visíveis"
