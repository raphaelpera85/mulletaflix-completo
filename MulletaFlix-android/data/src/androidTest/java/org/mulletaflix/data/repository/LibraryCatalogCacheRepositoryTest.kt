package org.mulletaflix.data.repository

import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.HomeFeedCacheScope
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.ImageType
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.MediaSource
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

class LibraryCatalogCacheRepositoryTest {
    @Test
    fun `datastore isolates users preserves several libraries and strips playback data`() = runBlocking {
        val session = TestSessionRepository()
        val cache = LibraryCatalogCacheRepositoryImpl(
            InstrumentationRegistry.getInstrumentation().targetContext,
            session,
        )
        val media = MediaItem(
            id = "media-123",
            name = "Título salvo",
            type = MediaItemType.Movie,
            overview = "Resumo do título",
            imageTags = mapOf(ImageType.Primary to "abc123"),
            mediaSources = listOf(MediaSource(id = "source", path = "D:/private/movie.mkv", transcodeUrl = "http://secret/stream")),
            mediaStreams = listOf(MediaStream(index = 0, type = MediaStreamType.Video, deliveryUrl = "http://secret/track")),
            canDownload = true,
        )

        repeat(9) { index ->
            cache.write(
                userId = "user-1",
                libraryId = "library-$index",
                libraryName = "Biblioteca $index",
                collectionType = "movies",
                sortBy = "SortName",
                sortOrder = "Descending",
                activeFilters = emptyList(),
                items = listOf(media),
                totalItemCount = 12,
            )
        }

        assertNull(cache.read("user-1", "library-0")) // only the eight most recently viewed libraries survive
        val restored = checkNotNull(cache.read("user-1", "library-8"))
        assertEquals("Biblioteca 8", restored.libraryName)
        assertEquals(12, restored.totalItemCount)
        assertEquals("Título salvo", restored.items.single().name)
        assertEquals(mapOf(ImageType.Primary to "abc123"), restored.items.single().imageTags)
        assertTrue(restored.items.single().mediaSources.isEmpty())
        assertTrue(restored.items.single().mediaStreams.isEmpty())
        assertFalse(restored.items.single().canDownload)

        session.scope.value = HomeFeedCacheScope("server-1", "http://mulletaflix.duckdns.org:8096", "user-2")
        assertNull(cache.read("user-2", "library-8"))
    }
}

private class TestSessionRepository : SessionRepository {
    val scope = MutableStateFlow<HomeFeedCacheScope?>(
        HomeFeedCacheScope("server-1", "http://192.168.1.20:8096", "user-1"),
    )

    override fun getAccessToken(): Flow<String?> = MutableStateFlow("not-used-by-cache")
    override fun getDeviceId(): Flow<String> = MutableStateFlow("device")
    override fun getBaseUrl(): Flow<String> = MutableStateFlow("http://192.168.1.20:8096")
    override fun getCurrentUserId(): Flow<String?> = MutableStateFlow(scope.value?.userId)
    override fun getHomeFeedCacheScope(): Flow<HomeFeedCacheScope?> = scope
    override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
    override suspend fun setBaseUrl(url: String) = Unit
    override suspend fun clearSession() = Unit
}
