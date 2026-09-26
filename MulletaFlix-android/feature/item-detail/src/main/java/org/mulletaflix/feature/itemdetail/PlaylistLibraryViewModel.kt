package org.mulletaflix.feature.itemdetail

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.launch
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.usecase.ManagePlaylistUseCase
import javax.inject.Inject

data class PlaylistLibraryState(
    val playlists: List<Playlist> = emptyList(),
    val selectedPlaylist: Playlist? = null,
    val items: List<MediaItem> = emptyList(),
    val totalItems: Int = 0,
    val isLoadingPlaylists: Boolean = false,
    val isLoadingItems: Boolean = false,
    val error: String? = null,
    val itemsError: String? = null,
    val hasMoreItems: Boolean = false,
)

@HiltViewModel
class PlaylistLibraryViewModel @Inject constructor(
    private val managePlaylistUseCase: ManagePlaylistUseCase,
    authRepository: AuthRepository,
) : ViewModel() {
    private val _state = MutableStateFlow(PlaylistLibraryState())
    val state: StateFlow<PlaylistLibraryState> = _state.asStateFlow()
    private var userId: String? = null
    private var playlistsJob: Job? = null
    private var itemsJob: Job? = null
    private var playlistsRequestGeneration = 0L
    private var itemsRequestGeneration = 0L
    private var receivedItemCount = 0
    private var failedItemsStartIndex: Int? = null

    init {
        viewModelScope.launch {
            authRepository.getSavedUserId().distinctUntilChanged().collect { id ->
                if (id == userId) return@collect
                userId = id?.takeIf(String::isNotBlank)
                playlistsRequestGeneration++
                itemsRequestGeneration++
                playlistsJob?.cancel()
                itemsJob?.cancel()
                receivedItemCount = 0
                failedItemsStartIndex = null
                _state.value = PlaylistLibraryState()
                if (userId != null) loadPlaylists()
            }
        }
    }

    fun loadPlaylists() {
        val id = userId ?: return
        val generation = ++playlistsRequestGeneration
        playlistsJob?.cancel()
        playlistsJob = viewModelScope.launch {
            _state.value = _state.value.copy(isLoadingPlaylists = true, error = null)
            managePlaylistUseCase.getPlaylists(id).fold(
                onSuccess = { playlists ->
                    if (generation != playlistsRequestGeneration) return@fold
                    val selected = playlists.firstOrNull { it.id == _state.value.selectedPlaylist?.id }
                        ?: playlists.firstOrNull()
                    _state.value = _state.value.copy(playlists = playlists, isLoadingPlaylists = false)
                    if (selected != null) selectPlaylist(selected)
                    else {
                        receivedItemCount = 0
                        failedItemsStartIndex = null
                        _state.value = _state.value.copy(
                            selectedPlaylist = null,
                            items = emptyList(),
                            totalItems = 0,
                            hasMoreItems = false,
                            itemsError = null,
                        )
                    }
                },
                onFailure = { error ->
                    if (generation == playlistsRequestGeneration) _state.value = _state.value.copy(isLoadingPlaylists = false, error = error.message ?: "Não foi possível carregar as playlists.")
                },
            )
        }
    }

    fun refresh() {
        itemsJob?.cancel()
        itemsRequestGeneration++
        receivedItemCount = 0
        failedItemsStartIndex = null
        _state.value = _state.value.copy(
            items = emptyList(),
            isLoadingItems = false,
            hasMoreItems = false,
            itemsError = null,
        )
        loadPlaylists()
    }

    fun selectPlaylist(playlist: Playlist) {
        if (playlist.id == _state.value.selectedPlaylist?.id && _state.value.items.isNotEmpty()) return
        itemsJob?.cancel()
        receivedItemCount = 0
        failedItemsStartIndex = null
        _state.value = _state.value.copy(
            selectedPlaylist = playlist,
            items = emptyList(),
            totalItems = 0,
            isLoadingItems = true,
            error = null,
            itemsError = null,
            hasMoreItems = false,
        )
        loadItems(playlist, 0, replace = true)
    }

    fun loadNextPage() {
        val playlist = _state.value.selectedPlaylist ?: return
        if (_state.value.isLoadingItems || !_state.value.hasMoreItems) return
        loadItems(playlist, receivedItemCount, replace = false)
    }

    fun retryItems() {
        val startIndex = failedItemsStartIndex ?: 0
        _state.value.selectedPlaylist?.let { loadItems(it, startIndex, replace = startIndex == 0) }
    }

    private fun loadItems(playlist: Playlist, startIndex: Int, replace: Boolean) {
        val id = userId ?: return
        val generation = ++itemsRequestGeneration
        itemsJob?.cancel()
        itemsJob = viewModelScope.launch {
            _state.value = _state.value.copy(isLoadingItems = true, itemsError = null)
            managePlaylistUseCase.getPlaylistItems(id, playlist.id, startIndex, PAGE_SIZE).fold(
                onSuccess = { (items, total) ->
                    if (generation != itemsRequestGeneration || userId != id) return@fold
                    receivedItemCount = startIndex + items.size
                    failedItemsStartIndex = null
                    val oldItems = if (replace) emptyList() else _state.value.items
                    val seen = oldItems.asSequence().map(MediaItem::id).toHashSet()
                    _state.value = _state.value.copy(
                        items = oldItems + items.filter { seen.add(it.id) },
                        totalItems = total,
                        isLoadingItems = false,
                        itemsError = null,
                        hasMoreItems = items.isNotEmpty() && receivedItemCount < total,
                    )
                },
                onFailure = { error ->
                    if (generation == itemsRequestGeneration && userId == id) {
                        failedItemsStartIndex = startIndex
                        _state.value = _state.value.copy(
                            isLoadingItems = false,
                            itemsError = error.message ?: "Não foi possível carregar os títulos.",
                            hasMoreItems = false,
                        )
                    }
                },
            )
        }
    }

    private companion object { const val PAGE_SIZE = 50 }
}
