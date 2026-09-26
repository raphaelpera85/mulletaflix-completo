package org.mulletaflix.feature.library

import java.text.Normalizer
import java.util.Locale
import org.mulletaflix.domain.model.MediaItem

internal data class LibraryLetterTarget(val letter: String, val itemIndex: Int)

internal fun libraryGridTargetIndex(
    itemIndex: Int,
    hasLoadError: Boolean,
    hasActiveFilters: Boolean,
): Int = itemIndex + (if (hasLoadError) 1 else 0) + (if (hasActiveFilters) 1 else 0)

/** Build jump targets from the loaded, alphabetically sorted catalog. */
internal fun libraryLetterTargets(items: List<MediaItem>): List<LibraryLetterTarget> {
    val targets = linkedMapOf<String, Int>()
    items.forEachIndexed { index, item ->
        val first = Normalizer.normalize(item.name.trim(), Normalizer.Form.NFD)
            .replace("\\p{Mn}+".toRegex(), "")
            .uppercase(Locale.ROOT)
            .firstOrNull()
            ?.takeIf { it in 'A'..'Z' }
            ?.toString() ?: "#"
        targets.putIfAbsent(first, index)
    }
    return targets.map { (letter, index) -> LibraryLetterTarget(letter, index) }
}
