package org.mulletaflix.feature.syncplay

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackRepository
import org.mulletaflix.domain.repository.RemotePlaybackSession
import javax.inject.Inject

data class RemotePlaybackUiState(
    val sessions: List<RemotePlaybackSession> = emptyList(),
    val isLoading: Boolean = false,
    val busySessionId: String? = null,
    val error: String? = null,
    val notice: String? = null,
)

@HiltViewModel
class RemotePlaybackViewModel @Inject constructor(
    private val repository: RemotePlaybackRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(RemotePlaybackUiState())
    val state: StateFlow<RemotePlaybackUiState> = _state.asStateFlow()
    private var refreshQueued = false

    fun refresh(queueIfLoading: Boolean = false) {
        if (_state.value.isLoading) {
            if (queueIfLoading) refreshQueued = true
            return
        }
        _state.update { it.copy(isLoading = true, error = null) }
        viewModelScope.launch {
            repository.getActiveSessions()
                .onSuccess { sessions ->
                    _state.update { it.copy(sessions = sessions, isLoading = false, error = null) }
                }
                .onFailure { error ->
                    _state.update {
                        it.copy(
                            // A failed refresh cannot confirm that previously shown
                            // sessions are still active. Hide their controls until a
                            // successful response supplies a fresh snapshot.
                            sessions = emptyList(),
                            isLoading = false,
                            error = error.message?.takeIf(String::isNotBlank)
                                ?: "Não foi possível localizar reproduções ativas.",
                        )
                    }
                }
            if (refreshQueued) {
                refreshQueued = false
                refresh()
            }
        }
    }

    fun sendCommand(
        sessionId: String,
        command: RemotePlaybackCommand,
        seekPositionTicks: Long? = null,
    ) {
        if (_state.value.busySessionId != null || sessionId.isBlank()) return
        _state.update { it.copy(busySessionId = sessionId, error = null, notice = null) }
        viewModelScope.launch {
            repository.sendCommand(sessionId, command, seekPositionTicks)
                .onSuccess {
                    _state.update { it.copy(busySessionId = null, notice = "Comando enviado ao dispositivo.") }
                    refresh(queueIfLoading = true)
                }
                .onFailure { error ->
                    _state.update {
                        it.copy(
                            busySessionId = null,
                            error = error.message?.takeIf(String::isNotBlank)
                                ?: "O dispositivo não confirmou o comando. Atualize e tente novamente.",
                        )
                    }
                }
        }
    }
}
