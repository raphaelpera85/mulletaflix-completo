package org.mulletaflix.core.common.cache

/** Clears volatile image artwork without touching persistent offline media downloads. */
fun interface ArtworkCacheCleaner {
    suspend fun clear()
}
