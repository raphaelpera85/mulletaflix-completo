package org.mulletaflix.feature.library

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive

/** Keeps TV library content current while the destination is visible and resumed. */
@Composable
internal fun TvRefreshEffect(
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

/** Keeps one failed network refresh from killing every future TV refresh. */
private suspend fun refreshSafely(onRefresh: suspend () -> Unit) {
    try {
        onRefresh()
    } catch (cancelled: CancellationException) {
        throw cancelled
    } catch (_: Throwable) {
        // The ViewModel owns user-facing error state. The scheduler must remain alive.
    }
}
