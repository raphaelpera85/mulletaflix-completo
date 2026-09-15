package org.mulletaflix.feature.player

/** Maps horizontal drag distance to a playback displacement. */
internal fun seekDeltaFromHorizontalDrag(
    dragPixels: Float,
    viewportWidthPixels: Float,
    durationMs: Long,
): Long {
    if (viewportWidthPixels <= 0f || durationMs <= 0L) return 0L
    return (dragPixels / viewportWidthPixels * durationMs).toLong()
}
