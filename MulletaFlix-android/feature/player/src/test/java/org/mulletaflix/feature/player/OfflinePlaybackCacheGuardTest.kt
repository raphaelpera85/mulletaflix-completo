package org.mulletaflix.feature.player

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

class OfflinePlaybackCacheGuardTest {
    @Test
    fun `player reads downloaded media without caching streamed bytes`() {
        val playerSource = File("src/main/java/org/mulletaflix/feature/player/PlayerViewModel.kt")
        val factorySource = File("src/main/java/org/mulletaflix/feature/player/PlaybackCacheDataSourceFactory.kt")
        assertTrue("PlayerViewModel.kt not found at ${playerSource.absolutePath}", playerSource.isFile)
        assertTrue("PlaybackCacheDataSourceFactory.kt not found at ${factorySource.absolutePath}", factorySource.isFile)

        assertTrue(playerSource.readText().contains("playbackCacheDataSourceFactory("))
        val factoryContents = factorySource.readText()
        assertTrue(factoryContents.contains(".setCacheWriteDataSinkFactory(null)"))
        assertTrue(factoryContents.contains(".setUpstreamDataSourceFactory(upstreamFactory)"))
    }
}
