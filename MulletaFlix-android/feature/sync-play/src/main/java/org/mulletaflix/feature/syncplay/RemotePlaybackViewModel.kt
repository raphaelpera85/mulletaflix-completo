package org.mulletaflix.feature.syncplay

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.domain.repository.RemotePlaybackCommand
import org.mulletaflix.domain.repository.RemotePlaybackIdentity
import org.mulletaflix.domain.repository.RemotePlaybackRepository
import org.mulletaflix.domain.repository.RemotePlaybackSession
import javax.inject.Inject

data class RemotePlaybackUiState(
    val sessions: List<RemotePlaybackSession> = emptyList(),
    val identity: RemotePlaybackIdentity? = null,
    val isLoading: Boolean = false,
    val busySessionId: String? = null,
    val error: String? = null,
    val notice: String? = null,
)

@HiltViewModel
class RemotePlaybackViewModel @Inject constructor(
    private val repository: RemotePlaybackRepository,
    private val sessionRepository: SessionRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(RemotePlaybackUiState())
    val state: StateFlow<RemotePlaybackUiState> = _state.asStateFlow()
    private var refreshQueued = false
    private var activeSession: FeedbackRequestSession? = null
    private var activeIdentity: RemotePlaybackIdentity? = null
    private var hasObservedSession = false
    private var operationGeneration = 0L
    private var refreshJob: Job? = null
    private var commandJob: Job? = null

    init {
        viewModelScope.launch {
            sessionRepository.getFeedbackRequestSession().collect { session ->
                updateSession(session, currentJob = null, refreshOnChange = true)
            }
        }
    }

    fun refresh(queueIfLoading: Boolean = false) {
        if (_state.value.isLoading) {
            if (queueIfLoading) refreshQueued = true
            return
        }
        _state.update { it.copy(isLoading = true, error = null) }
        refreshJob = viewModelScope.launch {
            val currentJob = currentCoroutineContext()[Job]
            val requestSession = sessionRepository.getFeedbackRequestSession().first()
            updateSession(requestSession, currentJob, refreshOnChange = false)
            val identity = activeIdentity
            if (requestSession == null || identity == null) {
                _state.update {
                    it.copy(
                        sessions = emptyList(),
                        isLoading = false,
                        error = "Conecte-se ao servidor para ver dispositivos em reprodução.",
                    )
                }
                return@launch
            }
            val requestGeneration = operationGeneration
            _state.update { it.copy(isLoading = true, error = null) }
            repository.getActiveSessions(identity)
                .onSuccess { sessions ->
                    if (requestGeneration == operationGeneration) {
                        _state.update { it.copy(sessions = sessions, isLoading = false, error = null) }
                    }
                }
                .onFailure { error ->
                    if (requestGeneration == operationGeneration) {
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
                }
            if (requestGeneration == operationGeneration && refreshQueued) {
                refreshQueued = false
                refresh()
            }
        }
    }

    fun sendCommand(
        expectedIdentity: RemotePlaybackIdentity,
        sessionId: String,
        command: RemotePlaybackCommand,
        seekPositionTicks: Long? = null,
    ) {
        if (activeSession == null) return
        val identity = activeIdentity ?: return
        if (!identity.matches(expectedIdentity) || _state.value.sessions.none { it.id == sessionId }) return
        if (_state.value.busySessionId != null || sessionId.isBlank()) return
        val requestGeneration = operationGeneration
        _state.update { it.copy(busySessionId = sessionId, error = null, notice = null) }
        commandJob = viewModelScope.launch {
            repository.sendCommand(identity, sessionId, command, seekPositionTicks)
                .onSuccess {
                    if (requestGeneration == operationGeneration) {
                        _state.update { it.copy(busySessionId = null, notice = "Comando enviado ao dispositivo.") }
                        refresh(queueIfLoading = true)
                    }
                }
                .onFailure { error ->
                    if (requestGeneration == operationGeneration) {
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

    private fun updateSession(
        session: FeedbackRequestSession?,
        currentJob: Job?,
        refreshOnChange: Boolean,
    ) {
        val newIdentity = session?.let { RemotePlaybackIdentity(it.serverId, it.serverUrl, it.userId) }
        val previousIdentity = activeIdentity
        val scopeChanged = hasObservedSession && when {
            previousIdentity == null || newIdentity == null -> previousIdentity != newIdentity
            else -> !previousIdentity.matches(newIdentity)
        }
        activeSession = session
        activeIdentity = newIdentity
        hasObservedSession = true

        if (scopeChanged) {
            operationGeneration += 1
            refreshQueued = false
            refreshJob?.takeIf { it != currentJob }?.cancel()
            commandJob?.takeIf { it != currentJob }?.cancel()
            _state.value = RemotePlaybackUiState(
                identity = newIdentity,
                error = if (session == null) {
                    "Conecte-se ao servidor para ver dispositivos em reprodução."
                } else {
                    null
                },
            )
            if (refreshOnChange && session != null) refresh()
        } else {
            _state.update { it.copy(identity = newIdentity) }
        }
    }
}
