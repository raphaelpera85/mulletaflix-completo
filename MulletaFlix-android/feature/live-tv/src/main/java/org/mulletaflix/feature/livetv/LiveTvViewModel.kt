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
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.concurrent.TimeUnit
import javax.inject.Inject

data class LiveTvUiState(
    val channels: List<MediaItem> = emptyList(),
    val recordings: List<MediaItem> = emptyList(),
    val programs: List<MediaItem> = emptyList(),
    val isLoading: Boolean = true,
    val isLoadingGuide: Boolean = false,
    val error: String? = null,
    val guideError: String? = null,
    val recordingsError: String? = null,
    val schedulingProgramId: String? = null,
    val scheduledProgramIds: Set<String> = emptySet(),
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
            repository.getRecordings(userId)
                .onSuccess { recordings -> _state.update { it.copy(recordings = recordings, recordingsError = null) } }
                .onFailure { e -> _state.update { it.copy(recordingsError = e.message ?: "Não foi possível carregar as gravações.") } }
        }
    }

    fun loadGuide() {
        viewModelScope.launch {
            val ids = _state.value.channels.map { it.id }
            if (ids.isEmpty()) return@launch
            _state.update { it.copy(isLoadingGuide = true, guideError = null) }
            val formatter = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply {
                timeZone = java.util.TimeZone.getTimeZone("UTC")
            }
            val startMillis = System.currentTimeMillis()
            val endMillis = startMillis + TimeUnit.HOURS.toMillis(24)
            repository.getPrograms(ids, formatter.format(Date(startMillis)), formatter.format(Date(endMillis)))
                .onSuccess { programs -> _state.update { it.copy(programs = programs, isLoadingGuide = false) } }
                .onFailure { e -> _state.update { it.copy(isLoadingGuide = false, guideError = e.message ?: "Não foi possível carregar o guia.") } }
        }
    }

    fun scheduleRecording(program: MediaItem) {
        if (program.id in _state.value.scheduledProgramIds) return
        viewModelScope.launch {
            _state.update { it.copy(schedulingProgramId = program.id, guideError = null) }
            repository.scheduleRecording(program)
                .onSuccess {
                    _state.update {
                        it.copy(
                            schedulingProgramId = null,
                            scheduledProgramIds = it.scheduledProgramIds + program.id,
                        )
                    }
                }
                .onFailure { error ->
                    _state.update {
                        it.copy(
                            schedulingProgramId = null,
                            guideError = error.message ?: "Não foi possível agendar a gravação.",
                        )
                    }
                }
        }
    }
}
