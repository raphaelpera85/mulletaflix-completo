package org.mulletaflix.feature.player

import androidx.media3.common.TrackSelectionParameters
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.common.Tracks
import androidx.media3.common.util.UnstableApi

/** Converts the server's global stream index to the filtered UI-list position. */
internal fun uiTrackIndex(tracks: List<TrackInfo>, serverStreamIndex: Int?, fallback: Int): Int =
    serverStreamIndex?.let { index -> tracks.indexOfFirst { it.index == index }.takeIf { it >= 0 } }
        ?: fallback.takeIf { it in tracks.indices }
        ?: -1

/** Converts a filtered UI position back to the global stream index expected by the server. */
internal fun serverTrackIndexAt(tracks: List<TrackInfo>, uiIndex: Int): Int? =
    tracks.getOrNull(uiIndex)?.index

/**
 * Maps a Jellyfin stream index onto the position of the matching Media3 track.
 *
 * Two different numbering spaces were being compared directly:
 *
 *  - Jellyfin's `Index` is 0-based across **every** stream of the file (video
 *    first, then audio and subtitles in container order);
 *  - `Format.id` is whatever the container calls the track. For Matroska that is
 *    the EBML `TrackNumber`, which is **1-based**, so a normal dual-audio MKV has
 *    server indexes `1, 2` for its audio tracks while the container reports the
 *    ids `"2", "3"`.
 *
 * `selectTrackByServerIndex` matched the server index against `Format.id` first
 * and then fell back to comparing it with a **group-local** track index, so
 * choosing the first audio track played the second, and the last entry of the
 * list silently did nothing.
 *
 * The container preserves stream order, so the position of the server index among
 * the streams of the same type is what identifies the track. Null means the index
 * is not one of them, so the caller can decline instead of guessing.
 */
internal fun trackCandidatePosition(
    serverIndex: Int,
    orderedServerIndices: List<Int>,
): Int? = orderedServerIndices.indexOf(serverIndex).takeIf { it >= 0 }

/** Resolves one server stream to one Media3 group, even when group has many variants. */
internal fun supportedTrackGroupPosition(
    serverIndex: Int,
    orderedServerIndices: List<Int>,
    supportedTrackIndicesByGroup: List<List<Int>>,
): Int? {
    val position = trackCandidatePosition(serverIndex, orderedServerIndices) ?: return null
    return position.takeIf { !supportedTrackIndicesByGroup.getOrNull(it).isNullOrEmpty() }
}

/**
 * Server indices of the streams of one type, in the order the container lists them.
 *
 * Sorting makes the correspondence explicit instead of relying on the order the
 * server happened to serialise the streams in.
 */
internal fun orderedStreamIndices(serverIndices: List<Int>): List<Int> = serverIndices.sorted()

/** Build the same Media3 override used by the player for one selected stream. */
@UnstableApi
internal fun trackSelectionParametersForServerIndex(
    currentParameters: TrackSelectionParameters,
    tracks: Tracks,
    serverIndex: Int,
    trackType: Int,
    orderedServerIndices: List<Int>,
): TrackSelectionParameters? {
    val candidateGroups = tracks.groups.filter { it.type == trackType }
    val supportedTrackIndicesByGroup = candidateGroups.map { group ->
        (0 until group.length).filter { trackIndex -> group.isTrackSupported(trackIndex) }
    }
    val groupPosition = supportedTrackGroupPosition(
        serverIndex = serverIndex,
        orderedServerIndices = orderedServerIndices,
        supportedTrackIndicesByGroup = supportedTrackIndicesByGroup,
    ) ?: return null
    val selectedGroup = candidateGroups.getOrNull(groupPosition) ?: return null
    val selectedTrackIndices = supportedTrackIndicesByGroup.getOrNull(groupPosition)
        ?.takeIf { it.isNotEmpty() }
        ?: return null

    return currentParameters.buildUpon()
        .setTrackTypeDisabled(trackType, false)
        .setOverrideForType(
            TrackSelectionOverride(selectedGroup.mediaTrackGroup, selectedTrackIndices),
        )
        .build()
}
