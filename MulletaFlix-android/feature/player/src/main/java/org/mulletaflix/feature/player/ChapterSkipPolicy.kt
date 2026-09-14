package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.Chapter

internal enum class ChapterSkipKind { INTRO, CREDITS }

internal data class ChapterSkipAction(
    val kind: ChapterSkipKind,
    val targetPositionMs: Long,
)

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
