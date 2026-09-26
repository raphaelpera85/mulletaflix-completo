package org.mulletaflix.feature.player

import androidx.media3.common.C
import androidx.media3.datasource.ByteArrayDataSource
import androidx.media3.datasource.DataSource
import androidx.media3.datasource.DataSpec
import androidx.media3.datasource.StatsDataSource
import androidx.media3.datasource.cache.CacheDataSource
import androidx.media3.datasource.cache.NoOpCacheEvictor
import androidx.media3.datasource.cache.SimpleCache
import androidx.media3.database.StandaloneDatabaseProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.ByteArrayOutputStream
import java.io.File
import java.util.UUID
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class PlaybackCacheDataSourceTest {
    private lateinit var cacheDirectory: File
    private lateinit var databaseProvider: StandaloneDatabaseProvider
    private lateinit var cache: SimpleCache

    @Before
    fun setUp() {
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        databaseProvider = StandaloneDatabaseProvider(context)
        cacheDirectory = File(context.cacheDir, "playback-cache-tests/${UUID.randomUUID()}")
        cache = SimpleCache(cacheDirectory, NoOpCacheEvictor(), databaseProvider)
    }

    @After
    fun tearDown() {
        cache.release()
        SimpleCache.delete(cacheDirectory, databaseProvider)
    }

    @Test
    fun playbackReadsDownloadedSegmentsFromCacheWithoutReadingUpstream() {
        val downloadedMedia = ByteArray(32_768) { index -> (index % 251).toByte() }
        val downloadUpstream = StatsDataSource(ByteArrayDataSource(downloadedMedia))
        val dataSpec = mediaRequest("downloaded-item")
        val downloadSource = CacheDataSource.Factory()
            .setCache(cache)
            .setUpstreamDataSourceFactory(DataSource.Factory { downloadUpstream })
            .createDataSource()

        assertArrayEquals(downloadedMedia, readAll(downloadSource, dataSpec))
        assertTrue(cache.isCached("downloaded-item", 0, downloadedMedia.size.toLong()))
        assertEquals(downloadedMedia.size.toLong(), downloadUpstream.bytesRead)

        val playbackUpstream = StatsDataSource(ByteArrayDataSource(byteArrayOf(99)))
        val playbackSource = playbackCacheDataSourceFactory(
            cache = cache,
            upstreamFactory = DataSource.Factory { playbackUpstream },
        ).createDataSource()

        assertArrayEquals(downloadedMedia, readAll(playbackSource, dataSpec))
        assertEquals("Playback should not read upstream for downloaded media", 0L, playbackUpstream.bytesRead)
    }

    @Test
    fun streamedBytesReachPlayerButAreNotWrittenToPersistentCache() {
        val streamedMedia = ByteArray(48_000) { index -> ((index * 7) % 253).toByte() }
        val streamingUpstream = StatsDataSource(ByteArrayDataSource(streamedMedia))
        val dataSpec = mediaRequest("stream-only-item")
        val playbackSource = playbackCacheDataSourceFactory(
            cache = cache,
            upstreamFactory = DataSource.Factory { streamingUpstream },
        ).createDataSource()

        assertArrayEquals(streamedMedia, readAll(playbackSource, dataSpec))
        assertEquals(streamedMedia.size.toLong(), streamingUpstream.bytesRead)
        assertEquals(0L, cache.cacheSpace)
        assertFalse(cache.isCached("stream-only-item", 0, streamedMedia.size.toLong()))
    }

    private fun mediaRequest(cacheKey: String): DataSpec = DataSpec.Builder()
        .setUri("https://cache-test.invalid/$cacheKey")
        .setKey(cacheKey)
        .build()

    private fun readAll(source: DataSource, dataSpec: DataSpec): ByteArray {
        val output = ByteArrayOutputStream()
        val buffer = ByteArray(4_096)
        source.open(dataSpec)
        try {
            while (true) {
                val bytesRead = source.read(buffer, 0, buffer.size)
                if (bytesRead == C.RESULT_END_OF_INPUT) break
                output.write(buffer, 0, bytesRead)
            }
        } finally {
            source.close()
        }
        return output.toByteArray()
    }
}
