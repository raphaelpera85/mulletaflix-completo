package org.mulletaflix.feature.player

/**
 * Keeps the active player responsive to a user choice while the persisted
 * preference flow catches up with the same selection.
 */
internal data class PlaybackQualityPreference(
    val quality: String = "Auto",
    private val selectedInCurrentSession: Boolean = false,
) {
    fun select(quality: String): PlaybackQualityPreference = copy(
        quality = normalizeQualityPreference(quality),
        selectedInCurrentSession = true,
    )

    fun applyStoredPreference(quality: String): PlaybackQualityPreference =
        if (selectedInCurrentSession) this else copy(quality = normalizeQualityPreference(quality))

    val shouldApplyMeteredAutoCap: Boolean
        get() = quality == "Auto"
}
