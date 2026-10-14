package org.mulletaflix.domain.usecase

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.LibraryFilterOptions
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.MediaRepository
import java.io.IOException

class GetFavoriteItemsUseCaseTest {

    @Test
    fun `returns success with items and total count on successful repository query`() = runTest {
        val favoriteMovie = MediaItem(id = "fav-1", name = "Filme Favorito", type = MediaItemType.Movie)
        val fakeRepo = object : FakeMediaRepository() {
            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                officialRatings: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ): Result<Pair<List<MediaItem>, Int>> {
                assertEquals("user-123", userId)
                assertEquals("SortName", sortBy)
                assertEquals("Ascending", sortOrder)
                assertEquals("IsFavorite", filters)
                assertEquals(true, isFavorite)
                assertEquals(0, startIndex)
                assertEquals(40, limit)
                assertNull(searchTerm)
                return Result.success(listOf(favoriteMovie) to 1)
            }
        }

        val useCase = GetFavoriteItemsUseCase(fakeRepo)
        val result = useCase("user-123")

        assertTrue(result.isSuccess)
        val (items, total) = result.getOrThrow()
        assertEquals(1, total)
        assertEquals(1, items.size)
        assertEquals("fav-1", items.first().id)
    }

    @Test
    fun `propagates failure when repository returns an error`() = runTest {
        val networkError = IOException("Falha de conexão com o servidor")
        val fakeRepo = object : FakeMediaRepository() {
            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                officialRatings: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ): Result<Pair<List<MediaItem>, Int>> {
                return Result.failure(networkError)
            }
        }

        val useCase = GetFavoriteItemsUseCase(fakeRepo)
        val result = useCase("user-123")

        assertTrue(result.isFailure)
        assertEquals("Falha de conexão com o servidor", result.exceptionOrNull()?.message)
    }

    @Test
    fun `trims userId and searchTerm before passing to repository`() = runTest {
        var observedUser: String? = null
        var observedSearch: String? = null
        val fakeRepo = object : FakeMediaRepository() {
            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                officialRatings: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ): Result<Pair<List<MediaItem>, Int>> {
                observedUser = userId
                observedSearch = searchTerm
                return Result.success(emptyList<MediaItem>() to 0)
            }
        }

        val useCase = GetFavoriteItemsUseCase(fakeRepo)
        val result = useCase(
            userId = "  user-trimmed  ",
            searchTerm = "  matrix  ",
        )

        assertTrue(result.isSuccess)
        assertEquals("user-trimmed", observedUser)
        assertEquals("matrix", observedSearch)
    }

    @Test
    fun `converts blank searchTerm to null`() = runTest {
        var observedSearch: String? = "not-null"
        val fakeRepo = object : FakeMediaRepository() {
            override suspend fun getItems(
                userId: String,
                parentId: String?,
                includeItemTypes: String?,
                sortBy: String?,
                sortOrder: String?,
                filters: String?,
                searchTerm: String?,
                startIndex: Int,
                limit: Int,
                genres: String?,
                years: String?,
                officialRatings: String?,
                isPlayed: Boolean?,
                isFavorite: Boolean?,
            ): Result<Pair<List<MediaItem>, Int>> {
                observedSearch = searchTerm
                return Result.success(emptyList<MediaItem>() to 0)
            }
        }

        val useCase = GetFavoriteItemsUseCase(fakeRepo)
        val result = useCase("user-1", searchTerm = "   ")

        assertTrue(result.isSuccess)
        assertNull(observedSearch)
    }

    @Test
    fun `validates parameters and rejects invalid inputs`() = runTest {
        val fakeRepo = object : FakeMediaRepository() {}
        val useCase = GetFavoriteItemsUseCase(fakeRepo)

        val blankUser = useCase("   ")
        assertTrue(blankUser.isFailure)
        assertTrue(blankUser.exceptionOrNull() is IllegalArgumentException)

        val emptyUser = useCase("")
        assertTrue(emptyUser.isFailure)
        assertTrue(emptyUser.exceptionOrNull() is IllegalArgumentException)

        val negativeIndex = useCase("user-1", startIndex = -1)
        assertTrue(negativeIndex.isFailure)
        assertTrue(negativeIndex.exceptionOrNull() is IllegalArgumentException)

        val zeroLimit = useCase("user-1", limit = 0)
        assertTrue(zeroLimit.isFailure)
        assertTrue(zeroLimit.exceptionOrNull() is IllegalArgumentException)

        val negativeLimit = useCase("user-1", limit = -10)
        assertTrue(negativeLimit.isFailure)
        assertTrue(negativeLimit.exceptionOrNull() is IllegalArgumentException)
    }

    private open class FakeMediaRepository : MediaRepository {
        override suspend fun getResumeItems(userId: String, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getNextUp(userId: String, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getLibraries(userId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getItems(
            userId: String,
            parentId: String?,
            includeItemTypes: String?,
            sortBy: String?,
            sortOrder: String?,
            filters: String?,
            searchTerm: String?,
            startIndex: Int,
            limit: Int,
            genres: String?,
            years: String?,
            officialRatings: String?,
            isPlayed: Boolean?,
            isFavorite: Boolean?,
        ): Result<Pair<List<MediaItem>, Int>> = Result.success(Pair(emptyList(), 0))
        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> = Result.failure(NotImplementedError())
        override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getSeasons(userId: String, seriesId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun getSpecialFeatures(userId: String, itemId: String): Result<List<MediaItem>> = Result.success(emptyList())
        override suspend fun markAsPlayed(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun markAsUnplayed(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun markAsFavorite(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun unmarkAsFavorite(userId: String, itemId: String): Result<Unit> = Result.success(Unit)
        override suspend fun getLiveTvChannelPreview(userId: String): Result<List<MediaItem>> = Result.success(emptyList())
    }
}
