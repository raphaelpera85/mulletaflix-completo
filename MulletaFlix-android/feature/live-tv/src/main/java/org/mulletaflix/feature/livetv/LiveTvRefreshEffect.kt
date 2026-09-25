package org.mulletaflix.feature.livetv

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive

/** Runs a foreground refresh without letting one transient failure kill the timer. */
@Composable
internal fun LiveTvRefreshEffect(
    lifecycleOwner: LifecycleOwner,
    refreshIntervalMillis: Long,
    refreshImmediately: Boolean,
    onRefresh: suspend () -> Unit,
) {
    LaunchedEffect(lifecycleOwner, refreshIntervalMillis, refreshImmediately) {
        if (refreshIntervalMillis <= 0L) return@LaunchedEffect
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.RESUMED) {
            if (refreshImmediately) refreshSafely(onRefresh)
            while (isActive) {
                delay(refreshIntervalMillis)
                refreshSafely(onRefresh)
            }
        }
    }
}

private suspend fun refreshSafely(onRefresh: suspend () -> Unit) {
    try {
        onRefresh()
    } catch (cancelled: CancellationException) {
        throw cancelled
    } catch (_: Throwable) {
        // The ViewModel owns the visible error state; keep the scheduler alive.
    }
}
