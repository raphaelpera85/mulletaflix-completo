package org.mulletaflix.feature.itemdetail

/** Invalidates stale asynchronous reader loads and progress writes. */
internal class BookReaderRequestGeneration {
    private var generation = 0L

    fun begin(): Long = ++generation

    fun isCurrent(candidate: Long): Boolean = candidate == generation
}
