package org.mulletaflix.feature.library

import java.text.Normalizer
import java.util.Locale
import org.mulletaflix.domain.model.MediaItem

internal data class LibraryLetterTarget(val letter: String, val itemIndex: Int?)

internal sealed interface LibraryLetterNavigationDecision {
    data class ScrollTo(val itemIndex: Int) : LibraryLetterNavigationDecision
    data object LoadMore : LibraryLetterNavigationDecision
    data object Wait : LibraryLetterNavigationDecision
    data object Failed : LibraryLetterNavigationDecision
    data object NoItems : LibraryLetterNavigationDecision
}

internal fun libraryGridTargetIndex(
    itemIndex: Int,
    hasLoadError: Boolean,
    hasActiveFilters: Boolean,
): Int = itemIndex + (if (hasLoadError) 1 else 0) + (if (hasActiveFilters) 1 else 0)

/** Build loaded targets and, while paging, selectable targets for not-yet-loaded letters. */
internal fun libraryLetterTargets(
    items: List<MediaItem>,
    hasMore: Boolean = false,
    pendingLetter: String? = null,
): List<LibraryLetterTarget> {
    val targets = linkedMapOf<String, Int?>()
    items.forEachIndexed { index, item ->
        val first = libraryItemInitial(item.name)
        targets.putIfAbsent(first, index)
    }
    if (hasMore) {
        return ('A'..'Z').map { letter ->
            LibraryLetterTarget(letter.toString(), targets[letter.toString()])
        } + LibraryLetterTarget("#", targets["#"])
    }
    if (pendingLetter != null && pendingLetter !in targets) {
        targets[pendingLetter] = null
    }
    return targets.map { (letter, index) -> LibraryLetterTarget(letter, index) }
}

internal fun libraryLetterNavigationDecision(
    items: List<MediaItem>,
    letter: String,
    hasMore: Boolean,
    isLoading: Boolean,
    hasLoadError: Boolean,
): LibraryLetterNavigationDecision {
    val normalizedLetter = letter.uppercase(Locale.ROOT)
    val exactTarget = items.indexOfFirst { libraryItemInitial(it.name) == normalizedLetter }
    if (exactTarget >= 0) return LibraryLetterNavigationDecision.ScrollTo(exactTarget)
    if (hasLoadError) return LibraryLetterNavigationDecision.Failed

    if (normalizedLetter in "A".."Z") {
        val nextLetter = items.indexOfFirst { item ->
            val initial = libraryItemInitial(item.name)
            initial in "A".."Z" && initial >= normalizedLetter
        }
        if (nextLetter >= 0) return LibraryLetterNavigationDecision.ScrollTo(nextLetter)
    }

    if (items.isEmpty()) {
        return when {
            hasMore && !isLoading && !hasLoadError -> LibraryLetterNavigationDecision.LoadMore
            isLoading -> LibraryLetterNavigationDecision.Wait
            else -> LibraryLetterNavigationDecision.NoItems
        }
    }

    return when {
        hasMore && !isLoading && !hasLoadError -> LibraryLetterNavigationDecision.LoadMore
        isLoading -> LibraryLetterNavigationDecision.Wait
        else -> LibraryLetterNavigationDecision.ScrollTo(items.lastIndex)
    }
}

private fun libraryItemInitial(name: String): String =
    Normalizer.normalize(name.trim(), Normalizer.Form.NFD)
        .replace("\\p{Mn}+".toRegex(), "")
        .uppercase(Locale.ROOT)
        .firstOrNull()
        ?.takeIf { it in 'A'..'Z' }
        ?.toString() ?: "#"
