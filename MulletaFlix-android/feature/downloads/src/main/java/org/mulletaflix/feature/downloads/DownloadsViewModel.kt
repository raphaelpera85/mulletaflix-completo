package org.mulletaflix.feature.downloads

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import javax.inject.Inject

@HiltViewModel
class DownloadsViewModel @Inject constructor(
    private val manageDownloadsUseCase: ManageDownloadsUseCase,
) : ViewModel() {
    val downloads: StateFlow<List<DownloadEntry>> = manageDownloadsUseCase.observeDownloads()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())

    private val _queuePaused = MutableStateFlow(false)
    val queuePaused: StateFlow<Boolean> = _queuePaused
    val wifiOnly: StateFlow<Boolean> = manageDownloadsUseCase.observeWifiOnly()
        .stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), false)

    init {
        viewModelScope.launch {
            manageDownloadsUseCase.observeQueuePaused().collect { _queuePaused.value = it }
        }
    }

    fun remove(id: String) {
        manageDownloadsUseCase.remove(id)
    }

    fun retry(entry: DownloadEntry) {
        manageDownloadsUseCase.retry(entry)
    }

    fun pauseQueue() {
        manageDownloadsUseCase.pauseAll().onSuccess { _queuePaused.value = true }
    }

    fun resumeQueue() {
        manageDownloadsUseCase.resumeAll().onSuccess { _queuePaused.value = false }
    }

    fun setWifiOnly(enabled: Boolean) {
        manageDownloadsUseCase.setWifiOnly(enabled)
    }
}
