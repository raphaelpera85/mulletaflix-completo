package org.mulletaflix.feature.livetv

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository
import java.time.Instant
import java.time.temporal.ChronoUnit
import javax.inject.Inject

data class LiveTvUiState(
    val channels: List<MediaItem> = emptyList(),
    val programs: List<MediaItem> = emptyList(),
    val isLoading: Boolean = true,
    val isLoadingGuide: Boolean = false,
    val error: String? = null,
    val guideError: String? = null,
)

@HiltViewModel
class LiveTvViewModel @Inject constructor(
    private val repository: LiveTvRepository,
    private val sessionRepository: SessionRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(LiveTvUiState())
    val state = _state.asStateFlow()

    init { refresh() }

    fun refresh() {
        viewModelScope.launch {
            val userId = sessionRepository.getCurrentUserId().first()
            if (userId.isNullOrBlank()) {
                _state.update { it.copy(isLoading = false, error = "Sessão expirada. Entre novamente para ver a TV ao vivo.") }
                return@launch
            }
            _state.update { it.copy(isLoading = true, error = null) }
            repository.getChannels(userId)
                .onSuccess { channels -> _state.update { it.copy(channels = channels, isLoading = false) } }
                .onFailure { e -> _state.update { it.copy(isLoading = false, error = e.message ?: "Não foi possível carregar os canais.") } }
        }
    }

    fun loadGuide() {
        viewModelScope.launch {
            val ids = _state.value.channels.map { it.id }
            if (ids.isEmpty()) return@launch
            _state.update { it.copy(isLoadingGuide = true, guideError = null) }
            val start = Instant.now()
            repository.getPrograms(ids, start.toString(), start.plus(24, ChronoUnit.HOURS).toString())
                .onSuccess { programs -> _state.update { it.copy(programs = programs, isLoadingGuide = false) } }
                .onFailure { e -> _state.update { it.copy(isLoadingGuide = false, guideError = e.message ?: "Não foi possível carregar o guia.") } }
        }
    }
}
