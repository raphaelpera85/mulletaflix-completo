package org.mulletaflix.feature.player

/**
 * One audio or subtitle track as the container reports it, without any server
 * metadata.
 */
internal data class OfflineTrack(
    val language: String? = null,
    val codec: String? = null,
    val channels: Int? = null,
    val isDefault: Boolean = false,
    val isForced: Boolean = false,
    val isSelected: Boolean = false,
    val formatId: String? = null,
    val label: String? = null,
) {
    /** `TrackInfo.codec` is displayed uppercased, so a bare MIME subtype is enough. */
    fun codecLabel(): String? = codec
        ?.substringAfterLast('/')
        ?.trim()
        ?.takeIf { it.isNotEmpty() }
}

/**
 * Position of the track the player is currently using, or -1.
 *
 * Offline the radio must reflect what is actually playing: the player picks a
 * default track as soon as the container is read, so leaving the selection at
 * "none" would show a menu where no track is marked.
 */
internal fun selectedOfflineTrackIndex(offlineTracks: List<OfflineTrack>): Int =
    offlineTracks.indexOfFirst { it.isSelected }.takeIf { it >= 0 } ?: -1

/** Preserve an explicit user's "off" choice across subsequent track refreshes. */
internal fun offlineSubtitleSelectionIndex(offlineTracks: List<OfflineTrack>, subtitlesDisabled: Boolean): Int =
    if (subtitlesDisabled) -1 else selectedOfflineTrackIndex(offlineTracks)

/**
 * Track list for a downloaded item, built from the container itself.
 *
 * Offline there is no server metadata, so `audioTracks` and `subtitleTracks`
 * stayed empty — and the OSD enables its audio/subtitle buttons from those lists,
 * so a download that carries several tracks could not have any of them switched.
 * The buttons were disabled by missing state, not by missing content.
 *
 * `index` is the track's position in this list. `selectTrackByServerIndex`
 * resolves a server index by looking it up among the indices of its own type, so
 * offline the position is the identity and the same selection path works without
 * a second code path.
 */
internal fun offlineTrackInfos(
    offlineTracks: List<OfflineTrack>,
    fallbackPrefix: String,
): List<TrackInfo> {
    var embeddedPosition = 0
    var externalPosition = 0
    return offlineTracks.map { track ->
        val externalIndex = externalSubtitleServerIndex(track.formatId)
        // Each type gets its own sequential fallback count, so an unlabeled external
        // track never skips a number because of where an embedded track sits in the
        // combined list (and vice versa).
        val trackIndex = externalIndex ?: embeddedPosition++
        val fallbackNumber = if (externalIndex == null) trackIndex + 1 else ++externalPosition
        TrackInfo(
            index = trackIndex,
            displayName = track.label?.takeIf(String::isNotBlank) ?: friendlyTrackName(
                displayName = null,
                language = track.language,
                fallback = "$fallbackPrefix $fallbackNumber",
            ),
            language = track.language,
            codec = track.codecLabel(),
            channels = track.channels,
            isDefault = track.isDefault,
            isForced = track.isForced,
            isExternal = externalIndex != null,
        )
    }
}

internal fun offlineSubtitleTrackIndex(streamIndex: Int): Int? =
    streamIndex.takeIf { it in 0..100_000 }?.let { OFFLINE_EXTERNAL_INDEX_BASE + it }

private const val OFFLINE_EXTERNAL_INDEX_BASE = 1_000_000

/**
 * The indices [selectTrackByServerIndex] should look a position up in, offline.
 *
 * The list is simply `0 until size`, which makes the lookup the identity.
 */
internal fun offlineStreamIndices(trackCount: Int): List<Int> = List(trackCount) { it }
