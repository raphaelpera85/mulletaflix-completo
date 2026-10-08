package org.mulletaflix.feature.itemdetail

import java.util.concurrent.atomic.AtomicLong

/** Invalidates stale asynchronous reader loads and progress writes. */
internal class BookReaderRequestGeneration {
    private val generation = AtomicLong()

    fun begin(): Long = generation.incrementAndGet()

    fun isCurrent(candidate: Long): Boolean = candidate == generation.get()
}
