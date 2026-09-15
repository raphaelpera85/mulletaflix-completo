package org.mulletaflix.feature.player

/** Converts the server's global stream index to the filtered UI-list position. */
internal fun uiTrackIndex(tracks: List<TrackInfo>, serverStreamIndex: Int?, fallback: Int): Int =
    serverStreamIndex?.let { index -> tracks.indexOfFirst { it.index == index }.takeIf { it >= 0 } }
        ?: fallback.takeIf { it in tracks.indices }
        ?: -1

/** Converts a filtered UI position back to the global stream index expected by the server. */
internal fun serverTrackIndexAt(tracks: List<TrackInfo>, uiIndex: Int): Int? =
    tracks.getOrNull(uiIndex)?.index
