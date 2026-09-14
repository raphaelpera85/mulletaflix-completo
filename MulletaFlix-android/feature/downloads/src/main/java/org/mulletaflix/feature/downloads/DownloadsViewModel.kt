package org.mulletaflix.feature.downloads

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.stateIn
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import javax.inject.Inject

@HiltViewModel
class DownloadsViewModel @Inject constructor(repository: DownloadRepository) : ViewModel() {
    val downloads: StateFlow<List<DownloadEntry>> = repository.observeDownloads().stateIn(viewModelScope, SharingStarted.WhileSubscribed(5_000), emptyList())
    private val downloadRepository = repository
    fun remove(id: String) { downloadRepository.remove(id) }
}
