package org.mulletaflix.feature.settings

import org.mulletaflix.domain.model.QUALITY_AUTO
import org.mulletaflix.domain.model.QUALITY_PRESET_CHOICES
import org.mulletaflix.domain.model.normalizePlaybackQualityPreference

/**
 * Quality choices the settings dialog offers.
 *
 * The catalogue itself lives in `:domain` (`PlaybackQuality.kt`) because the player
 * writes into it too: it stores whatever resolution a title really offers, so a 360p
 * track is stored as `360p`. This screen used to know only six presets and answered
 * "Auto" for anything else, which made a real preference invisible here.
 */
internal val defaultQualityChoices: List<String> = listOf(QUALITY_AUTO) + QUALITY_PRESET_CHOICES

/**
 * Keeps a stored quality valid.
 *
 * Delegates to the same rule the player uses, so a value the player can store is a value
 * this screen can show.
 */
internal fun normalizeDefaultQuality(value: String?): String =
    normalizePlaybackQualityPreference(value)
