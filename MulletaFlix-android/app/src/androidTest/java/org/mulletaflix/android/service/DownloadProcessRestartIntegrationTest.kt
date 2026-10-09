package org.mulletaflix.android.service

import android.content.Context
import android.net.Uri
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.offline.DownloadService as Media3DownloadService
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.util.concurrent.TimeUnit
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith

/** Host-orchestrated process-death coverage. The two methods run in separate Gradle/runner processes. */
@UnstableApi
@RunWith(AndroidJUnit4::class)
class DownloadProcessRestartIntegrationTest {
    private val context: Context
        get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun preparePartialDownloadForHostProcessRestart() {
        assertDebugApplication()
        assertNoExistingActiveDownloads()
        assertQueueIsNotUserPaused()

        val arguments = InstrumentationRegistry.getArguments()
        val testId = requireNotNull(arguments.getString("downloadRestartTestId"))
        val mediaUrl = requireNotNull(arguments.getString("downloadRestartMediaUrl"))
        require(testId.matches(Regex("[a-f0-9-]{36}"))) { "Expected a UUID test id" }
        require(Uri.parse(mediaUrl).host == "10.0.2.2") { "Fixture must use the emulator host bridge" }

        val manager = DownloadManagerSingleton.get(context)
        val requestId = requestId(testId)
        check(manager.downloadIndex.getDownload(requestId) == null) { "Test download id already exists" }
        manager.addDownload(
            DownloadRequest.Builder(requestId, Uri.parse(mediaUrl))
                .setMimeType("video/mp4")
                .setCustomCacheKey("process-restart-fixture-$testId")
                .build(),
        )
        Media3DownloadService.sendResumeDownloads(context, DownloadService::class.java, false)

        try {
            val partial = awaitDownload(manager, requestId) {
                it.state == Download.STATE_DOWNLOADING && it.bytesDownloaded >= MIN_PARTIAL_BYTES
            }
            assertTrue("The fixture must be interrupted before the full payload is cached", partial.bytesDownloaded < FIXTURE_SIZE)
            manager.pauseDownloads()
            val stopped = awaitQuiescentPartial(manager, requestId)
            assertTrue("The partial byte count must survive queue pause", stopped.bytesDownloaded >= MIN_PARTIAL_BYTES)
        } catch (failure: Throwable) {
            manager.removeDownload(requestId)
            throw failure
        }
    }

    @Test
    fun verifyPartialDownloadResumedByFreshApplicationProcess() {
        assertDebugApplication()
        val arguments = InstrumentationRegistry.getArguments()
        val testId = requireNotNull(arguments.getString("downloadRestartTestId"))
        require(testId.matches(Regex("[a-f0-9-]{36}"))) { "Expected a UUID test id" }
        val resumedOffset = arguments.getString("downloadRestartObservedOffset")?.toLongOrNull() ?: 0L
        val completedBytes = arguments.getString("downloadRestartCompletedBytes")?.toLongOrNull() ?: 0L
        assertTrue("A fresh app process must issue a nonzero HTTP Range request", resumedOffset >= MIN_PARTIAL_BYTES)
        assertEquals("The restarted app must finish every byte remaining in the fixture", FIXTURE_SIZE - resumedOffset, completedBytes)

        // The host confirms the resumed request and full response before asking
        // the real DownloadService to remove only this UUID-scoped test item.
        Media3DownloadService.sendRemoveDownload(
            context,
            DownloadService::class.java,
            requestId(testId),
            false,
        )
    }

    @Test
    fun cleanupIsolatedHostProcessFixture() {
        assertDebugApplication()
        val testId = requireNotNull(InstrumentationRegistry.getArguments().getString("downloadRestartTestId"))
        require(testId.matches(Regex("[a-f0-9-]{36}"))) { "Expected a UUID test id" }
        Media3DownloadService.sendRemoveDownload(
            context,
            DownloadService::class.java,
            requestId(testId),
            false,
        )
    }

    private fun assertDebugApplication() {
        assertTrue("This test must never use the production package", context.packageName.endsWith(".debug"))
        assertEquals("Only the expected emulator can run the host-process test", "emulator-5556", InstrumentationRegistry.getArguments().getString("deviceSerial"))
    }

    private fun assertQueueIsNotUserPaused() {
        assertTrue(
            "Refusing to change a user-paused queue during integration testing",
            !context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE).getBoolean("queue_paused", false),
        )
    }

    private fun assertNoExistingActiveDownloads() {
        val cursor = DownloadManagerSingleton.get(context).downloadIndex.getDownloads()
        try {
            while (cursor.moveToNext()) {
                val download = cursor.download
                assertTrue(
                    "Refusing process restart test while user download ${download.request.id} is active",
                    download.state == Download.STATE_COMPLETED || download.state == Download.STATE_FAILED,
                )
            }
        } finally {
            cursor.close()
        }
    }

    private fun awaitQuiescentPartial(manager: DownloadManager, requestId: String): Download {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(15)
        var previousBytes = -1L
        var stableSamples = 0
        var current: Download? = null
        while (System.nanoTime() < deadline) {
            current = manager.downloadIndex.getDownload(requestId)
            if (current != null && current.bytesDownloaded == previousBytes) stableSamples++ else stableSamples = 0
            if (current != null && current.state != Download.STATE_DOWNLOADING &&
                current.bytesDownloaded >= MIN_PARTIAL_BYTES && stableSamples >= 4
            ) return current
            previousBytes = current?.bytesDownloaded ?: -1L
            Thread.sleep(150)
        }
        throw AssertionError("Download did not quiesce after pause; last state=${current?.state}, bytes=${current?.bytesDownloaded}")
    }

    private fun awaitDownload(
        manager: DownloadManager,
        requestId: String,
        condition: (Download) -> Boolean,
    ): Download {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(45)
        var current: Download? = null
        while (System.nanoTime() < deadline) {
            current = manager.downloadIndex.getDownload(requestId)
            if (current != null && condition(current)) return current
            Thread.sleep(100)
        }
        throw AssertionError("Timed out waiting for $requestId; last state=${current?.state}, bytes=${current?.bytesDownloaded}")
    }

    private fun requestId(testId: String) = "codex-process-restart-$testId"

    private companion object {
        const val MIN_PARTIAL_BYTES = 512L * 1024L
        const val FIXTURE_SIZE = 16L * 1024L * 1024L
    }
}
