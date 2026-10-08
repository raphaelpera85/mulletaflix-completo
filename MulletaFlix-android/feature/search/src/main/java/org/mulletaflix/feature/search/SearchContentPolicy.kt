package org.mulletaflix.feature.search

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.isBookContent
import org.mulletaflix.domain.repository.SearchHintItem

internal fun searchItemsForDevice(items: List<MediaItem>, isTelevision: Boolean): List<MediaItem> =
    if (isTelevision) items.filterNot { it.type.isBookContent() } else items

internal fun searchHintsForDevice(
    hints: List<SearchHintItem>,
    isTelevision: Boolean,
): List<SearchHintItem> =
    if (isTelevision) {
        hints.filterNot { it.type.equals("Book", ignoreCase = true) || it.type.equals("Audiobook", ignoreCase = true) }
    } else {
        hints
    }

internal fun searchFiltersForDevice(isTelevision: Boolean): List<SearchFilter> =
    SearchFilter.values().filterNot { isTelevision && it == SearchFilter.Books }
