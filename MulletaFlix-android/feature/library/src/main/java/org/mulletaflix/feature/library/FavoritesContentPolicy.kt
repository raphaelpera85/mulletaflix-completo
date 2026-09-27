package org.mulletaflix.feature.library

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

internal fun favoritesItemsForDevice(items: List<MediaItem>, isTelevision: Boolean): List<MediaItem> =
    if (isTelevision) items.filterNot { it.type == MediaItemType.Book } else items

internal fun isBooksLibrary(item: MediaItem?): Boolean =
    item?.collectionType?.trim().equals("books", ignoreCase = true) ||
        item?.name?.trim().let { it.equals("Livros", ignoreCase = true) || it.equals("Books", ignoreCase = true) }
