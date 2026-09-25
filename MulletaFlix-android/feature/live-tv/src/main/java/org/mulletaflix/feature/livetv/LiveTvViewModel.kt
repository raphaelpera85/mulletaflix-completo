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

    /**
     * True while the EPG dialog is on screen.
     *
     * `refresh()` invalidates any guide request built on the previous channel
     * snapshot, which is correct — but it also clears `isLoadingGuide`, so a
     * guide that was still loading when a refresh landed was left with an empty
     * programme list and no request behind it. The dialog then read "Nenhum
     * programa encontrado para as próximas 24 horas." even though the guide
     * worked, and only closing and reopening it recovered. Knowing the dialog is
     * open lets the refresh reload the guide instead.
     */
    private var guideRequested = false

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
                    val channelSetChanged = guide.channels.map { it.id } != _state.value.channels.map { it.id }
                    _state.update {
                        it.copy(
                            channels = guide.channels,
                            recordings = guide.recordings,
                            // A guide built from a different set of channels describes
                            // channels that are no longer on screen. Keeping it would let
                            // the viewer schedule a programme from the previous snapshot
                            // when the reload fails.
                            programs = if (channelSetChanged) emptyList() else it.programs,
                            isLoading = false,
                            error = null,
                            recordingsError = guide.recordingsError,
                        )
                    }
                    // The guide is fetched separately and must never make the
                    // channel list wait, so it is a second call in the same job.
                    reconcileScheduledRecordings(generation, userId, sessionAtRequest)
                    // The channel snapshot just changed, so whatever the dialog
                    // was showing is stale. Reloading it keeps an open guide from
                    // being stranded empty (see `guideRequested`).
                    if (guideRequested) loadGuide()
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

    /**
     * Marks every programme the server already has a pending recording for.
     *
     * The set is unioned with what this session already knows rather than
     * replaced: a timer created moments ago may not be visible in this response
     * yet, and losing the mark would offer "Gravar" again for a programme that is
     * already set to record — which is how a second timer used to be created. A
     * failure is ignored on purpose: not knowing what is scheduled must not break
     * the channel list.
     */
    private suspend fun reconcileScheduledRecordings(
        generation: Long,
        userId: String,
        sessionAtRequest: Long,
    ) {
        val scheduled = repository.getScheduledProgramIds().getOrNull() ?: return
        if (generation != refreshGeneration || !isCurrentSession(userId, sessionAtRequest)) return
        _state.update { it.copy(scheduledProgramIds = it.scheduledProgramIds + scheduled) }
    }
    /** Used by the TV foreground timer; manual refresh remains destructive. */
    fun refreshIfIdle() {
        val current = _state.value
        // The initial session collector starts the request before the first
        // `isLoading` state emission is dispatched. Treat the Job as the
        // source of truth too, otherwise the TV foreground timer can cancel
        // the first channel load and leave the screen with stale/empty data.
        if (refreshJob?.isActive == true ||
            !shouldRefreshLiveTvIfIdle(current.isOffline, current.isLoading)
        ) return
        refresh()
    }

    /** Marks the EPG dialog as open so a later refresh reloads it. */
    fun loadGuide() {
        guideRequested = true
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
            // The window is an *overlap* window: the server filters by end date on one
            // side and start date on the other, so a film that began before "now" and is
            // still on the air is part of the guide. Asking for programmes that *start*
            // after now left exactly the one being watched out of the list.
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

    /** Marks the EPG dialog as closed, so refreshes stop reloading it. */
    fun closeGuide() {
        guideRequested = false
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
