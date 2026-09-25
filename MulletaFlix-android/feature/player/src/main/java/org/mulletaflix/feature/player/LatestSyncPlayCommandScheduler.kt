package org.mulletaflix.feature.player

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

/** Keeps delayed realtime playback commands ordered by arrival. */
internal class LatestSyncPlayCommandScheduler(private val scope: CoroutineScope) {
    private var generation = 0L
    private var pending: Job? = null

    fun schedule(delayMs: Long, action: () -> Unit) {
        pending?.cancel()
        val scheduledGeneration = ++generation
        pending = scope.launch {
            delay(delayMs.coerceAtLeast(0L))
            if (scheduledGeneration != generation) return@launch
            pending = null
            action()
        }
    }

    fun cancelPending() {
        generation += 1
        pending?.cancel()
        pending = null
    }
}
