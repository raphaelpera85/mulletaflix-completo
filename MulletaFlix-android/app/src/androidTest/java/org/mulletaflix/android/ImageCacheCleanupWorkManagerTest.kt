package org.mulletaflix.android

import android.content.Context
import android.graphics.Bitmap
import android.os.SystemClock
import androidx.media3.database.StandaloneDatabaseProvider
import androidx.media3.datasource.cache.NoOpCacheEvictor
import androidx.media3.datasource.cache.SimpleCache
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.work.Configuration
import androidx.work.WorkInfo
import androidx.work.WorkManager
import androidx.work.impl.WorkManagerImpl
import androidx.work.testing.WorkManagerTestInitHelper
import androidx.work.testing.WorkManagerTestInitHelper.ExecutorsMode
import coil.Coil
import coil.annotation.ExperimentalCoilApi
import coil.disk.DiskCache
import coil.memory.MemoryCache
import java.io.File
import java.io.FileOutputStream
import java.util.UUID
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.runBlocking
import okio.buffer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

@OptIn(ExperimentalCoilApi::class)
@RunWith(AndroidJUnit4::class)
class ImageCacheCleanupWorkManagerTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private var workManager: WorkManager? = null
    private var originalWorkManager: WorkManagerImpl? = null

    @Before
    fun initializeWorkManager() {
        originalWorkManager = WorkManagerImpl.getInstance(context)
        WorkManagerTestInitHelper.initializeTestWorkManager(
            context,
            Configuration.Builder().build(),
            ExecutorsMode.LEGACY_OVERRIDE_WITH_SYNCHRONOUS_EXECUTORS,
        )
        workManager = WorkManager.getInstance(context)
    }

    @After
    fun restoreWorkManager() {
        try {
            workManager?.cancelUniqueWork(IMAGE_CACHE_CLEANUP_WORK_NAME)?.result?.get()
        } finally {
            if (workManager != null) {
                try {
                    WorkManagerTestInitHelper.closeWorkDatabase()
                } finally {
                    WorkManagerImpl.setDelegate(originalWorkManager)
                }
            }
        }
    }

    @Test
    fun hourlyPeriodicWorkClearsCoilAndPreservesOfflineMediaCache() {
        val manager = requireNotNull(workManager)
        ImageCacheCleanup.schedule(context)
        val scheduled = manager
            .getWorkInfosForUniqueWork(IMAGE_CACHE_CLEANUP_WORK_NAME)
            .get()
            .single()

        assertEquals(
            TimeUnit.HOURS.toMillis(ImageCacheCleanup.INTERVAL_HOURS),
            requireNotNull(scheduled.periodicityInfo).repeatIntervalMillis,
        )
        // Test WorkManager may immediately run a periodic request once on enqueue.
        // Wait for that initial cycle to finish so the seeded entries are only
        // observed by the explicit TestDriver-triggered cycle below.
        val readyForTestCycle = awaitPeriodicWorkEnqueued(scheduled.id)

        val imageLoader = Coil.imageLoader(context)
        val memoryCache = requireNotNull(imageLoader.memoryCache)
        val diskCache = requireNotNull(imageLoader.diskCache)
        val cacheToken = UUID.randomUUID().toString()
        val memoryKey = MemoryCache.Key("cleanup-test-memory-$cacheToken")
        val diskKey = "cleanup-test-disk-$cacheToken"
        val bitmap = Bitmap.createBitmap(2, 2, Bitmap.Config.ARGB_8888)
        val offlineCacheDirectory = File(context.cacheDir, "image-cleanup-test-$cacheToken")
        val offlineDatabase = StandaloneDatabaseProvider(context)
        val offlineCache = SimpleCache(offlineCacheDirectory, NoOpCacheEvictor(), offlineDatabase)
        val offlineKey = "cleanup-test-media-$cacheToken"
        val offlineBytes = byteArrayOf(11, 22, 33, 44)

        try {
            memoryCache[memoryKey] = MemoryCache.Value(bitmap)
            writeCoilDiskEntry(diskCache, diskKey)
            writeOfflineCacheEntry(offlineCache, offlineKey, offlineBytes)

            assertNotNull(memoryCache[memoryKey])
            assertTrue(hasCoilDiskEntry(diskCache, diskKey))
            assertTrue(offlineCache.isCached(offlineKey, 0, offlineBytes.size.toLong()))

            runBlocking { CoilArtworkCacheCleaner(context).clear() }
            assertNull("manual clear evicts the in-memory poster", memoryCache[memoryKey])
            assertFalse("manual clear evicts the disk poster", hasCoilDiskEntry(diskCache, diskKey))
            assertTrue("manual artwork cleanup preserves offline media", offlineCache.isCached(offlineKey, 0, offlineBytes.size.toLong()))

            memoryCache[memoryKey] = MemoryCache.Value(bitmap)
            writeCoilDiskEntry(diskCache, diskKey)

            val testDriver = requireNotNull(WorkManagerTestInitHelper.getTestDriver(context))
            testDriver.setPeriodDelayMet(readyForTestCycle.id)

            awaitArtworkCacheClear(memoryCache, memoryKey, diskCache, diskKey)
            val completedCycle = awaitPeriodicWorkEnqueued(scheduled.id)
            assertEquals(WorkInfo.State.ENQUEUED, completedCycle.state)
            assertEquals(
                TimeUnit.HOURS.toMillis(ImageCacheCleanup.INTERVAL_HOURS),
                requireNotNull(completedCycle.periodicityInfo).repeatIntervalMillis,
            )
            assertNull(memoryCache[memoryKey])
            assertFalse(hasCoilDiskEntry(diskCache, diskKey))
            assertTrue(offlineCache.isCached(offlineKey, 0, offlineBytes.size.toLong()))
        } finally {
            memoryCache.remove(memoryKey)
            diskCache.remove(diskKey)
            offlineCache.removeResource(offlineKey)
            offlineCache.release()
            SimpleCache.delete(offlineCacheDirectory, offlineDatabase)
            bitmap.recycle()
        }
    }

    private fun writeCoilDiskEntry(cache: DiskCache, key: String) {
        val editor = requireNotNull(cache.openEditor(key))
        try {
            cache.fileSystem.sink(editor.data).buffer().use { sink ->
                sink.write("test-artwork-cache-entry".toByteArray(Charsets.UTF_8))
            }
            editor.commit()
        } catch (failure: Throwable) {
            editor.abort()
            throw failure
        }
    }

    private fun hasCoilDiskEntry(cache: DiskCache, key: String): Boolean {
        val snapshot = cache.openSnapshot(key) ?: return false
        snapshot.close()
        return true
    }

    private fun writeOfflineCacheEntry(
        cache: androidx.media3.datasource.cache.Cache,
        key: String,
        bytes: ByteArray,
    ) {
        val hole = cache.startReadWrite(key, 0, bytes.size.toLong())
        assertFalse(hole.isCached)
        try {
            val file = cache.startFile(key, 0, bytes.size.toLong())
            FileOutputStream(file).use { it.write(bytes) }
            cache.commitFile(file, bytes.size.toLong())
        } finally {
            cache.releaseHoleSpan(hole)
        }
    }

    private fun awaitArtworkCacheClear(
        memoryCache: MemoryCache,
        memoryKey: MemoryCache.Key,
        diskCache: DiskCache,
        diskKey: String,
    ) {
        val deadline = SystemClock.elapsedRealtime() + TimeUnit.SECONDS.toMillis(5)
        while ((memoryCache[memoryKey] != null || hasCoilDiskEntry(diskCache, diskKey)) &&
            SystemClock.elapsedRealtime() < deadline
        ) {
            Thread.sleep(20)
        }
        assertNull("Memory artwork entry was not cleared", memoryCache[memoryKey])
        assertFalse("Disk artwork entry was not cleared", hasCoilDiskEntry(diskCache, diskKey))
    }

    private fun awaitPeriodicWorkEnqueued(id: UUID): WorkInfo {
        val deadline = SystemClock.elapsedRealtime() + TimeUnit.SECONDS.toMillis(5)
        var latest = requireNotNull(workManager?.getWorkInfoById(id)?.get())
        while (latest.state != WorkInfo.State.ENQUEUED && SystemClock.elapsedRealtime() < deadline) {
            Thread.sleep(20)
            latest = requireNotNull(workManager?.getWorkInfoById(id)?.get())
        }
        assertEquals(WorkInfo.State.ENQUEUED, latest.state)
        return latest
    }

    private companion object {
        const val IMAGE_CACHE_CLEANUP_WORK_NAME = "hourly-image-cache-cleanup"
    }
}
