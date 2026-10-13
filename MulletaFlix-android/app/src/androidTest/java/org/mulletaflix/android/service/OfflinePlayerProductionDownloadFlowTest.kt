package org.mulletaflix.android.service

import android.content.Context
import android.content.ContextWrapper
import android.content.res.Configuration
import android.graphics.Bitmap
import androidx.lifecycle.ViewModelStore
import androidx.media3.common.C
import androidx.media3.common.util.UnstableApi
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import androidx.work.Configuration as WorkConfiguration
import androidx.work.WorkInfo
import androidx.work.WorkManager
import androidx.work.impl.WorkManagerImpl
import androidx.work.testing.WorkManagerTestInitHelper
import androidx.work.testing.WorkManagerTestInitHelper.ExecutorsMode
import coil.Coil
import coil.annotation.ExperimentalCoilApi
import coil.memory.MemoryCache
import com.squareup.moshi.Moshi
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okio.Buffer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.android.ImageCacheCleanup
import org.mulletaflix.android.service.DownloadManagerSingleton
import org.mulletaflix.android.service.Media3DownloadRepository
import org.mulletaflix.android.service.OfflineSubtitleRecoveryWorkScheduler
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.core.api.OfflineDownloadCache
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.data.repository.SettingsRepositoryImpl
import org.mulletaflix.domain.repository.DownloadState
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetNextEpisodeUseCase
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.feature.player.PlaybackIssueReporter
import org.mulletaflix.feature.player.PlayerViewModel
import java.io.File
import java.lang.reflect.Proxy
import java.util.UUID
import java.util.concurrent.TimeUnit

@RunWith(AndroidJUnit4::class)
@UnstableApi
@OptIn(ExperimentalCoilApi::class)
class OfflinePlayerProductionDownloadFlowTest {
    @Test
    fun pausingOnePartialDownloadPreservesItWhileItsSiblingContinues() {
        val expectedProfile = checkNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE for this run" }
        val context = ApplicationProvider.getApplicationContext<Context>()
        val configuration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
        val television = (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        val actualProfile = if (configuration.smallestScreenWidthDp >= 600) "TABLET" else "PHONE"
        assertTrue("Per-item download integration targets a handheld device", !television)
        assertEquals("Run on the declared device profile", expectedProfile, actualProfile)

        val testId = UUID.randomUUID().toString()
        val firstMediaId = "pause-resume-first-$testId"
        val siblingMediaId = "pause-resume-sibling-$testId"
        val firstTitle = "Download pausável $testId"
        val siblingTitle = "Download irmão $testId"
        val server = MockWebServer()
        val isolatedDataDirectory = File(context.cacheDir, "per-download-pause-session/$testId")
        val isolatedContext = object : ContextWrapper(context) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = File(isolatedDataDirectory, "files").apply { mkdirs() }
        }
        val sessionRepository = SessionRepositoryImpl(isolatedContext)
        val settingsRepository = SettingsRepositoryImpl(isolatedContext, sessionRepository)
        val manager = DownloadManagerSingleton.get(context)
        var repository: Media3DownloadRepository? = null
        val temporaryRequestIds = mutableListOf<String>()
        var failure: Throwable? = null

        try {
            assertNoActiveProductionDownloads(manager)
            check(!context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
                .getBoolean("queue_paused", false)
            ) { "Refusing to change a user-paused download queue during the integration test" }

            val partialBody = ByteArray(8 * 1024 * 1024) { (it % 251).toByte() }
            val siblingBody = ByteArray(64 * 1024) { (it % 127).toByte() }
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "video/x-matroska")
                    .setBody(Buffer().write(partialBody))
                    .throttleBody(64 * 1024, 100, TimeUnit.MILLISECONDS),
            )
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "video/x-matroska")
                    .setBody(Buffer().write(siblingBody))
                    .throttleBody(16 * 1024, 100, TimeUnit.MILLISECONDS),
            )
            server.start()
            val baseUrl = server.url("/").toString().trimEnd('/')
            runBlocking {
                sessionRepository.saveSession(
                    serverUrl = baseUrl,
                    token = "isolated-download-pause-token",
                    userId = "download-pause-user-$testId",
                    deviceId = "download-pause-device-$testId",
                )
            }
            val downloadRepository = Media3DownloadRepository(
                appContext = context,
                sessionRepository = sessionRepository,
                clientIdentityInterceptor = ClientIdentityInterceptor(sessionRepository),
                subtitleRecoveryWorkScheduler = OfflineSubtitleRecoveryWorkScheduler(context),
            )
            repository = downloadRepository
            val firstRequestId = serverScopedDownloadRequestId("download-pause-user-$testId", baseUrl, firstMediaId)
            val siblingRequestId = serverScopedDownloadRequestId("download-pause-user-$testId", baseUrl, siblingMediaId)
            temporaryRequestIds += firstRequestId
            temporaryRequestIds += siblingRequestId

            awaitValue("repository accepts first isolated download") {
                downloadRepository.enqueue(
                    firstMediaId,
                    firstTitle,
                    server.url("/partial-first.mkv").toString(),
                ).takeIf { it.isSuccess }
            }.getOrThrow()

            val bytesBeforePause = awaitValue("first download has persisted partial bytes") {
                val download = manager.downloadIndex.getDownload(firstRequestId)
                download?.takeIf {
                    it.state == androidx.media3.exoplayer.offline.Download.STATE_DOWNLOADING &&
                        it.getBytesDownloaded() > 0L
                }?.getBytesDownloaded()
            }
            assertTrue("Pause must be exercised after partial progress", bytesBeforePause > 0L)

            awaitValue("repository accepts sibling download") {
                downloadRepository.enqueue(
                    siblingMediaId,
                    siblingTitle,
                    server.url("/sibling.mkv").toString(),
                ).takeIf { it.isSuccess }
            }.getOrThrow()
            downloadRepository.pause(firstMediaId).getOrThrow()

            awaitCondition("first download persists only its individual stop reason") {
                val download = manager.downloadIndex.getDownload(firstRequestId)
                download?.state == androidx.media3.exoplayer.offline.Download.STATE_STOPPED &&
                    download.stopReason == USER_PAUSED_DOWNLOAD_STOP_REASON
            }
            val pausedDownload = requireNotNull(manager.downloadIndex.getDownload(firstRequestId))
            assertTrue("Pausing must preserve the bytes already downloaded", pausedDownload.getBytesDownloaded() >= bytesBeforePause)

            awaitCondition("sibling download finishes while the first remains paused") {
                manager.downloadIndex.getDownload(siblingRequestId)?.state ==
                    androidx.media3.exoplayer.offline.Download.STATE_COMPLETED
            }
            assertEquals(
                DownloadState.Paused,
                runBlocking {
                    downloadRepository.observeDownloads().first { entries -> entries.any { it.id == firstMediaId } }
                        .single { it.id == firstMediaId }.state
                },
            )
            assertEquals(
                DownloadState.Completed,
                runBlocking {
                    downloadRepository.observeDownloads().first { entries -> entries.any { it.id == siblingMediaId } }
                        .single { it.id == siblingMediaId }.state
                },
            )

            downloadRepository.resume(firstMediaId).getOrThrow()
            awaitCondition("the same first download resumes to completion") {
                manager.downloadIndex.getDownload(firstRequestId)?.state ==
                    androidx.media3.exoplayer.offline.Download.STATE_COMPLETED
            }
            assertEquals(
                "The queue-wide pause preference must remain untouched",
                false,
                context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
                    .getBoolean("queue_paused", false),
            )
        } catch (throwable: Throwable) {
            failure = throwable
        } finally {
            runCatching { server.shutdown() }.onFailure { if (failure == null) failure = it }
            temporaryRequestIds.forEach { requestId ->
                runCatching {
                    if (manager.downloadIndex.getDownload(requestId) != null) {
                        checkNotNull(repository).remove(requestId).getOrThrow()
                        awaitCondition("temporary per-item test download is removed") {
                            manager.downloadIndex.getDownload(requestId) == null
                        }
                    }
                    val downloadPreferences = context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
                    check(downloadPreferences.edit()
                        .remove("title:$requestId")
                        .remove("item:$requestId")
                        .remove("owner:$requestId")
                        .remove("server:$requestId")
                        .remove("image:$requestId")
                        .remove(downloadFailureMetadataKey(requestId))
                        .commit()) { "Unable to persist removal of temporary download metadata" }
                }.onFailure { if (failure == null) failure = it }
            }
            runCatching {
                runBlocking {
                    settingsRepository.clearLocalPreferences()
                    sessionRepository.clearSession()
                }
                isolatedDataDirectory.deleteRecursively()
            }.onFailure { if (failure == null) failure = it }
        }
        failure?.let { throw it }
    }

    @Test
    fun productionDownloadRepositoryFeedsOfflinePlayerFromCacheWithoutNetwork() {
        val expectedProfile = checkNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE or TABLET for this run" }
        val context = ApplicationProvider.getApplicationContext<Context>()
        val configuration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
        val television = (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        val actualProfile = if (configuration.smallestScreenWidthDp >= 600) "TABLET" else "PHONE"
        assertTrue("This offline playback test targets handheld profiles", !television)
        assertEquals("Run on the declared device profile", expectedProfile, actualProfile)

        val fixture = InstrumentationRegistry.getInstrumentation().context.assets
            .open("offline-multitrack.mkv")
            .use { it.readBytes() }
        val testId = UUID.randomUUID().toString()
        val mediaId = "offline-player-$testId"
        val title = "Fixture multifaixa $testId"
        val server = MockWebServer()
        val viewModelScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val viewModelStore = ViewModelStore()
        val isolatedDataDirectory = File(context.cacheDir, "offline-production-session/$testId")
        val isolatedContext = object : ContextWrapper(context) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = File(isolatedDataDirectory, "files").apply { mkdirs() }
        }
        val sessionRepository = SessionRepositoryImpl(isolatedContext)
        val settingsRepository = SettingsRepositoryImpl(isolatedContext, sessionRepository)
        val cache = OfflineDownloadCache.get(context)
        val manager = DownloadManagerSingleton.get(context)
        var repository: Media3DownloadRepository? = null
        var isolatedDownloadRequestId: String? = null
        var failure: Throwable? = null

        try {
            assertNoActiveProductionDownloads(manager)
            check(!context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
                .getBoolean("queue_paused", false)
            ) { "Refusing to change a user-paused download queue during the integration test" }
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "video/x-matroska")
                    .setBody(Buffer().write(fixture)),
            )
            server.start()
            val baseUrl = server.url("/").toString().trimEnd('/')
            runBlocking {
                sessionRepository.saveSession(
                    serverUrl = baseUrl,
                    token = "isolated-offline-test-token",
                    userId = "offline-test-user-$testId",
                    deviceId = "offline-test-device-$testId",
                )
            }
            val downloadRepository = Media3DownloadRepository(
                appContext = context,
                sessionRepository = sessionRepository,
                clientIdentityInterceptor = ClientIdentityInterceptor(sessionRepository),
                subtitleRecoveryWorkScheduler = OfflineSubtitleRecoveryWorkScheduler(context),
            )
            repository = downloadRepository
            val userId = "offline-test-user-$testId"
            isolatedDownloadRequestId = serverScopedDownloadRequestId(userId, baseUrl, mediaId)

            val requestUri = server.url("/offline-production.mkv").toString()
            val enqueueResult = awaitValue("production repository accepts the isolated download") {
                downloadRepository.enqueue(mediaId, title, requestUri).takeIf { it.isSuccess }
            }
            enqueueResult.getOrThrow()

            val downloadedEntry = runBlocking {
                withTimeout(TimeUnit.SECONDS.toMillis(35)) {
                    downloadRepository.observeDownloads().first { entries ->
                        entries.any { entry ->
                            entry.id == mediaId && entry.state == DownloadState.Completed &&
                                entry.uri.startsWith(server.url("/").toString().trimEnd('/'))
                        }
                    }.single { it.id == mediaId }
                }
            }
            assertEquals("The repository must keep the expected account/server request identity", isolatedDownloadRequestId, downloadedEntry.downloadId)
            assertEquals("The repository must persist the requested title", title, downloadedEntry.title)
            assertEquals("The repository must report all downloaded bytes", fixture.size.toLong(), downloadedEntry.bytesDownloaded)
            assertTrue("The production cache should contain the completed fixture", cache.cacheSpace >= fixture.size)
            assertEquals("The file should be downloaded once", 1, server.requestCount)

            executeHourlyImageCleanup(context)
            assertEquals(
                "The hourly image cleanup must preserve the production download state",
                androidx.media3.exoplayer.offline.Download.STATE_COMPLETED,
                manager.downloadIndex.getDownload(downloadedEntry.downloadId)?.state,
            )

            val mediaRepository = unusedDependency<MediaRepository>()
            lateinit var viewModel: PlayerViewModel
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                viewModel = PlayerViewModel(
                    context = context,
                    getItemDetailUseCase = GetItemDetailUseCase(mediaRepository),
                    playbackRepository = unusedDependency(),
                    sessionRepository = sessionRepository,
                    syncPlayRealtimeClient = SyncPlayRealtimeClient(
                        httpClient = OkHttpClient(),
                        sessionRepository = sessionRepository,
                        applicationScope = viewModelScope,
                        moshi = Moshi.Builder().build(),
                    ),
                    manageSyncPlayUseCase = ManageSyncPlayUseCase(unusedDependency<SyncPlayRepository>()),
                    settingsRepository = settingsRepository,
                    getNextEpisodeUseCase = GetNextEpisodeUseCase(mediaRepository),
                    manageDownloadsUseCase = ManageDownloadsUseCase(downloadRepository),
                    networkMonitor = object : NetworkMonitor {
                        override val isOnline = kotlinx.coroutines.flow.flowOf(false)
                        override val isMetered = kotlinx.coroutines.flow.flowOf(false)
                    },
                    playbackIssueReporter = PlaybackIssueReporter(
                        sessionRepository = sessionRepository,
                        userFeedbackRepository = unusedDependency<UserFeedbackRepository>(),
                        playbackIssueQueue = unusedDependency<PlaybackIssueQueue>(),
                    ),
                    teardownScope = viewModelScope,
                )
                viewModelStore.put("production-offline-player", viewModel)
            }

            // A shut-down origin makes a successful READY state evidence of cache-backed playback.
            server.shutdown()
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                viewModel.loadOffline(downloadedEntry.uri, title, downloadedEntry.downloadId)
            }
            awaitCondition("production PlayerViewModel exposes the cached audio/subtitle streams") {
                viewModel.state.value.audioTracks.size == 2 &&
                    viewModel.state.value.subtitleTracks.size == 1 &&
                    !viewModel.state.value.isBuffering
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertEquals(
                    "PlayerViewModel should prepare cached media to READY",
                    androidx.media3.common.Player.STATE_READY,
                    viewModel.player.playbackState,
                )
                assertNull("The offline player reported a playback failure", viewModel.player.playerError)
            }
            assertEquals(title, viewModel.state.value.title)
            assertEquals("Reprodução offline", viewModel.state.value.playbackStats?.playMethod)

            val englishTrack = viewModel.state.value.audioTracks.indexOfFirst {
                it.language?.lowercase() in setOf("eng", "en")
            }
            assertTrue("The actual repository/player flow did not expose English audio", englishTrack >= 0)
            InstrumentationRegistry.getInstrumentation().runOnMainSync { viewModel.selectAudio(englishTrack) }
            awaitCondition("the player selects English audio from the persisted download") {
                viewModel.state.value.selectedAudioIndex == englishTrack
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertEquals(
                    1,
                    viewModel.player.currentTracks.groups
                        .filter { it.type == C.TRACK_TYPE_AUDIO }
                        .flatMap { group -> (0 until group.length).map(group::isTrackSelected) }
                        .count { it },
                )
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync { viewModel.selectSubtitle(0) }
            awaitCondition("the player selects the embedded subtitle") {
                viewModel.state.value.selectedSubtitleIndex == 0
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertTrue(
                    viewModel.player.currentTracks.groups
                        .filter { it.type == C.TRACK_TYPE_TEXT }
                        .flatMap { group -> (0 until group.length).map(group::isTrackSelected) }
                        .any { it },
                )
            }
            assertEquals("Offline playback attempted another origin request", 1, server.requestCount)
        } catch (throwable: Throwable) {
            failure = throwable
        } finally {
            runCatching { server.shutdown() }.onFailure { if (failure == null) failure = it }
            InstrumentationRegistry.getInstrumentation().runOnMainSync { viewModelStore.clear() }
            isolatedDownloadRequestId?.let { requestId ->
                runCatching {
                    val downloadPreferences = context.getSharedPreferences("offline_downloads", Context.MODE_PRIVATE)
                    if (manager.downloadIndex.getDownload(requestId) != null) {
                        checkNotNull(repository).remove(requestId).getOrThrow()
                        awaitCondition("the test download is removed from the production index") {
                            manager.downloadIndex.getDownload(requestId) == null
                        }
                    }
                    check(downloadPreferences.edit()
                        .remove("title:$requestId")
                        .remove("item:$requestId")
                        .remove("owner:$requestId")
                        .remove("server:$requestId")
                        .remove("image:$requestId")
                        .remove(downloadFailureMetadataKey(requestId))
                        .commit()) { "Unable to persist removal of temporary download metadata" }
                    awaitCondition("isolated repository metadata is removed") {
                        listOf(
                            "title:$requestId",
                            "item:$requestId",
                            "owner:$requestId",
                            "server:$requestId",
                            "image:$requestId",
                            downloadFailureMetadataKey(requestId),
                        ).none(downloadPreferences::contains)
                    }
                }.onFailure { if (failure == null) failure = it }
            }
            viewModelScope.cancel()
            runCatching {
                runBlocking {
                    settingsRepository.clearLocalPreferences()
                    sessionRepository.clearSession()
                }
                isolatedDataDirectory.deleteRecursively()
            }.onFailure { if (failure == null) failure = it }
        }
        failure?.let { throw it }
    }

    private fun assertNoActiveProductionDownloads(manager: androidx.media3.exoplayer.offline.DownloadManager) {
        val cursor = manager.downloadIndex.getDownloads()
        try {
            val active = buildList {
                while (cursor.moveToNext()) {
                    val download = cursor.download
                    if (download.state in setOf(
                            androidx.media3.exoplayer.offline.Download.STATE_QUEUED,
                            androidx.media3.exoplayer.offline.Download.STATE_DOWNLOADING,
                            androidx.media3.exoplayer.offline.Download.STATE_RESTARTING,
                            androidx.media3.exoplayer.offline.Download.STATE_REMOVING,
                        )
                    ) add(download.request.id)
                }
            }
            check(active.isEmpty()) {
                "Refusing to resume or alter pre-existing active user downloads during this integration test: $active"
            }
        } finally {
            cursor.close()
        }
    }

    private fun awaitCondition(message: String, condition: () -> Boolean) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30)
        while (System.nanoTime() < deadline) {
            if (condition()) return
            Thread.sleep(40)
        }
        assertTrue("Timed out: $message", condition())
    }

    private fun executeHourlyImageCleanup(context: Context) {
        val originalWorkManager = WorkManagerImpl.getInstance(context)
        var workManager: WorkManager? = null
        var initialized = false
        val imageLoader = Coil.imageLoader(context)
        val memoryCache = requireNotNull(imageLoader.memoryCache)
        val cacheKey = MemoryCache.Key("offline-cleanup-integration-${UUID.randomUUID()}")
        val bitmap = Bitmap.createBitmap(2, 2, Bitmap.Config.ARGB_8888)

        try {
            WorkManagerTestInitHelper.initializeTestWorkManager(
                context,
                WorkConfiguration.Builder().build(),
                ExecutorsMode.LEGACY_OVERRIDE_WITH_SYNCHRONOUS_EXECUTORS,
            )
            initialized = true
            workManager = WorkManager.getInstance(context)
            ImageCacheCleanup.schedule(context)

            val scheduled = workManager
                .getWorkInfosForUniqueWork(HOURLY_IMAGE_CACHE_CLEANUP_WORK_NAME)
                .get()
                .single()
            assertEquals(
                TimeUnit.HOURS.toMillis(ImageCacheCleanup.INTERVAL_HOURS),
                requireNotNull(scheduled.periodicityInfo).repeatIntervalMillis,
            )
            awaitPeriodicWorkEnqueued(workManager, scheduled.id)

            memoryCache[cacheKey] = MemoryCache.Value(bitmap)
            assertNotNull("The test artwork entry must exist before the worker runs", memoryCache[cacheKey])
            requireNotNull(WorkManagerTestInitHelper.getTestDriver(context))
                .setPeriodDelayMet(scheduled.id)
            awaitCondition("the scheduled image-cache worker clears Coil memory") {
                memoryCache[cacheKey] == null
            }
            val completedCycle = requireNotNull(workManager.getWorkInfoById(scheduled.id).get())
            assertEquals(WorkInfo.State.ENQUEUED, completedCycle.state)
            assertNull(memoryCache[cacheKey])
        } finally {
            memoryCache.remove(cacheKey)
            bitmap.recycle()
            try {
                if (initialized) {
                    try {
                        workManager?.cancelUniqueWork(HOURLY_IMAGE_CACHE_CLEANUP_WORK_NAME)?.result?.get()
                    } finally {
                        WorkManagerTestInitHelper.closeWorkDatabase()
                    }
                }
            } finally {
                // Restore the app's singleton even if test initialization failed midway.
                WorkManagerImpl.setDelegate(originalWorkManager)
            }
        }
    }

    private fun awaitPeriodicWorkEnqueued(workManager: WorkManager, id: UUID) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10)
        var latest = requireNotNull(workManager.getWorkInfoById(id).get())
        while (latest.state != WorkInfo.State.ENQUEUED && System.nanoTime() < deadline) {
            Thread.sleep(20)
            latest = requireNotNull(workManager.getWorkInfoById(id).get())
        }
        assertEquals(WorkInfo.State.ENQUEUED, latest.state)
    }

    private fun <T : Any> awaitValue(message: String, value: () -> T?): T {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10)
        while (System.nanoTime() < deadline) {
            value()?.let { return it }
            Thread.sleep(40)
        }
        throw AssertionError("Timed out: $message")
    }

    private inline fun <reified T : Any> unusedDependency(): T =
        Proxy.newProxyInstance(T::class.java.classLoader, arrayOf(T::class.java)) { _, method, _ ->
            throw AssertionError("Unexpected test dependency call: ${T::class.java.simpleName}.${method.name}")
        } as T

    private companion object {
        const val HOURLY_IMAGE_CACHE_CLEANUP_WORK_NAME = "hourly-image-cache-cleanup"
    }
}
