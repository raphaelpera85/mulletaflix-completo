package org.mulletaflix.android.update

import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive

/** TV checks periodically only while resumed; phones keep their resume-only check. */
@Composable
internal fun AppUpdateCheckEffect(
    lifecycleOwner: LifecycleOwner,
    intervalMillis: Long,
    checkForUpdate: () -> Unit,
) {
    LaunchedEffect(lifecycleOwner, intervalMillis) {
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.RESUMED) {
            checkSafely(checkForUpdate)
            if (intervalMillis <= 0L) return@repeatOnLifecycle
            while (isActive) {
                delay(intervalMillis)
                checkSafely(checkForUpdate)
            }
        }
    }
}

private fun checkSafely(check: () -> Unit) {
    try {
        check()
    } catch (cancelled: CancellationException) {
        throw cancelled
    } catch (_: Throwable) {
        // A transient failure must not terminate future foreground checks.
    }
}

internal const val TV_APP_UPDATE_CHECK_INTERVAL_MILLIS = 60L * 60L * 1000L

internal fun appUpdateCheckIntervalMillis(isTelevision: Boolean): Long =
    if (isTelevision) TV_APP_UPDATE_CHECK_INTERVAL_MILLIS else 0L
