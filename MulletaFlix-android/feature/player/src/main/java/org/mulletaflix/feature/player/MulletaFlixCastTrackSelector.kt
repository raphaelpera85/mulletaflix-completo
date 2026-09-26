package org.mulletaflix.feature.player

import androidx.media3.cast.CastTrackSelector
import androidx.media3.common.C
import androidx.media3.common.TrackSelectionOverride
import androidx.media3.common.TrackSelectionParameters
import androidx.media3.common.util.UnstableApi
import com.google.android.gms.cast.MediaTrack
import com.google.common.collect.ImmutableList
import com.google.common.collect.ImmutableSet

/**
 * Bridges Media3 track choices to the tracks advertised by the active Cast receiver.
 * Media3 intentionally leaves remote track selection disabled unless an app supplies
 * this selector; without it, changing a track in the sender UI is silently ignored.
 */
@UnstableApi
internal class MulletaFlixCastTrackSelector : CastTrackSelector() {
    override fun evaluate(request: CastTrackSelectorRequest): CastTrackSelectorResult {
        if (request.trackSelectionRequestReason == TRACK_SELECTION_REQUEST_REASON_RECEIVER_UPDATE) {
            return request.buildResultUpon()
                .setTrackSelectionParameters(parametersFromReceiver(request))
                .build()
        }

        val candidates = request.trackGroupList.indices.map { index ->
            CastTrackCandidate(
                groupIndex = index,
                type = request.mediaTracks[index].type,
                language = request.trackGroupList[index].getFormat(0).language,
            )
        }
        val overrides = request.trackGroupList.indices.mapNotNull { index ->
            request.trackSelectionParameters.overrides[request.trackGroupList[index]]
                ?.let { index to it.trackIndices.isNotEmpty() }
        }.toMap()
        val selectedIndices = selectCastTrackGroupIndices(
            candidates = candidates,
            currentlySelectedIndices = request.trackGroupList.indices.filterTo(mutableSetOf()) {
                request.trackGroupList[it] in request.currentlySelectedTrackGroups
            },
            explicitOverrides = overrides,
            disabledTrackTypes = request.trackSelectionParameters.disabledTrackTypes,
            preferredLanguages = mapOf(
                C.TRACK_TYPE_AUDIO to request.trackSelectionParameters.preferredAudioLanguages,
                C.TRACK_TYPE_TEXT to request.trackSelectionParameters.preferredTextLanguages,
            ),
        )
        val selected = ImmutableSet.copyOf(selectedIndices.map(request.trackGroupList::get))
        return request.buildResultUpon().setSelections(selected).build()
    }

    private fun parametersFromReceiver(request: CastTrackSelectorRequest): TrackSelectionParameters {
        val candidates = request.trackGroupList.indices.map { index ->
            CastTrackCandidate(
                groupIndex = index,
                type = request.mediaTracks[index].type,
                language = request.trackGroupList[index].getFormat(0).language,
            )
        }
        val parameters = request.trackSelectionParameters.buildUpon()
        val textCandidates = candidates.filter { it.type == MediaTrack.TYPE_TEXT }
        val selectedText = textCandidates.firstOrNull {
            request.trackGroupList[it.groupIndex] in request.currentlySelectedTrackGroups
        }
        if (selectedText != null) {
            parameters
                .setOverrideForType(
                    TrackSelectionOverride(request.trackGroupList[selectedText.groupIndex], ImmutableList.of(0)),
                )
                .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, false)
            selectedText.language?.let(parameters::setPreferredTextLanguage)
        } else if (textCandidates.isNotEmpty()) {
            parameters
                .clearOverridesOfType(C.TRACK_TYPE_TEXT)
                .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, true)
        }
        return parameters.build()
    }
}

internal data class CastTrackCandidate(
    val groupIndex: Int,
    val type: Int,
    val language: String?,
)

internal fun selectCastTrackGroupIndices(
    candidates: List<CastTrackCandidate>,
    currentlySelectedIndices: Set<Int>,
    explicitOverrides: Map<Int, Boolean>,
    disabledTrackTypes: Set<Int>,
    preferredLanguages: Map<Int, List<String>>,
): Set<Int> {
    val result = mutableSetOf<Int>()
    val groupsByType = candidates.groupBy(CastTrackCandidate::type)

    listOf(
        // The configured Google Default Media Receiver supports text tracks;
        // audio switching requires an app-owned custom receiver.
        MediaTrack.TYPE_TEXT to C.TRACK_TYPE_TEXT,
        MediaTrack.TYPE_VIDEO to C.TRACK_TYPE_VIDEO,
    ).forEach { (castType, playerType) ->
        val groups = groupsByType[castType].orEmpty()
        if (groups.isEmpty() || playerType in disabledTrackTypes) return@forEach

        val explicitOverride = groups.firstOrNull { it.groupIndex in explicitOverrides }
        val chosen = if (explicitOverride != null) {
            explicitOverride.takeIf { explicitOverrides.getValue(it.groupIndex) }
        } else {
            preferredCastLanguage(groups, preferredLanguages[playerType].orEmpty())
                ?: groups.firstOrNull { it.groupIndex in currentlySelectedIndices }
        }

        chosen?.let { result += it.groupIndex }
    }
    return result
}

private fun preferredCastLanguage(
    candidates: List<CastTrackCandidate>,
    preferredLanguages: List<String>,
): CastTrackCandidate? = preferredLanguages.firstNotNullOfOrNull { preferred ->
    candidates.firstOrNull { candidate ->
        val language = candidate.language ?: return@firstOrNull false
        language.equals(preferred, ignoreCase = true) ||
            language.substringBefore('-').substringBefore('_')
                .equals(preferred.substringBefore('-').substringBefore('_'), ignoreCase = true)
    }
}
