package org.mulletaflix.core.common.dispatcher

import javax.inject.Qualifier

@Qualifier
@Retention(AnnotationRetention.RUNTIME)
annotation class IoDispatcher

@Qualifier
@Retention(AnnotationRetention.RUNTIME)
annotation class DefaultDispatcher

@Qualifier
@Retention(AnnotationRetention.RUNTIME)
annotation class MainDispatcher

/**
 * A scope that outlives any ViewModel, for work that must complete while a screen
 * is being torn down.
 *
 * `ViewModel.clear()` cancels `viewModelScope` **before** it calls `onCleared()`,
 * so a coroutine launched there from `onCleared()` never runs its body. The player
 * used that scope to tell the server a playback session had ended, which meant the
 * session was never closed and the server-side transcode was never stopped.
 */
@Qualifier
@Retention(AnnotationRetention.RUNTIME)
annotation class ApplicationScope
