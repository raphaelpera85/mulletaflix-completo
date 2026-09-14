package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.Playlist

interface PlaylistRepository {
    suspend fun getPlaylists(userId: String): Result<List<Playlist>>
    suspend fun createPlaylist(userId: String, name: String, itemId: String? = null): Result<Playlist>
    suspend fun addItem(userId: String, playlistId: String, itemId: String): Result<Unit>
}
