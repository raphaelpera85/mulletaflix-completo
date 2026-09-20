package org.mulletaflix.feature.player

/** Keeps a manual retry from jumping back to the beginning after a stream error. */
internal fun playbackRetryPosition(currentPositionMs: Long, errorPositionMs: Long): Long =
    maxOf(0L, currentPositionMs, errorPositionMs)
