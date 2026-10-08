package org.mulletaflix.feature.player

import android.content.Context
import android.content.ContextWrapper
import android.content.res.Configuration
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import android.net.Uri
import androidx.lifecycle.ViewModelStore
import androidx.media3.common.C
import androidx.media3.common.MimeTypes
import androidx.media3.common.util.UnstableApi
import androidx.media3.database.DefaultDatabaseProvider
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.scheduler.Requirements
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.squareup.moshi.Moshi
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okio.Buffer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.OfflineDownloadCache
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.core.api.cleartextAwareMediaDataSourceFactory
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.data.repository.SettingsRepositoryImpl
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.DownloadEntry
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
import java.lang.reflect.Proxy
import java.io.File
import java.util.UUID
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit

@RunWith(AndroidJUnit4::class)
@UnstableApi
class OfflinePlayerViewModelIntegrationTest {
    @Test
    fun playerViewModelPlaysCompletedProductionCacheDownloadAndExposesSelectableTracksOffline() {
        val expectedProfile = checkNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE or TABLET for this run" }
        val context = ApplicationProvider.getApplicationContext<Context>()
        val configuration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
        val isTelevision = (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) ==
            Configuration.UI_MODE_TYPE_TELEVISION
        val actualProfile = if (configuration.smallestScreenWidthDp >= 600) "TABLET" else "PHONE"
        assertTrue("Offline player integration is scoped to phone/tablet devices", !isTelevision)
        assertEquals("The test must execute on the declared device profile", expectedProfile, actualProfile)
        val fixture = InstrumentationRegistry.getInstrumentation().context.assets
            .open("offline-multitrack.mkv")
            .use { it.readBytes() }
        val testId = UUID.randomUUID().toString()
        val downloadId = "offline-player-vm-$testId"
        val server = MockWebServer()
        val executor = Executors.newSingleThreadExecutor()
        val databaseName = "offline-player-$testId.db"
        val databaseHelper = object : SQLiteOpenHelper(context, databaseName, null, 1) {
            override fun onCreate(database: SQLiteDatabase) = Unit
            override fun onUpgrade(database: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
        }
        val databaseProvider = DefaultDatabaseProvider(databaseHelper)
        val cache = OfflineDownloadCache.get(context)
        val teardownScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val viewModelStore = ViewModelStore()
        val isolatedDataDirectory = File(context.cacheDir, "offline-player-data/$testId")
        val isolatedContext = object : ContextWrapper(context) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = File(isolatedDataDirectory, "files").apply { mkdirs() }
        }
        val sessionRepository = SessionRepositoryImpl(isolatedContext)
        val settingsRepository = SettingsRepositoryImpl(isolatedContext, sessionRepository)
        var downloadManager: DownloadManager? = null
        lateinit var mediaUri: Uri
        lateinit var playerViewModel: PlayerViewModel
        var failure: Throwable? = null

        try {
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "video/x-matroska")
                    .setBody(Buffer().write(fixture)),
            )
            server.start()
            mediaUri = Uri.parse(server.url("/offline-player.mkv").toString())

            val manager = DownloadManager(
                context,
                databaseProvider,
                cache,
                cleartextAwareMediaDataSourceFactory(connectTimeoutMs = 5_000, readTimeoutMs = 5_000),
                executor,
            ).apply {
                setMinRetryCount(0)
                setMaxParallelDownloads(1)
                setRequirements(Requirements(Requirements.NETWORK))
                resumeDownloads()
            }
            downloadManager = manager
            manager.addDownload(
                DownloadRequest.Builder(downloadId, mediaUri)
                    .setMimeType(MimeTypes.VIDEO_MATROSKA)
                    .build(),
            )
            awaitCompletedDownload(manager, downloadId)
            assertTrue("Fixture was not written to the app's persistent download cache", cache.cacheSpace >= fixture.size)
            assertEquals("The source should be requested once for the completed download", 1, server.requestCount)

            runBlocking {
                sessionRepository.saveSession(
                    serverUrl = server.url("/").toString().trimEnd('/'),
                    token = "offline-test-token",
                    userId = "offline-test-user-$testId",
                    deviceId = "offline-test-device-$testId",
                )
            }
            val mediaRepository = unusedDependency<MediaRepository>()
            val offlineDownload = DownloadEntry(
                id = "offline-fixture-$testId",
                title = "Fixture multifaixa",
                uri = mediaUri.toString(),
                state = DownloadState.Completed,
                percent = 100,
                bytesDownloaded = fixture.size.toLong(),
                contentLength = fixture.size.toLong(),
                downloadId = downloadId,
            )
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerViewModel = PlayerViewModel(
                    context = context,
                    getItemDetailUseCase = GetItemDetailUseCase(mediaRepository),
                    playbackRepository = unusedDependency(),
                    sessionRepository = sessionRepository,
                    syncPlayRealtimeClient = SyncPlayRealtimeClient(
                        httpClient = OkHttpClient(),
                        sessionRepository = sessionRepository,
                        applicationScope = teardownScope,
                        moshi = Moshi.Builder().build(),
                    ),
                    manageSyncPlayUseCase = ManageSyncPlayUseCase(unusedDependency<SyncPlayRepository>()),
                    settingsRepository = settingsRepository,
                    getNextEpisodeUseCase = GetNextEpisodeUseCase(mediaRepository),
                    manageDownloadsUseCase = ManageDownloadsUseCase(offlineDownloadRepository(offlineDownload)),
                    networkMonitor = object : NetworkMonitor {
                        override val isOnline = flowOf(false)
                        override val isMetered = flowOf(false)
                    },
                    playbackIssueReporter = PlaybackIssueReporter(
                        sessionRepository = sessionRepository,
                        userFeedbackRepository = unusedDependency<UserFeedbackRepository>(),
                        playbackIssueQueue = unusedDependency<PlaybackIssueQueue>(),
                    ),
                    teardownScope = teardownScope,
                )
            }
            viewModelStore.put("offline-player", playerViewModel)

            // With the origin stopped, only the app's real cache data source can satisfy playback.
            server.shutdown()
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerViewModel.loadOffline(mediaUri.toString(), "Fixture multifaixa", downloadId)
            }

            awaitCondition("offline player publishes its container tracks") {
                playerViewModel.state.value.audioTracks.size == 2 &&
                    playerViewModel.state.value.subtitleTracks.size == 1 &&
                    !playerViewModel.state.value.isBuffering
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertEquals(
                    "PlayerViewModel should prepare offline media to READY",
                    androidx.media3.common.Player.STATE_READY,
                    playerViewModel.player.playbackState,
                )
                assertNull("Playback failed despite the completed persistent cache", playerViewModel.player.playerError)
            }
            assertEquals("Offline playback must not contact its stopped source", 1, server.requestCount)
            assertEquals("Fixture multifaixa", playerViewModel.state.value.title)
            assertEquals("Reprodução offline", playerViewModel.state.value.playbackStats?.playMethod)

            val audio = playerViewModel.state.value.audioTracks
            val englishAudioIndex = audio.indexOfFirst { it.language?.lowercase() in setOf("eng", "en") }
            assertTrue("English audio track was not exposed by PlayerViewModel: $audio", englishAudioIndex >= 0)
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerViewModel.selectAudio(englishAudioIndex)
            }
            awaitCondition("PlayerViewModel selects English audio") {
                playerViewModel.state.value.selectedAudioIndex == englishAudioIndex
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertEquals(
                    "Media3 should have exactly one selected audio stream",
                    1,
                    playerViewModel.player.currentTracks.groups
                        .filter { it.type == C.TRACK_TYPE_AUDIO }
                        .flatMap { group -> (0 until group.length).map(group::isTrackSelected) }
                        .count { it },
                )
            }

            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerViewModel.selectSubtitle(0)
            }
            awaitCondition("PlayerViewModel selects the embedded subtitle") {
                playerViewModel.state.value.selectedSubtitleIndex == 0
            }
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertTrue(
                    "Media3 did not select the requested subtitle stream",
                    playerViewModel.player.currentTracks.groups
                        .filter { it.type == C.TRACK_TYPE_TEXT }
                        .flatMap { group -> (0 until group.length).map(group::isTrackSelected) }
                        .any { it },
                )
            }
            assertEquals("Offline playback unexpectedly accessed the network source", 1, server.requestCount)
        } catch (throwable: Throwable) {
            failure = throwable
        } finally {
            runCatching { server.shutdown() }
                .onFailure { if (failure == null) failure = it }
            InstrumentationRegistry.getInstrumentation().runOnMainSync { viewModelStore.clear() }
            downloadManager?.let { manager ->
                runCatching {
                    if (manager.downloadIndex.getDownload(downloadId) != null) {
                        manager.removeDownload(downloadId)
                        awaitRemovedDownload(manager, downloadId)
                    }
                }
                    .onFailure { if (failure == null) failure = it }
                runCatching { manager.release() }
                    .onFailure { if (failure == null) failure = it }
            }
            executor.shutdownNow()
            teardownScope.cancel()
            runCatching {
                runBlocking {
                    settingsRepository.clearLocalPreferences()
                    sessionRepository.clearSession()
                }
                isolatedDataDirectory.deleteRecursively()
                databaseHelper.close()
                context.deleteDatabase(databaseName)
            }.onFailure { if (failure == null) failure = it }
        }
        failure?.let { throw it }
    }

    private fun awaitCompletedDownload(manager: DownloadManager, id: String) {
        awaitCondition("Media3 download $id completes") {
            val download = manager.downloadIndex.getDownload(id)
            if (download?.state == Download.STATE_FAILED) {
                throw AssertionError("Fixture download failed: ${download.failureReason}")
            }
            download?.state == Download.STATE_COMPLETED
        }
    }

    private fun awaitRemovedDownload(manager: DownloadManager, id: String) {
        awaitCondition("test download $id is removed from the persistent index") {
            manager.downloadIndex.getDownload(id) == null
        }
    }

    private fun awaitCondition(message: String, condition: () -> Boolean) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(20)
        while (System.nanoTime() < deadline) {
            if (condition()) return
            Thread.sleep(40)
        }
        assertTrue("Timed out: $message", condition())
    }

    private inline fun <reified T : Any> unusedDependency(): T =
        Proxy.newProxyInstance(T::class.java.classLoader, arrayOf(T::class.java)) { _, method, _ ->
            throw AssertionError("Unexpected test dependency call: ${T::class.java.simpleName}.${method.name}")
        } as T

    private fun offlineDownloadRepository(entry: DownloadEntry): DownloadRepository =
        Proxy.newProxyInstance(
            DownloadRepository::class.java.classLoader,
            arrayOf(DownloadRepository::class.java),
        ) { _, method, _ ->
            if (method.name == "observeDownloads") flowOf(listOf(entry))
            else throw AssertionError("Unexpected test download repository call: ${method.name}")
        } as DownloadRepository
}
