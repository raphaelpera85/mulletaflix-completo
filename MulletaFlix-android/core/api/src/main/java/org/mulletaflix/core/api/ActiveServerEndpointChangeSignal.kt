package org.mulletaflix.core.api

import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import javax.inject.Inject
import javax.inject.Singleton

/** Announces an endpoint change confirmed by automatic server recovery. */
@Singleton
class ActiveServerEndpointChangeSignal @Inject constructor() {
    private val mutableChanges = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val changes: SharedFlow<String> = mutableChanges.asSharedFlow()

    suspend fun notifyChanged(serverUrl: String) {
        mutableChanges.emit(serverUrl)
    }
}
