package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

internal fun externalSubtitleStreamsForPlayback(
    streams: List<MediaStream>,
    isCasting: Boolean,
    isOfflinePlayback: Boolean,
): List<MediaStream> = if (isOfflinePlayback) {
    emptyList()
} else {
    streams.filter { stream ->
        stream.type == MediaStreamType.Subtitle && stream.isExternal &&
            (!isCasting || castSubtitleContentType(externalSubtitleMimeType(stream.codec, stream.deliveryUrl)) != null)
    }
}

internal fun subtitleStreamsForPreferredPlayback(
    streams: List<MediaStream>,
    isCasting: Boolean,
    castSupportedExternalStreamIndices: Set<Int>? = null,
): List<MediaStream> {
    if (!isCasting) return streams
    val supportedIndices = castSupportedExternalStreamIndices ?: run {
        val supported = streams.filter { stream ->
            stream.isExternal &&
                castSubtitleContentType(externalSubtitleMimeType(stream.codec, stream.deliveryUrl)) != null
        }
        val duplicateUrls = supported.mapNotNull { it.deliveryUrl?.takeIf(String::isNotBlank) }
            .groupingBy { it }
            .eachCount()
            .filterValues { it > 1 }
            .keys
        supported.filter { stream ->
            stream.deliveryUrl.isNullOrBlank() || stream.deliveryUrl !in duplicateUrls
        }.mapTo(mutableSetOf()) { it.index }
    }
    return streams.filterNot { stream -> stream.isExternal && stream.index !in supportedIndices }
}

internal fun selectedExternalSubtitleStream(
    streams: List<MediaStream>,
    preferredStreamIndex: Int?,
): MediaStream? = preferredStreamIndex?.let { selectedIndex ->
    streams.firstOrNull { it.index == selectedIndex }
}

internal fun visibleSubtitleTracks(tracks: List<TrackInfo>, isCasting: Boolean): List<TrackInfo> =
    if (isCasting) tracks.filterNot { it.isExternal && !it.isCastSupported } else tracks

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
