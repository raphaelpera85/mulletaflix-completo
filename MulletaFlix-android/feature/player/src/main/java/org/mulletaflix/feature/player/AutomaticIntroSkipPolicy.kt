package org.mulletaflix.feature.player

/** Returns an intro target only for active, seekable playback with auto-skip enabled. */
internal fun automaticIntroSkipTarget(
    enabled: Boolean,
    isPlaying: Boolean,
    isSeekable: Boolean,
    positionMs: Long,
    action: ChapterSkipAction?,
    pendingTargetMs: Long?,
): Long? {
    if (!enabled || !isPlaying || !isSeekable || action?.kind != ChapterSkipKind.INTRO) return null
    return action.targetPositionMs.takeIf { it > positionMs && it != pendingTargetMs }
}
