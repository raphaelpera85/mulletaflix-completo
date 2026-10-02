package org.mulletaflix.feature.player

/** Measurements observed by this device during one local Media3 playback session. */
data class PlaybackSessionMetrics(
    val firstVideoFrameMs: Long? = null,
    val bufferingEpisodes: Int = 0,
    val bufferingDurationMs: Long = 0L,
    val droppedVideoFrames: Int = 0,
    val activeVideoFormat: String? = null,
    val isBuffering: Boolean = false,
)

/** Small monotonic-clock tracker kept local to the current player session. */
internal class PlaybackSessionDiagnostics {
    private var preparedAtMs: Long? = null
    private var firstVideoFrameMs: Long? = null
    private var bufferingEpisodes = 0
    private var completedBufferingDurationMs = 0L
    private var bufferingStartedAtMs: Long? = null
    private var firstFrameRendered = false
    private var playerBuffering = false
    private var playWhenReady = false
    private var droppedVideoFrames = 0
    private var activeVideoFormat: String? = null

    fun reset() {
        preparedAtMs = null
        firstVideoFrameMs = null
        bufferingEpisodes = 0
        completedBufferingDurationMs = 0L
        bufferingStartedAtMs = null
        firstFrameRendered = false
        playerBuffering = false
        playWhenReady = false
        droppedVideoFrames = 0
        activeVideoFormat = null
    }

    fun onPrepared(nowMs: Long) {
        preparedAtMs = nowMs
    }

    fun onPlaybackStateChanged(isBuffering: Boolean, nowMs: Long) {
        playerBuffering = isBuffering
        updateBufferingInterval(nowMs)
    }

    fun onPlayWhenReadyChanged(value: Boolean, nowMs: Long) {
        playWhenReady = value
        updateBufferingInterval(nowMs)
    }

    fun onFirstVideoFrameRendered(renderTimeMs: Long) {
        if (firstFrameRendered) return
        firstFrameRendered = true
        firstVideoFrameMs = preparedAtMs?.let { (renderTimeMs - it).coerceAtLeast(0L) }
    }

    fun onDroppedVideoFrames(count: Int) {
        if (count > 0) droppedVideoFrames += count
    }

    fun onVideoFormatChanged(width: Int, height: Int, codecs: String?, bitrate: Long) {
        activeVideoFormat = buildList {
            if (width > 0 && height > 0) add("${width}x$height")
            codecs?.trim()?.takeIf(String::isNotEmpty)?.let(::add)
            if (bitrate > 0L) add("${bitrate / 1000} kbps")
        }.takeIf { it.isNotEmpty() }?.joinToString(" · ")
    }

    fun snapshot(nowMs: Long): PlaybackSessionMetrics {
        val activeDuration = bufferingStartedAtMs?.let { (nowMs - it).coerceAtLeast(0L) } ?: 0L
        return PlaybackSessionMetrics(
            firstVideoFrameMs = firstVideoFrameMs,
            bufferingEpisodes = bufferingEpisodes,
            bufferingDurationMs = completedBufferingDurationMs + activeDuration,
            droppedVideoFrames = droppedVideoFrames,
            activeVideoFormat = activeVideoFormat,
            isBuffering = bufferingStartedAtMs != null,
        )
    }

    private fun updateBufferingInterval(nowMs: Long) {
        val shouldCountBuffering = playerBuffering && playWhenReady
        if (shouldCountBuffering && bufferingStartedAtMs == null) {
            bufferingStartedAtMs = nowMs
            if (firstFrameRendered) bufferingEpisodes++
        } else if (!shouldCountBuffering && bufferingStartedAtMs != null) {
            completedBufferingDurationMs += (nowMs - bufferingStartedAtMs!!).coerceAtLeast(0L)
            bufferingStartedAtMs = null
        }
    }
}

internal fun formatPlaybackDuration(milliseconds: Long): String =
    if (milliseconds < 1_000L) "$milliseconds ms" else "${milliseconds / 1_000}.${(milliseconds % 1_000) / 100} s"
