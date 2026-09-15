package org.mulletaflix.feature.settings

/** Cache entries that can be removed without deleting Media3 offline downloads. */
internal fun cacheEntriesToRemove(entryNames: Iterable<String>): List<String> =
    entryNames.filterNot { it.equals("downloads", ignoreCase = true) }.toList()
