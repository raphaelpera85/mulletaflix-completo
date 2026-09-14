package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.repository.PlaylistRepository
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PlaylistRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : PlaylistRepository {
    override suspend fun getPlaylists(userId: String): Result<List<Playlist>> = runCatching {
        api.getPlaylists(userId).items.map { Playlist(it.id, it.name.orEmpty()) }
    }

    override suspend fun createPlaylist(userId: String, name: String, itemId: String?): Result<Playlist> = runCatching {
        require(name.isNotBlank()) { "Informe um nome para a playlist." }
        val created = api.createPlaylist(name.trim(), userId, itemId)
        val id = requireNotNull(created.id) { "O servidor não retornou o ID da playlist." }
        Playlist(id, name.trim())
    }

    override suspend fun addItem(userId: String, playlistId: String, itemId: String): Result<Unit> = runCatching {
        api.addItemToPlaylist(playlistId, itemId, userId)
    }
}
