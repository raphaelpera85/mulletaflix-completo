package org.mulletaflix.feature.syncplay

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.core.api.SessionRepository
import javax.inject.Inject

data class SyncPlayUiState(
    val groups: List<SyncPlayGroup> = emptyList(),
    val isLoading: Boolean = false,
    val isSubmitting: Boolean = false,
    val error: String? = null,
    val activeGroupId: String? = null,
)

@HiltViewModel
class SyncPlayViewModel @Inject constructor(
    private val manageSyncPlayUseCase: ManageSyncPlayUseCase,
    private val sessionRepository: SessionRepository,
) : ViewModel() {

    private val _state = MutableStateFlow(SyncPlayUiState())
    val state: StateFlow<SyncPlayUiState> = _state.asStateFlow()

    private var refreshJob: Job? = null
    private var currentUserId: String? = null
    private var sessionGeneration = 0L
    private var hasObservedSession = false

    init {
        viewModelScope.launch {
            sessionRepository.getCurrentUserId().distinctUntilChanged().collect { userId ->
                val userChanged = hasObservedSession && currentUserId != userId
                currentUserId = userId
                hasObservedSession = true
                if (userChanged) {
                    refreshJob?.cancel()
                    ++sessionGeneration
                    _state.update {
                        it.copy(
                            groups = emptyList(),
                            isLoading = false,
                            isSubmitting = false,
                            error = null,
                            activeGroupId = null,
                        )
                    }
                }
                refresh()
            }
        }
    }

    fun refresh(isBackground: Boolean = false) {
        refreshJob?.cancel()
        refreshJob = viewModelScope.launch {
            val generation = sessionGeneration
            val userId = currentUserId ?: sessionRepository.getCurrentUserId().first()
            if (userId.isNullOrBlank()) {
                if (isCurrentSession(userId, generation)) {
                    _state.update { it.copy(isLoading = false, error = "Sessão expirada. Entre novamente para carregar as salas.") }
                }
                return@launch
            }
            if (!isBackground) {
                _state.update { it.copy(isLoading = true, error = null) }
            }
            manageSyncPlayUseCase.getGroups()
                .onSuccess { groups ->
                    if (isCurrentSession(userId, generation)) {
                        _state.update { it.copy(groups = groups, isLoading = false, error = null) }
                    }
                }
                .onFailure { e ->
                    if (!isBackground && isCurrentSession(userId, generation)) {
                        _state.update { it.copy(isLoading = false, error = e.message ?: "Não foi possível carregar as salas.") }
                    }
                }
        }
    }

    fun createGroup(name: String, onCreated: () -> Unit = {}) {
        // O botão já fica desabilitado enquanto envia, mas isso é lido na composição:
        // dois toques no mesmo frame passam os dois, e o servidor cria duas salas com
        // o mesmo nome. O flag é marcado **antes** de lançar a corrotina, pelo mesmo
        // motivo da guarda do download de atualização em Ajustes — a janela entre o
        // toque e o primeiro `update` é onde o segundo toque entra.
        if (_state.value.isSubmitting) return
        val userId = currentUserId
        val generation = sessionGeneration
        _state.update { it.copy(isSubmitting = true, error = null) }
        viewModelScope.launch {
            manageSyncPlayUseCase.createGroup(name)
                .onSuccess {
                    if (!isCurrentSession(userId, generation)) return@onSuccess
                    _state.update { it.copy(isSubmitting = false) }
                    onCreated()
                    refresh()
                }
                .onFailure { e ->
                    if (isCurrentSession(userId, generation)) {
                        _state.update { it.copy(isSubmitting = false, error = e.message ?: "Não foi possível criar a sala.") }
                    }
                }
        }
    }

    /**
     * Entra na sala no servidor.
     *
     * Não recebe mais um callback com "o que está tocando": o servidor não informa
     * isso em `SyncPlay/List` (ver [SyncPlayGroup]), então o callback só existia
     * para carregar um id que era sempre nulo. Seguir a reprodução do grupo exige
     * o WebSocket do SyncPlay, que este app ainda não tem.
     */
    fun joinGroup(groupId: String) {
        if (_state.value.isSubmitting) return
        val userId = currentUserId
        val generation = sessionGeneration
        _state.update { it.copy(isSubmitting = true, error = null) }
        viewModelScope.launch {
            manageSyncPlayUseCase.joinGroup(groupId)
                .onSuccess {
                    if (!isCurrentSession(userId, generation)) return@onSuccess
                    _state.update { it.copy(isSubmitting = false, activeGroupId = groupId) }
                    refresh()
                }
                .onFailure { e ->
                    if (isCurrentSession(userId, generation)) {
                        _state.update { it.copy(isSubmitting = false, error = e.message ?: "Não foi possível entrar na sala.") }
                    }
                }
        }
    }

    fun leaveGroup() {
        if (_state.value.isSubmitting) return
        val userId = currentUserId
        val generation = sessionGeneration
        _state.update { it.copy(isSubmitting = true, error = null) }
        viewModelScope.launch {
            manageSyncPlayUseCase.leaveGroup()
                .onSuccess {
                    if (!isCurrentSession(userId, generation)) return@onSuccess
                    _state.update { it.copy(isSubmitting = false, activeGroupId = null) }
                    refresh()
                }
                .onFailure { e ->
                    if (isCurrentSession(userId, generation)) {
                        _state.update { it.copy(isSubmitting = false, error = e.message ?: "Não foi possível sair da sala.") }
                    }
                }
        }
    }

    override fun onCleared() {
        refreshJob?.cancel()
        super.onCleared()
    }

    private fun isCurrentSession(userId: String?, generation: Long): Boolean =
        !userId.isNullOrBlank() && currentUserId == userId && sessionGeneration == generation
}
