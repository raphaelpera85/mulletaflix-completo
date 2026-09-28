package org.mulletaflix.android

import coil.disk.DiskCache
import coil.memory.MemoryCache
import coil.annotation.ExperimentalCoilApi
import io.mockk.mockk
import io.mockk.verify
import org.junit.Assert.assertEquals
import org.junit.Test

@OptIn(ExperimentalCoilApi::class)
class ImageCacheCleanupTest {
    @Test
    fun `hourly cleanup clears memory and disk artwork caches`() {
        val memoryCache = mockk<MemoryCache>(relaxed = true)
        val diskCache = mockk<DiskCache>(relaxed = true)

        clearArtworkCaches(memoryCache, diskCache)

        verify(exactly = 1) { memoryCache.clear() }
        verify(exactly = 1) { diskCache.clear() }
        assertEquals(1L, ImageCacheCleanup.INTERVAL_HOURS)
    }

    @Test
    fun `cleanup tolerates a cache that is not initialized`() {
        clearArtworkCaches(memoryCache = null, diskCache = null)
    }
}
