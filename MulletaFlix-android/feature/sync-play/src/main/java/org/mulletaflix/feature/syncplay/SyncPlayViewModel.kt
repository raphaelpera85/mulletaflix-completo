package org.mulletaflix.feature.syncplay

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayRepository
import javax.inject.Inject

data class SyncPlayUiState(val groups: List<SyncPlayGroup> = emptyList(), val isLoading: Boolean = false, val isSubmitting: Boolean = false, val error: String? = null, val activeGroupId: String? = null)

@HiltViewModel
class SyncPlayViewModel @Inject constructor(private val repository: SyncPlayRepository) : ViewModel() {
    private val _state = MutableStateFlow(SyncPlayUiState())
    val state: StateFlow<SyncPlayUiState> = _state.asStateFlow()
    private var refreshJob: Job? = null
    init { refresh() }
    fun refresh(isBackground: Boolean = false) {
        refreshJob?.cancel()
        refreshJob = viewModelScope.launch {
            if (!isBackground) {
                _state.update { it.copy(isLoading = true, error = null) }
            }
            repository.getGroups()
                .onSuccess { groups -> _state.update { it.copy(groups = groups, isLoading = false, error = null) } }
                .onFailure { e ->
                    if (!isBackground) {
                        _state.update { it.copy(isLoading = false, error = e.message ?: "Não foi possível carregar as salas.") }
                    }
                }
        }
    }
    fun createGroup(name: String, onCreated: () -> Unit = {}) { val cleanName = name.trim(); if (cleanName.isEmpty()) return; viewModelScope.launch { _state.update { it.copy(isSubmitting = true, error = null) }; repository.createGroup(cleanName).onSuccess { _state.update { it.copy(isSubmitting = false) }; onCreated(); refresh() }.onFailure { e -> _state.update { it.copy(isSubmitting = false, error = e.message ?: "Não foi possível criar a sala.") } } } }
    fun joinGroup(groupId: String, onJoined: (SyncPlayGroup?) -> Unit = {}) {
        viewModelScope.launch {
            _state.update { it.copy(isSubmitting = true, error = null) }
            repository.joinGroup(groupId)
                .onSuccess {
                    val joinedGroup = _state.value.groups.firstOrNull { it.groupId == groupId }
                    _state.update { it.copy(isSubmitting = false, activeGroupId = groupId) }
                    onJoined(joinedGroup)
                    refresh()
                }
                .onFailure { e ->
                    _state.update { it.copy(isSubmitting = false, error = e.message ?: "Não foi possível entrar na sala.") }
                }
        }
    }
    fun leaveGroup() { viewModelScope.launch { _state.update { it.copy(isSubmitting = true, error = null) }; repository.leaveGroup().onSuccess { _state.update { it.copy(isSubmitting = false, activeGroupId = null) }; refresh() }.onFailure { e -> _state.update { it.copy(isSubmitting = false, error = e.message ?: "Não foi possível sair da sala.") } } } }

    override fun onCleared() {
        refreshJob?.cancel()
        super.onCleared()
    }
}
