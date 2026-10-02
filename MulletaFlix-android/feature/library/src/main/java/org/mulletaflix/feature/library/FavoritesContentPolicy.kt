package org.mulletaflix.feature.library

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

internal fun favoritesItemsForDevice(items: List<MediaItem>, isTelevision: Boolean): List<MediaItem> =
    if (isTelevision) items.filterNot { it.type == MediaItemType.Book } else items

internal fun isBooksLibrary(item: MediaItem?): Boolean =
    item?.collectionType?.trim().equals("books", ignoreCase = true) ||
        item?.name?.trim().let { it.equals("Livros", ignoreCase = true) || it.equals("Books", ignoreCase = true) }

internal fun isBooksLibrary(collectionType: String?, libraryName: String?): Boolean =
    collectionType?.trim().equals("books", ignoreCase = true) ||
        libraryName?.trim().let { it.equals("Livros", ignoreCase = true) || it.equals("Books", ignoreCase = true) }

internal enum class LibraryItemTapAction { OpenDetails, ExplainOffline }

internal fun libraryItemTapAction(isOffline: Boolean): LibraryItemTapAction =
    if (isOffline) LibraryItemTapAction.ExplainOffline else LibraryItemTapAction.OpenDetails
