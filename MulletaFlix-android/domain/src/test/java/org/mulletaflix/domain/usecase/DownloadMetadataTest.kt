package org.mulletaflix.domain.usecase

import kotlinx.coroutines.flow.emptyFlow
import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository

class DownloadMetadataTest {
    @Test
    fun `enqueue with metadata delegates cover reference without changing download contract`() {
        var receivedImage: String? = null
        val repository = object : DownloadRepository {
            override fun observeDownloads() = emptyFlow<List<DownloadEntry>>()
            override fun enqueue(id: String, title: String, uri: String) = Result.success(Unit)
            override fun enqueueWithMetadata(id: String, title: String, uri: String, imageUrl: String?): Result<Unit> {
                receivedImage = imageUrl
                return Result.success(Unit)
            }
            override fun retry(id: String, title: String, uri: String) = Result.success(Unit)
            override fun remove(id: String) = Result.success(Unit)
            override fun pauseAll() = Result.success(Unit)
            override fun resumeAll() = Result.success(Unit)
        }

        ManageDownloadsUseCase(repository).enqueueWithMetadata(
            "movie-1",
            "Filme",
            "https://server/media/movie-1",
            "Items/movie-1/Images/Primary?tag=cover-1",
        )

        assertEquals("Items/movie-1/Images/Primary?tag=cover-1", receivedImage)
    }
}
