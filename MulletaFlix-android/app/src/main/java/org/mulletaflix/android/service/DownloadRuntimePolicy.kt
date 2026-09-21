package org.mulletaflix.android.service

/**
 * Keeps Media3 download work off the caller thread without creating an
 * unbounded number of workers on TV boxes or low-memory phones.
 */
internal fun downloadExecutorThreadCount(availableProcessors: Int): Int =
    availableProcessors.coerceIn(2, 4)
