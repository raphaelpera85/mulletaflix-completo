package org.mulletaflix.data.repository

import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.repository.PlaylistRepository
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PlaylistRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : PlaylistRepository {
    override suspend fun getPlaylists(userId: String): Result<List<Playlist>> = suspendRunCatching {
        api.getPlaylists(userId).items.map { Playlist(it.id, it.name.orEmpty()) }
    }

    override suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int): Result<Pair<List<MediaItem>, Int>> = suspendRunCatching {
        val response = api.getPlaylistItems(playlistId, userId, startIndex, limit)
        response.items.map { it.toDomain() } to response.totalRecordCount
    }

    override suspend fun createPlaylist(userId: String, name: String, itemId: String?): Result<Playlist> = suspendRunCatching {
        require(name.isNotBlank()) { "Informe um nome para a playlist." }
        val created = api.createPlaylist(name.trim(), userId, itemId)
        val id = requireNotNull(created.id) { "O servidor não retornou o ID da playlist." }
        Playlist(id, name.trim())
    }

    override suspend fun addItem(userId: String, playlistId: String, itemId: String): Result<Unit> = suspendRunCatching {
        api.addItemToPlaylist(playlistId, itemId, userId)
    }
}
