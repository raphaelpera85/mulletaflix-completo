package org.mulletaflix.domain.repository

import org.mulletaflix.domain.model.Playlist
import org.mulletaflix.domain.model.MediaItem

interface PlaylistRepository {
    suspend fun getPlaylists(userId: String): Result<List<Playlist>>
    suspend fun getPlaylistItems(userId: String, playlistId: String, startIndex: Int, limit: Int): Result<Pair<List<MediaItem>, Int>>
    suspend fun createPlaylist(userId: String, name: String, itemId: String? = null): Result<Playlist>
    suspend fun addItem(userId: String, playlistId: String, itemId: String): Result<Unit>
}
