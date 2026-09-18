package org.mulletaflix.feature.player

import org.mulletaflix.domain.model.Chapter

/**
 * Helper policy for navigating between media chapters.
 */
internal fun findCurrentChapter(
    chapters: List<Chapter>,
    currentPositionMs: Long,
): Chapter? {
    if (chapters.isEmpty()) return null
    return chapters
        .filter { (it.startPositionTicks / 10_000L) <= currentPositionMs }
        .maxByOrNull { it.startPositionTicks }
}

internal fun findNextChapterPosition(
    chapters: List<Chapter>,
    currentPositionMs: Long,
): Long? {
    if (chapters.isEmpty()) return null
    return chapters
        .firstOrNull { (it.startPositionTicks / 10_000L) > (currentPositionMs + 1_000L) }
        ?.let { it.startPositionTicks / 10_000L }
}

internal fun findPreviousChapterPosition(
    chapters: List<Chapter>,
    currentPositionMs: Long,
): Long? {
    if (chapters.isEmpty()) return null
    val currentChapter = findCurrentChapter(chapters, currentPositionMs) ?: return 0L
    val currentStartMs = currentChapter.startPositionTicks / 10_000L

    // If more than 3 seconds into the current chapter, restart the current chapter
    if (currentPositionMs - currentStartMs > 3_000L) {
        return currentStartMs
    }

    // Otherwise, find the chapter immediately preceding the current one
    val previousChapter = chapters
        .filter { (it.startPositionTicks / 10_000L) < currentStartMs }
        .maxByOrNull { it.startPositionTicks }

    return (previousChapter?.startPositionTicks ?: 0L) / 10_000L
}
