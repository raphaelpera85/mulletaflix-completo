package org.mulletaflix.domain.model

/**
 * Types of media segments detected natively or by Intro Skipper.
 */
enum class MediaSegmentType {
    Unknown,
    Commercial,
    Preview,
    Recap,
    Outro,
    Intro,
}

/**
 * Media segment (e.g. Intro or Credits) with boundary timestamps.
 * 1 tick = 100 nanoseconds; 10_000 ticks = 1 millisecond.
 */
data class MediaSegment(
    val id: String,
    val itemId: String,
    val type: MediaSegmentType,
    val startTicks: Long,
    val endTicks: Long,
) {
    val startPositionMs: Long get() = startTicks / 10_000L
    val endPositionMs: Long get() = endTicks / 10_000L
}
