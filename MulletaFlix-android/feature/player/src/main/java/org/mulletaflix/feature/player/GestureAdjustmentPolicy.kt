package org.mulletaflix.feature.player

internal fun adjustBrightness(current: Float, delta: Float): Float =
    (current.coerceIn(0.01f, 1f) + delta).coerceIn(0.01f, 1f)

internal fun adjustVolume(current: Int, maximum: Int, delta: Float): Int {
    if (maximum <= 0) return 0
    return (current + (delta * maximum).toInt()).coerceIn(0, maximum)
}
