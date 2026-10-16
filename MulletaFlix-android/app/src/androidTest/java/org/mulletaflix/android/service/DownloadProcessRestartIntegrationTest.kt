package org.mulletaflix.android.service

import android.content.Context
import android.content.ContextWrapper
import android.content.pm.ActivityInfo
import android.net.Uri
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.mutableStateOf
import androidx.compose.material3.Text
import androidx.lifecycle.ViewModelStore
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.offline.DownloadService as Media3DownloadService
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.core.app.ApplicationProvider
import androidx.test.platform.app.InstrumentationRegistry
import com.squareup.moshi.Moshi
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout
import okhttp3.OkHttpClient
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.atomic.AtomicInteger
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.android.service.Media3DownloadRepository
import org.mulletaflix.android.service.OfflineSubtitleRecoveryWorkScheduler
import org.mulletaflix.core.api.ClientIdentityInterceptor
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.data.repository.SettingsRepositoryImpl
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetNextEpisodeUseCase
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.feature.player.PlaybackIssueReporter
import org.mulletaflix.feature.player.PlayerViewModel
import org.mulletaflix.feature.player.VideoPlayerScreen
import java.io.File
import java.lang.reflect.Proxy

/** Host-orchestrated process-death coverage. The two methods run in separate Gradle/runner processes. */
@UnstableApi
@RunWith(AndroidJUnit4::class)
class DownloadProcessRestartIntegrationTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<OfflinePlaybackTestActivity>()

    private val context: Context
        get() = InstrumentationRegistry.getInstrumentation().targetContext

    @Test
    fun composeHostRunsLaunchedEffectWithoutPlayerScreen() {
        val effectStarted = AtomicBoolean(false)

        composeRule.setContent {
            LaunchedEffect(Unit) { effectStarted.set(true) }
            Text("Compose host control")
        }

        composeRule.waitForIdle()
        composeRule.onNodeWithText("Compose host control").assertIsDisplayed()
        composeRule.waitUntil(timeoutMillis = 5_000) { effectStarted.get() }
        assertTrue("The isolated Compose host must run LaunchedEffect", effectStarted.get())
    }

    @Test
    fun offlinePlaybackHostKeepsComposeContentWhenOrientationChanges() {
        val launchedEffectStarts = AtomicInteger()
        composeRule.setContent {
            LaunchedEffect(Unit) { launchedEffectStarts.incrementAndGet() }
            Text("Offline player host survived orientation")
        }
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Offline player host survived orientation").assertIsDisplayed()
        composeRule.waitUntil(timeoutMillis = 5_000) { launchedEffectStarts.get() == 1 }

        val requestedOrientation = if (
            composeRule.activity.resources.configuration.orientation == android.content.res.Configuration.ORIENTATION_LANDSCAPE
        ) {
            ActivityInfo.SCREEN_ORIENTATION_PORTRAIT
        } else {
            ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
        }
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            composeRule.activity.requestedOrientation = requestedOrientation
        }

        composeRule.waitUntil(timeoutMillis = 5_000) {
            val actualOrientation = composeRule.activity.resources.configuration.orientation
            val expectedOrientation = if (requestedOrientation == ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE) {
                android.content.res.Configuration.ORIENTATION_LANDSCAPE
            } else {
                android.content.res.Configuration.ORIENTATION_PORTRAIT
            }
            actualOrientation == expectedOrientation
        }
        composeRule.waitForIdle()
        composeRule.onNodeWithText("Offline player host survived orientation").assertIsDisplayed()
        assertEquals("The orientation change must not restart the retained Compose effect", 1, launchedEffectStarts.get())
    }

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

        runBlocking {
            isolatedSessionRepository(testId).saveSession(
                serverUrl = Uri.parse(mediaUrl).buildUpon().path("").build().toString().trimEnd('/'),
                token = "synthetic-offline-process-restart-token",
                userId = "offline-process-restart-$testId",
                deviceId = "offline-process-restart-device-$testId",
            )
        }

        val manager = DownloadManagerSingleton.get(context)
        val requestId = requestId(testId)
        check(manager.downloadIndex.getDownload(requestId) == null) { "Test download id already exists" }
        manager.addDownload(
            DownloadRequest.Builder(requestId, Uri.parse(mediaUrl))
                .setMimeType("video/x-matroska")
                .build(),
        )
        Media3DownloadService.sendResumeDownloads(context, DownloadService::class.java, false)

        try {
            val expectedFixtureBytes = requireNotNull(arguments.getString("downloadRestartFixtureBytes"))
                .toLongOrNull() ?: error("Expected downloadRestartFixtureBytes")
            val partial = awaitDownload(manager, requestId) {
                it.state == Download.STATE_DOWNLOADING && it.bytesDownloaded >= MIN_PARTIAL_BYTES
            }
            assertTrue("The fixture must be interrupted before the full payload is cached", partial.bytesDownloaded < expectedFixtureBytes)
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
        val expectedFixtureBytes = arguments.getString("downloadRestartFixtureBytes")?.toLongOrNull()
            ?: error("Expected downloadRestartFixtureBytes")
        val mediaUrl = requireNotNull(arguments.getString("downloadRestartMediaUrl"))
        assertTrue("A fresh app process must issue a nonzero HTTP Range request", resumedOffset >= MIN_PARTIAL_BYTES)
        assertEquals("The restarted app must finish every byte remaining in the fixture", expectedFixtureBytes - resumedOffset, completedBytes)

        val context = ApplicationProvider.getApplicationContext<Context>()
        val requestId = requestId(testId)
        val manager = DownloadManagerSingleton.get(context)
        val completed = awaitDownload(manager, requestId, timeoutSeconds = 120) {
            it.state == Download.STATE_COMPLETED
        }
        assertEquals("Every fixture byte must be persisted by Media3", expectedFixtureBytes, completed.bytesDownloaded)
        assertEquals("The player fixture must use a supported Matroska container", "video/x-matroska", completed.request.mimeType)

        val testContext = isolatedContext(testId)
        val sessionRepository = SessionRepositoryImpl(testContext)
        val restoredUserId = runBlocking {
            withTimeout(TimeUnit.SECONDS.toMillis(10)) {
                sessionRepository.getCurrentUserId().first()
            }
        }
        assertEquals(
            "The fresh process must read the UUID-scoped synthetic session before constructing the player",
            "offline-process-restart-$testId",
            restoredUserId,
        )
        val settingsRepository = SettingsRepositoryImpl(testContext, sessionRepository)
        val downloadRepository = Media3DownloadRepository(
            appContext = context,
            sessionRepository = sessionRepository,
            clientIdentityInterceptor = ClientIdentityInterceptor(sessionRepository),
            subtitleRecoveryWorkScheduler = OfflineSubtitleRecoveryWorkScheduler(context),
        )
        val teardownScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val viewModelStore = ViewModelStore()
        val showPlayerScreen = mutableStateOf(true)
        lateinit var playerViewModel: PlayerViewModel
        try {
            val mediaRepository = unusedDependency<MediaRepository>()
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
                    manageDownloadsUseCase = ManageDownloadsUseCase(downloadRepository),
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
                viewModelStore.put("process-restarted-offline-player", playerViewModel)
            }

            val activityLifecycleBeforeContent = composeRule.activity.lifecycle.currentState
            val screenComposed = AtomicBoolean(false)
            val hostLaunchedEffectStarted = AtomicBoolean(false)
            composeRule.setContent {
                if (showPlayerScreen.value) {
                    SideEffect { screenComposed.set(true) }
                    LaunchedEffect(Unit) { hostLaunchedEffectStarted.set(true) }
                    VideoPlayerScreen(
                        itemId = requestId,
                        offlineUri = mediaUrl,
                        offlineTitle = "Retomada offline",
                        offlineDownloadId = requestId,
                        onBack = {},
                        viewModel = playerViewModel,
                    )
                }
            }
            composeRule.mainClock.advanceTimeByFrame()
            composeRule.waitForIdle()
            try {
                composeRule.waitUntil(timeoutMillis = 30_000) {
                    val playbackState = playerViewModel.state.value
                    playbackState.title == "Retomada offline" || playbackState.error != null
                }
            } catch (timeout: androidx.compose.ui.test.ComposeTimeoutException) {
                var media3Snapshot = "unavailable"
                InstrumentationRegistry.getInstrumentation().runOnMainSync {
                    media3Snapshot = "state=${playerViewModel.player.playbackState}, " +
                        "error=${playerViewModel.player.playerError}, " +
                        "mediaItem=${playerViewModel.player.currentMediaItem?.localConfiguration?.uri}, " +
                        "activityLifecycle=${composeRule.activity.lifecycle.currentState}"
                }
                throw AssertionError(
                    "Timed out waiting for offline load; playerState=${playerViewModel.state.value}; " +
                        "media3=[$media3Snapshot]; restoredUserId=$restoredUserId; " +
                        "activityLifecycleBeforeContent=$activityLifecycleBeforeContent; " +
                        "activityLifecycleAtTimeout=${composeRule.activity.lifecycle.currentState}; " +
                        "screenComposed=${screenComposed.get()}; " +
                        "hostLaunchedEffectStarted=${hostLaunchedEffectStarted.get()}",
                    timeout,
                )
            }
            assertEquals(
                "The real offline player must load its saved session before rendering the title; " +
                    "playerError=${playerViewModel.state.value.error}",
                "Retomada offline",
                playerViewModel.state.value.title,
            )
            composeRule.waitForIdle()
            composeRule.onNodeWithText("Retomada offline").assertIsDisplayed()

            awaitPlaybackReady(playerViewModel)
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                assertEquals("Resumed offline media must prepare the player", Player.STATE_READY, playerViewModel.player.playbackState)
                assertNull("Offline playback must not fall back to the disabled origin", playerViewModel.player.playerError)
            }
            assertEquals("The resumed download must be classified as offline playback", "Reprodução offline", playerViewModel.state.value.playbackStats?.playMethod)
            awaitPlaybackAdvancing(playerViewModel.player)
            val originCheckUrl = Uri.parse(mediaUrl).buildUpon().encodedPath("/assert-offline").build().toString()
            OkHttpClient().newCall(okhttp3.Request.Builder().url(originCheckUrl).build()).execute().use { response ->
                assertEquals("Playback must consume the completed cache without requesting the disabled media origin", 204, response.code)
            }
        } finally {
            try {
                InstrumentationRegistry.getInstrumentation().runOnMainSync {
                    showPlayerScreen.value = false
                }
                composeRule.waitForIdle()
            } finally {
                // Dispose the screen before clearing its manually owned ViewModelStore
                // so lifecycle observers are detached before both players are released.
                InstrumentationRegistry.getInstrumentation().runOnMainSync {
                    viewModelStore.clear()
                    assertNull(
                        "The isolated PlayerViewModel store must be empty after teardown",
                        viewModelStore.get("process-restarted-offline-player"),
                    )
                }
                teardownScope.cancel()
            }
        }

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
        awaitDownloadRemoved(DownloadManagerSingleton.get(context), requestId(testId))
        runBlocking { isolatedSessionRepository(testId).clearSession() }
        isolatedDataDirectory(testId).deleteRecursively()
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
        timeoutSeconds: Long = 45,
        condition: (Download) -> Boolean,
    ): Download {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(timeoutSeconds)
        var current: Download? = null
        while (System.nanoTime() < deadline) {
            current = manager.downloadIndex.getDownload(requestId)
            if (current != null && condition(current)) return current
            Thread.sleep(100)
        }
        throw AssertionError(
            "Timed out waiting for $requestId; last state=${current?.state}, " +
                "bytes=${current?.bytesDownloaded}, contentLength=${current?.contentLength}, " +
                "percent=${current?.percentDownloaded}, stopReason=${current?.stopReason}, " +
                "failureReason=${current?.failureReason}, " +
                "requestMimeType=${current?.request?.mimeType}",
        )
    }

    private fun awaitPlaybackReady(viewModel: PlayerViewModel) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30)
        while (System.nanoTime() < deadline) {
            val state = viewModel.state.value
            var playbackState = Player.STATE_IDLE
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playbackState = viewModel.player.playbackState
            }
            if (!state.isBuffering && (state.error != null || playbackState == Player.STATE_READY)) break
            Thread.sleep(40)
        }
        val state = viewModel.state.value
        var playbackState = Player.STATE_IDLE
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            playbackState = viewModel.player.playbackState
        }
        assertTrue("The resumed download should reach READY, not error: ${state.error}", playbackState == Player.STATE_READY)
    }

    private fun awaitPlaybackAdvancing(player: Player) {
        var initialPositionMs = 0L
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            initialPositionMs = player.currentPosition
        }
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(20)
        var isAdvancing = false
        var currentPositionMs = initialPositionMs
        var isPlaying = false
        var playWhenReady = false
        while (System.nanoTime() < deadline && !isAdvancing) {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                isPlaying = player.isPlaying
                playWhenReady = player.playWhenReady
                currentPositionMs = player.currentPosition
                isAdvancing = isPlaying && currentPositionMs >= initialPositionMs + MIN_PLAYBACK_ADVANCE_MS
            }
            if (!isAdvancing) Thread.sleep(100)
        }
        assertTrue(
            "Offline playback must advance media position, not only reach READY; " +
                "isPlaying=$isPlaying, playWhenReady=$playWhenReady, " +
                "positionMs=$currentPositionMs, startMs=$initialPositionMs",
            isAdvancing,
        )
    }

    private fun awaitDownloadRemoved(manager: DownloadManager, requestId: String) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30)
        while (System.nanoTime() < deadline) {
            if (manager.downloadIndex.getDownload(requestId) == null) return
            Thread.sleep(50)
        }
        assertNull("The isolated download request must be removed by the service", manager.downloadIndex.getDownload(requestId))
    }

    private fun isolatedDataDirectory(testId: String) = File(context.cacheDir, "download-process-restart-session/$testId")

    private fun isolatedContext(testId: String) = object : ContextWrapper(context) {
        override fun getApplicationContext(): Context = this
        override fun getFilesDir(): File = File(isolatedDataDirectory(testId), "files").apply { mkdirs() }
    }

    private fun isolatedSessionRepository(testId: String) = SessionRepositoryImpl(isolatedContext(testId))

    private inline fun <reified T : Any> unusedDependency(): T =
        Proxy.newProxyInstance(T::class.java.classLoader, arrayOf(T::class.java)) { _, method, _ ->
            throw AssertionError("Unexpected test dependency call: ${T::class.java.simpleName}.${method.name}")
        } as T

    private fun requestId(testId: String) = "codex-process-restart-$testId"

    private companion object {
        const val MIN_PARTIAL_BYTES = 512L * 1024L
        const val MIN_PLAYBACK_ADVANCE_MS = 500L
    }
}
