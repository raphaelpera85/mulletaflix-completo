package org.mulletaflix.domain.usecase

import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.repository.MediaRepository
import java.io.IOException

class GetItemDetailUseCaseTest {

    private open class FakeMediaRepository : MediaRepository {
        var passedUserId: String? = null
        var passedItemId: String? = null
        var itemResult: Result<MediaItem> = Result.success(
            MediaItem(id = "item-1", name = "Test Movie", type = MediaItemType.Movie)
        )

        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> {
            passedUserId = userId
            passedItemId = itemId
            return itemResult
        }

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

    @Test
    fun `retrieves item by id successfully`() = runTest {
        val repo = FakeMediaRepository()
        val useCase = GetItemDetailUseCase(repo)

        val result = useCase("user-1", "item-1")

        assertTrue(result.isSuccess)
        assertEquals("Test Movie", result.getOrNull()?.name)
        assertEquals("user-1", repo.passedUserId)
        assertEquals("item-1", repo.passedItemId)
    }

    @Test
    fun `propagates repository failure when item not found or io error occurs`() = runTest {
        val repo = FakeMediaRepository().apply {
            itemResult = Result.failure(IOException("Item not found"))
        }
        val useCase = GetItemDetailUseCase(repo)

        val result = useCase("user-1", "item-missing")

        assertTrue(result.isFailure)
        assertTrue(result.exceptionOrNull() is IOException)
        assertEquals("Item not found", result.exceptionOrNull()?.message)
    }

    @Test
    fun `trims userId and itemId before passing to repository`() = runTest {
        val repo = FakeMediaRepository()
        val useCase = GetItemDetailUseCase(repo)

        val result = useCase("  user-123  \t", "  \nitem-456  ")

        assertTrue(result.isSuccess)
        assertEquals("user-123", repo.passedUserId)
        assertEquals("item-456", repo.passedItemId)
    }

    @Test
    fun `rejects blank userId with IllegalArgumentException`() = runTest {
        val repo = FakeMediaRepository()
        val useCase = GetItemDetailUseCase(repo)

        val result = useCase("   ", "item-1")

        assertTrue(result.isFailure)
        assertTrue(result.exceptionOrNull() is IllegalArgumentException)
        assertEquals(null, repo.passedUserId)
    }

    @Test
    fun `rejects blank itemId with IllegalArgumentException`() = runTest {
        val repo = FakeMediaRepository()
        val useCase = GetItemDetailUseCase(repo)

        val result = useCase("user-1", "   \t")

        assertTrue(result.isFailure)
        assertTrue(result.exceptionOrNull() is IllegalArgumentException)
        assertEquals(null, repo.passedItemId)
    }
}
