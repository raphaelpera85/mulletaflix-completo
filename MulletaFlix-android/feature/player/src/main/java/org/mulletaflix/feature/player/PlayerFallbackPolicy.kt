package org.mulletaflix.feature.player

/** Allows one direct-play recovery attempt without creating an error loop. */
internal fun shouldFallbackToTranscode(
    currentUri: String?,
    transcodeUri: String?,
    alreadyTried: Boolean,
): Boolean = !alreadyTried &&
    !transcodeUri.isNullOrBlank() &&
    currentUri != transcodeUri

/** Keeps the playback position valid when rebuilding the item with a transcode URL. */
internal fun fallbackPosition(positionMs: Long): Long = positionMs.coerceAtLeast(0L)
