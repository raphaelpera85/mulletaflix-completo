package org.mulletaflix.feature.settings

internal fun normalizeSubtitleFontSize(size: Int): Int = size.coerceIn(50, 200)
