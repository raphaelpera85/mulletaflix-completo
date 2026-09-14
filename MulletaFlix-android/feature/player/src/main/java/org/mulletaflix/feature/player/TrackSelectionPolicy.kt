package org.mulletaflix.feature.player

/** Converts the server's global stream index to the filtered UI-list position. */
internal fun uiTrackIndex(tracks: List<TrackInfo>, serverStreamIndex: Int?, fallback: Int): Int =
    serverStreamIndex?.let { index -> tracks.indexOfFirst { it.index == index }.takeIf { it >= 0 } }
        ?: fallback.takeIf { it in tracks.indices }
        ?: -1
