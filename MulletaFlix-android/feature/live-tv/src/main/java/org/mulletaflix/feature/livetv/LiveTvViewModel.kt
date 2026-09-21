package org.mulletaflix.feature.livetv

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.LiveTvRepository
import org.mulletaflix.domain.usecase.GetLiveTvChannelsUseCase
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
    val isOffline: Boolean = false,
    val error: String? = null,
    val guideError: String? = null,
    val recordingsError: String? = null,
    val schedulingProgramIds: Set<String> = emptySet(),
    val scheduledProgramIds: Set<String> = emptySet(),
)

@HiltViewModel
class LiveTvViewModel @Inject constructor(
    private val getLiveTvChannelsUseCase: GetLiveTvChannelsUseCase,
    private val repository: LiveTvRepository,
    private val sessionRepository: SessionRepository,
    private val networkMonitor: NetworkMonitor,
) : ViewModel() {
    private val _state = MutableStateFlow(LiveTvUiState())
    val state = _state.asStateFlow()
    private var refreshJob: Job? = null
    private var guideJob: Job? = null
    private var refreshGeneration = 0L
    private var guideGeneration = 0L
    private var currentUserId: String? = null
    private var sessionGeneration = 0L
    private var hasObservedSession = false

    init {
        viewModelScope.launch {
            var previousOnline: Boolean? = null
            networkMonitor.isOnline.distinctUntilChanged().collect { online ->
                val recovered = shouldRefreshLiveTvOnNetworkReturn(previousOnline, online)
                previousOnline = online
                _state.update { it.copy(isOffline = !online) }
                if (recovered) refresh()
            }
        }
        viewModelScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedSession && currentUserId != userId
                currentUserId = userId
                hasObservedSession = true
                if (userChanged) {
                    ++sessionGeneration
                    refreshJob?.cancel()
                    guideJob?.cancel()
                    ++guideGeneration
                    _state.update {
                        it.copy(
                            channels = emptyList(),
                            recordings = emptyList(),
                            programs = emptyList(),
                            isLoading = false,
                            isLoadingGuide = false,
                            error = null,
                            guideError = null,
                            recordingsError = null,
                            schedulingProgramIds = emptySet(),
                            scheduledProgramIds = emptySet(),
                        )
                    }
                }
                refresh()
            }
        }
    }

    fun refresh() {
        if (_state.value.isOffline) {
            _state.update {
                it.copy(
                    isLoading = false,
                    error = "Você está offline. Os canais serão atualizados quando a conexão voltar.",
                )
            }
            return
        }
        refreshJob?.cancel()
        val generation = ++refreshGeneration
        val sessionAtRequest = sessionGeneration
        // A new channel snapshot invalidates any guide request based on the
        // previous snapshot, even when the transport ignores cancellation.
        // The invalidated request returns before it can clear its own loading
        // flag (it checks `generation != guideGeneration` first), so the flag
        // has to be cleared here — otherwise the EPG dialog spins forever and
        // the "Guia EPG" action stays disabled.
        guideGeneration++
        guideJob?.cancel()
        _state.update { it.copy(isLoadingGuide = false) }
        refreshJob = viewModelScope.launch {
            val userId = currentUserId ?: sessionRepository.getCurrentUserId().first()
            if (userId.isNullOrBlank()) {
                if (sessionAtRequest == sessionGeneration && currentUserId == userId) {
                    _state.update { it.copy(isLoading = false, error = "Sessão expirada. Entre novamente para ver a TV ao vivo.") }
                }
                return@launch
            }
            if (!isCurrentSession(userId, sessionAtRequest)) return@launch
            _state.update { it.copy(isLoading = true, error = null) }
            getLiveTvChannelsUseCase(userId)
                .onSuccess { guide ->
                    if (generation != refreshGeneration || !isCurrentSession(userId, sessionAtRequest)) return@onSuccess
                    _state.update {
                        it.copy(
                            channels = guide.channels,
                            recordings = guide.recordings,
                            isLoading = false,
                            error = null,
                            recordingsError = null,
                        )
                    }
                }
                .onFailure { e ->
                    if (generation != refreshGeneration || !isCurrentSession(userId, sessionAtRequest)) return@onFailure
                    _state.update {
                        it.copy(
                            isLoading = false,
                            error = e.message ?: "Não foi possível carregar os canais.",
                        )
                    }
                }
        }
    }

    /** Used by the TV foreground timer; manual refresh remains destructive. */
    fun refreshIfIdle() {
        val current = _state.value
        if (!shouldRefreshLiveTvIfIdle(current.isOffline, current.isLoading)) return
        refresh()
    }

    fun loadGuide() {
        if (_state.value.isOffline) {
            _state.update {
                it.copy(
                    isLoadingGuide = false,
                    guideError = "Você está offline. O guia será carregado quando a conexão voltar.",
                )
            }
            return
        }
        guideJob?.cancel()
        val generation = ++guideGeneration
        val sessionAtRequest = sessionGeneration
        val userIdAtRequest = currentUserId
        guideJob = viewModelScope.launch {
            if (!isCurrentSession(userIdAtRequest, sessionAtRequest)) return@launch
            val ids = _state.value.channels.map { it.id }
            if (ids.isEmpty()) return@launch
            _state.update { it.copy(isLoadingGuide = true, guideError = null) }
            val formatter = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", Locale.US).apply {
                timeZone = java.util.TimeZone.getTimeZone("UTC")
            }
            val startMillis = System.currentTimeMillis()
            val endMillis = startMillis + TimeUnit.HOURS.toMillis(24)
            repository.getPrograms(ids, formatter.format(Date(startMillis)), formatter.format(Date(endMillis)))
                .onSuccess { programs ->
                    if (generation != guideGeneration || !isCurrentSession(userIdAtRequest, sessionAtRequest)) return@onSuccess
                    _state.update { it.copy(programs = programs, isLoadingGuide = false) }
                }
                .onFailure { e ->
                    if (generation != guideGeneration || !isCurrentSession(userIdAtRequest, sessionAtRequest)) return@onFailure
                    _state.update { it.copy(isLoadingGuide = false, guideError = e.message ?: "Não foi possível carregar o guia.") }
                }
        }
    }

    fun scheduleRecording(program: MediaItem) {
        if (_state.value.isOffline) {
            _state.update {
                it.copy(guideError = "Você está offline. Reconecte-se para agendar uma gravação.")
            }
            return
        }
        if (program.id in _state.value.scheduledProgramIds ||
            program.id in _state.value.schedulingProgramIds
        ) return
        // Update synchronously so repeated taps are rejected before the
        // coroutine gets a chance to start the network request.
        _state.update {
            it.copy(
                schedulingProgramIds = it.schedulingProgramIds + program.id,
                guideError = null,
            )
        }
        val sessionAtRequest = sessionGeneration
        val userIdAtRequest = currentUserId
        // Keep each program request independent: cancelling a different
        // program could leave a server-created timer unrepresented locally.
        viewModelScope.launch {
            repository.scheduleRecording(program)
                .onSuccess {
                    if (!isCurrentSession(userIdAtRequest, sessionAtRequest)) return@onSuccess
                    _state.update {
                        it.copy(
                            schedulingProgramIds = it.schedulingProgramIds - program.id,
                            scheduledProgramIds = it.scheduledProgramIds + program.id,
                        )
                    }
                }
                .onFailure { error ->
                    if (!isCurrentSession(userIdAtRequest, sessionAtRequest)) return@onFailure
                    _state.update {
                        it.copy(
                            schedulingProgramIds = it.schedulingProgramIds - program.id,
                            guideError = error.message ?: "Não foi possível agendar a gravação.",
                        )
                    }
                }
        }
    }

    private fun isCurrentSession(userId: String?, generation: Long): Boolean =
        !userId.isNullOrBlank() &&
            currentUserId == userId &&
            sessionGeneration == generation
}
