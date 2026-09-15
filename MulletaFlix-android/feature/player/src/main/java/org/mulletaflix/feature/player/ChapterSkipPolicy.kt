package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.Chapter
import org.mulletaflix.domain.model.MediaSegment
import org.mulletaflix.domain.model.MediaSegmentType

internal enum class ChapterSkipKind { INTRO, CREDITS }

internal data class ChapterSkipAction(
    val kind: ChapterSkipKind,
    val targetPositionMs: Long,
)

/** Returns a skip target if playback is inside an Intro Skipper media segment. */
internal fun mediaSegmentSkipAction(segments: List<MediaSegment>, positionMs: Long): ChapterSkipAction? {
    val activeSegment = segments.firstOrNull { segment ->
        positionMs in segment.startPositionMs until segment.endPositionMs
    } ?: return null

    return when (activeSegment.type) {
        MediaSegmentType.Intro -> ChapterSkipAction(kind = ChapterSkipKind.INTRO, targetPositionMs = activeSegment.endPositionMs)
        MediaSegmentType.Outro -> ChapterSkipAction(kind = ChapterSkipKind.CREDITS, targetPositionMs = activeSegment.endPositionMs)
        else -> null
    }
}

/** Returns a skip target using media segments first, falling back to chapter tags. */
internal fun skipAction(
    segments: List<MediaSegment>,
    chapters: List<Chapter>,
    positionMs: Long
): ChapterSkipAction? {
    return mediaSegmentSkipAction(segments, positionMs) ?: chapterSkipAction(chapters, positionMs)
}

/** Returns a skip target only while playback is inside a named intro/credits chapter. */
internal fun chapterSkipAction(chapters: List<Chapter>, positionMs: Long): ChapterSkipAction? {
    val ordered = chapters.sortedBy { it.startPositionTicks }
    val currentIndex = ordered.indexOfLast { it.startPositionTicks / 10_000L <= positionMs }
    if (currentIndex < 0) return null

    val current = ordered[currentIndex]
    val name = current.name.orEmpty()
    val kind = when {
        INTRO_NAMES.any { name.contains(it, ignoreCase = true) } -> ChapterSkipKind.INTRO
        CREDITS_NAMES.any { name.contains(it, ignoreCase = true) } -> ChapterSkipKind.CREDITS
        else -> return null
    }
    val nextStart = ordered.getOrNull(currentIndex + 1)?.startPositionTicks?.div(10_000L)
        ?: return null
    if (nextStart <= positionMs) return null
    return ChapterSkipAction(kind = kind, targetPositionMs = nextStart)
}

private val INTRO_NAMES = setOf("intro", "opening", "abertura")
private val CREDITS_NAMES = setOf("credits", "creditos", "créditos", "ending", "encerramento")
