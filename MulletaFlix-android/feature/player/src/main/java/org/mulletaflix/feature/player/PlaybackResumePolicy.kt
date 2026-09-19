package org.mulletaflix.feature.player

/** Server progress is authoritative; local progress fills only a missing sync window. */
internal fun chooseResumePositionMs(serverPositionMs: Long?, localPositionMs: Long?): Long =
    serverPositionMs?.takeIf { it > 0L }
        ?: localPositionMs?.takeIf { it > 0L }
        ?: 0L
