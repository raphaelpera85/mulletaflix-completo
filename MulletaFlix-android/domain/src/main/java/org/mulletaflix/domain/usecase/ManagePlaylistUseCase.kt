package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.PlaylistRepository
import javax.inject.Inject

/**
 * UseCase to handle user playlist interactions (fetching, creating, adding media).
 */
class ManagePlaylistUseCase @Inject constructor(
    private val playlistRepository: PlaylistRepository,
) {
    suspend fun getPlaylists(userId: String): Result<List<Playlist>> {
        if (userId.isBlank()) {
            return Result.failure(IllegalArgumentException("O identificador do usuário é obrigatório."))
        }
        return playlistRepository.getPlaylists(userId)
    }

    suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int = 0, limit: Int = 50): Result<Pair<List<MediaItem>, Int>> {
        if (userId.isBlank() || playlistId.isBlank() || startIndex < 0 || limit !in 1..200) {
            return Result.failure(IllegalArgumentException("Parâmetros inválidos para carregar a playlist."))
        }
        return playlistRepository.getPlaylistItems(userId, playlistId, startIndex, limit)
    }

    suspend fun createPlaylist(userId: String, name: String, itemId: String? = null): Result<Playlist> {
        val cleanName = name.trim()
        if (cleanName.isBlank()) {
            return Result.failure(IllegalArgumentException("O nome da playlist não pode estar vazio."))
        }
        return playlistRepository.createPlaylist(userId, cleanName, itemId)
    }

    suspend fun addToPlaylist(userId: String, playlistId: String, itemId: String): Result<Unit> {
        if (userId.isBlank() || playlistId.isBlank() || itemId.isBlank()) {
            return Result.failure(IllegalArgumentException("Parâmetros inválidos para adicionar à playlist."))
        }
        return playlistRepository.addItem(userId, playlistId, itemId)
    }
}
