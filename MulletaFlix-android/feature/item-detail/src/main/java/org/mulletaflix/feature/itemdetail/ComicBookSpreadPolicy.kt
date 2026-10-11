package org.mulletaflix.feature.itemdetail

internal const val COMIC_BOOK_SPREAD_MIN_WIDTH_DP = 840
internal const val COMIC_BOOK_TABLET_MIN_SMALLEST_WIDTH_DP = 600

internal fun shouldShowComicBookSpread(
    widthDp: Int,
    smallestWidthDp: Int,
    isTelevision: Boolean,
    isComicBook: Boolean,
): Boolean = isComicBook && !isTelevision &&
    smallestWidthDp >= COMIC_BOOK_TABLET_MIN_SMALLEST_WIDTH_DP &&
    widthDp >= COMIC_BOOK_SPREAD_MIN_WIDTH_DP

internal fun comicBookVisiblePages(currentPage: Int, pageCount: Int, useSpread: Boolean): List<Int> =
    if (pageCount <= 0) {
        emptyList()
    } else {
        val firstPage = currentPage.coerceIn(0, pageCount - 1)
        buildList {
            add(firstPage)
            if (useSpread && firstPage + 1 < pageCount) add(firstPage + 1)
        }
    }
