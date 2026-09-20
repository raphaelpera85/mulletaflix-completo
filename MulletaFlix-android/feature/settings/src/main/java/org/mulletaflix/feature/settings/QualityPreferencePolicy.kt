package org.mulletaflix.feature.settings

/** Quality choices supported by the player's adaptive track constraints. */
internal val defaultQualityChoices: List<String> = listOf("Auto", "4K", "1440p", "1080p", "720p", "480p")

internal fun normalizeDefaultQuality(value: String?): String =
    defaultQualityChoices.firstOrNull { it.equals(value?.trim(), ignoreCase = true) } ?: "Auto"
