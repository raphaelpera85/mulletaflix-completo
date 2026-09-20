package org.mulletaflix.feature.settings

internal const val DEFAULT_ASPECT_RATIO = "FIT"

internal val aspectRatioChoices = listOf(
    "FIT" to "Ajustar (Original)",
    "ZOOM" to "Preencher / Zoom",
    "FILL" to "Esticar",
)

internal fun normalizeAspectRatioPreferenceName(value: String?): String =
    aspectRatioChoices.firstOrNull { it.first.equals(value?.trim(), ignoreCase = true) }?.first
        ?: DEFAULT_ASPECT_RATIO

internal fun aspectRatioTitle(value: String?): String =
    aspectRatioChoices.firstOrNull { it.first == normalizeAspectRatioPreferenceName(value) }?.second
        ?: "Ajustar (Original)"
