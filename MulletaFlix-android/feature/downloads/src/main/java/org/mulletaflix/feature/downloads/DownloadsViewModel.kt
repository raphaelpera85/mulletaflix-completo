package org.mulletaflix.feature.downloads

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import javax.inject.Inject

data class DownloadsUiState(
    val entries: List<DownloadEntry> = emptyList(),
    val isLoaded: Boolean = false,
)

@HiltViewModel
class DownloadsViewModel @Inject constructor(
    private val manageDownloadsUseCase: ManageDownloadsUseCase,
) : ViewModel() {
    val downloadsState: StateFlow<DownloadsUiState> = manageDownloadsUseCase.observeDownloads()
        .map { entries -> DownloadsUiState(entries = entries, isLoaded = true) }
        .stateIn(
            viewModelScope,
            SharingStarted.WhileSubscribed(stopTimeoutMillis = 5_000, replayExpirationMillis = 0),
            DownloadsUiState(),
        )

    private val _queuePaused = MutableStateFlow(false)
    val queuePaused: StateFlow<Boolean> = _queuePaused
    private val _actionMessage = MutableStateFlow<String?>(null)
    val actionMessage: StateFlow<String?> = _actionMessage
    val wifiOnly: StateFlow<Boolean> = manageDownloadsUseCase.observeWifiOnly()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), false)

    init {
        viewModelScope.launch {
            manageDownloadsUseCase.observeQueuePaused().collect { _queuePaused.value = it }
        }
    }

    fun remove(id: String) {
        runAction { manageDownloadsUseCase.remove(id) }
    }

    fun removeCompleted() {
        runAction { manageDownloadsUseCase.removeCompleted() }
    }

    fun removeFailed() {
        runAction { manageDownloadsUseCase.removeFailed() }
    }

    fun retry(entry: DownloadEntry) {
        runAction { manageDownloadsUseCase.retry(entry) }
    }

    /** Requeues every failed item in the queue snapshot currently rendered. */
    fun retryFailed(entries: List<DownloadEntry>) {
        failedDownloads(entries).forEach { entry ->
            runAction { manageDownloadsUseCase.retry(entry) }
        }
    }

    fun pauseQueue() {
        runAction { manageDownloadsUseCase.pauseAll() }
            .onSuccess { _queuePaused.value = true }
    }

    fun resumeQueue() {
        runAction { manageDownloadsUseCase.resumeAll() }
            .onSuccess { _queuePaused.value = false }
    }

    fun setWifiOnly(enabled: Boolean) {
        runAction { manageDownloadsUseCase.setWifiOnly(enabled) }
    }

    fun clearActionMessage() {
        _actionMessage.value = null
    }

    private fun runAction(action: () -> Result<Unit>): Result<Unit> = runCatching {
        action().getOrThrow()
    }.onFailure { error ->
        _actionMessage.value = error.localizedMessage?.takeIf(String::isNotBlank)
            ?: "Não foi possível concluir a ação offline."
    }
}
