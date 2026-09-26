package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

internal fun externalSubtitleStreamsForPlayback(
    streams: List<MediaStream>,
    isCasting: Boolean,
    isOfflinePlayback: Boolean,
): List<MediaStream> = if (isCasting || isOfflinePlayback) {
    emptyList()
} else {
    streams.filter { it.type == MediaStreamType.Subtitle && it.isExternal }
}

internal fun subtitleStreamsForPreferredPlayback(
    streams: List<MediaStream>,
    isCasting: Boolean,
): List<MediaStream> = if (isCasting) streams.filterNot(MediaStream::isExternal) else streams

internal fun selectedExternalSubtitleStream(
    streams: List<MediaStream>,
    preferredStreamIndex: Int?,
): MediaStream? = preferredStreamIndex?.let { selectedIndex ->
    streams.firstOrNull { it.index == selectedIndex }
}

internal fun visibleSubtitleTracks(tracks: List<TrackInfo>, isCasting: Boolean): List<TrackInfo> =
    if (isCasting) tracks.filterNot(TrackInfo::isExternal) else tracks

internal fun visibleSubtitleSelectionIndex(
    tracks: List<TrackInfo>,
    selectedIndex: Int,
    visibleTracks: List<TrackInfo>,
): Int {
    val selected = tracks.getOrNull(selectedIndex) ?: return -1
    return visibleTracks.indexOfFirst { it.index == selected.index }
}

internal fun originalSubtitleSelectionIndex(
    allTracks: List<TrackInfo>,
    visibleTracks: List<TrackInfo>,
    visibleIndex: Int,
): Int? = visibleTracks.getOrNull(visibleIndex)?.let { visibleTrack ->
    allTracks.indexOfFirst { it.index == visibleTrack.index }.takeIf { it >= 0 }
}
