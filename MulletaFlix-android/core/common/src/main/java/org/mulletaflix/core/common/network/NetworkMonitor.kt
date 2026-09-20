package org.mulletaflix.core.common.network

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf

/**
 * Utility for reporting app connectivity status.
 */
interface NetworkMonitor {
    val isOnline: Flow<Boolean>

    /** True when Android reports that the active transport is metered. */
    val isMetered: Flow<Boolean>
        get() = flowOf(false)
}
