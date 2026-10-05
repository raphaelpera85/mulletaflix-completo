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
    fun datastoreIsolatesUsersPreservesRecentLibrariesAndStripsPlaybackData() = runBlocking {
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
            year = 2024,
            officialRating = "14",
            genres = listOf("Drama", "Ficção"),
            imageTags = mapOf(ImageType.Primary to "abc123"),
            mediaSources = listOf(MediaSource(id = "source", path = "D:/private/movie.mkv", transcodeUrl = "http://secret/stream")),
            mediaStreams = listOf(MediaStream(index = 0, type = MediaStreamType.Video, deliveryUrl = "http://secret/track")),
            canDownload = true,
        )
        val series = MediaItem(
            id = "series-456",
            name = "Série salva",
            type = MediaItemType.Series,
            year = 2022,
            premiereDate = "2022-09-01T00:00:00Z",
            endDate = "2025-05-01T00:00:00Z",
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
                items = listOf(media, series),
                totalItemCount = 12,
            )
        }

        assertNull(cache.read("user-1", "library-0")) // only the eight most recently viewed libraries survive
        val restored = checkNotNull(cache.read("user-1", "library-8"))
        assertEquals("Biblioteca 8", restored.libraryName)
        assertEquals(12, restored.totalItemCount)
        assertEquals(2, restored.items.size)
        val restoredMovie = restored.items.first { it.id == media.id }
        val restoredSeries = restored.items.first { it.id == series.id }
        assertEquals("Título salvo", restoredMovie.name)
        assertEquals(2024, restoredMovie.year)
        assertEquals("14", restoredMovie.officialRating)
        assertEquals(listOf("Drama", "Ficção"), restoredMovie.genres)
        assertEquals(mapOf(ImageType.Primary to "abc123"), restoredMovie.imageTags)
        assertTrue(restoredMovie.mediaSources.isEmpty())
        assertTrue(restoredMovie.mediaStreams.isEmpty())
        assertFalse(restoredMovie.canDownload)
        assertEquals("2022-09-01T00:00:00Z", restoredSeries.premiereDate)
        assertEquals("2025-05-01T00:00:00Z", restoredSeries.endDate)

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
