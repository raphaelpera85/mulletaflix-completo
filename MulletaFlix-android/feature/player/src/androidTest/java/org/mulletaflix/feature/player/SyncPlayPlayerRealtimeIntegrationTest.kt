package org.mulletaflix.feature.player

import android.content.Context
import android.content.ContextWrapper
import android.content.res.Configuration
import androidx.lifecycle.ViewModelStore
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.squareup.moshi.Moshi
import java.io.File
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.data.repository.SettingsRepositoryImpl
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType
import org.mulletaflix.domain.model.MediaSource
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackInfo
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetNextEpisodeUseCase
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase

@RunWith(AndroidJUnit4::class)
@UnstableApi
class SyncPlayPlayerRealtimeIntegrationTest {
    @Test
    fun pausePositionReceivedWhileQueueItemLoadsIsAppliedAfterMediaPreparation() {
        val expectedProfile = checkNotNull(
            InstrumentationRegistry.getArguments().getString("expectedDeviceProfile"),
        ) { "Set expectedDeviceProfile to PHONE, TABLET, or TV for this run" }
        val context = ApplicationProvider.getApplicationContext<Context>()
        val configuration = InstrumentationRegistry.getInstrumentation().targetContext.resources.configuration
        val actualProfile = when {
            (configuration.uiMode and Configuration.UI_MODE_TYPE_MASK) == Configuration.UI_MODE_TYPE_TELEVISION -> "TV"
            configuration.smallestScreenWidthDp >= 600 -> "TABLET"
            else -> "PHONE"
        }
        assertEquals("Run this journey only on its declared device profile", expectedProfile, actualProfile)

        val testId = UUID.randomUUID().toString()
        val isolatedDataDirectory = File(context.cacheDir, "syncplay-player-data/$testId")
        val isolatedContext = object : ContextWrapper(context) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = File(isolatedDataDirectory, "files").apply { mkdirs() }
        }
        val sessionRepository = SessionRepositoryImpl(isolatedContext)
        val settingsRepository = SettingsRepositoryImpl(isolatedContext, sessionRepository)
        val socketServer = MockWebServer()
        val mediaServer = MockWebServer()
        val mediaFile = PlayerTestMedia.createSilentWav(context)
        val mediaLoadStarted = CountDownLatch(1)
        val allowMediaDetails = CompletableDeferred<Unit>()
        val socketOpened = CountDownLatch(1)
        val socketClosed = CountDownLatch(1)
        val teardownScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val viewModelStore = ViewModelStore()
        val mediaId = "syncplay-media-$testId"
        val playlistItemId = "syncplay-playlist-$testId"
        val mediaRepository = BlockingMediaRepository(
            item = MediaItem(id = mediaId, name = "SyncPlay fixture", type = MediaItemType.Movie),
            loadStarted = mediaLoadStarted,
            allowLoad = allowMediaDetails,
        )
        var realtimeClient: SyncPlayRealtimeClient? = null
        lateinit var playerViewModel: PlayerViewModel
        var failure: Throwable? = null

        try {
            mediaServer.start()
            mediaServer.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "audio/wav")
                    .setBody(okio.Buffer().write(mediaFile.readBytes())),
            )
            socketServer.enqueue(
                MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                    override fun onOpen(webSocket: WebSocket, response: okhttp3.Response) {
                        socketPeer = webSocket
                        socketOpened.countDown()
                    }

                    override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                        webSocket.close(if (code == 1005) 1000 else code, if (code == 1005) "" else reason)
                    }

                    override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                        socketClosed.countDown()
                    }
                }),
            )
            socketServer.start()
            runBlocking {
                sessionRepository.saveSession(
                    serverUrl = socketServer.url("/").toString().trimEnd('/'),
                    token = "syncplay-player-test-token",
                    userId = "syncplay-player-test-user-$testId",
                    deviceId = "syncplay-player-test-device-$testId",
                )
            }
            val activeRealtimeClient = SyncPlayRealtimeClient(
                httpClient = OkHttpClient(),
                sessionRepository = sessionRepository,
                applicationScope = teardownScope,
                moshi = Moshi.Builder().build(),
            )
            realtimeClient = activeRealtimeClient
            val playbackRepository = FixturePlaybackRepository(
                streamUrl = mediaServer.url("/fixture.wav").toString(),
            )
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                playerViewModel = PlayerViewModel(
                    context = context,
                    getItemDetailUseCase = GetItemDetailUseCase(mediaRepository),
                    playbackRepository = playbackRepository,
                    sessionRepository = sessionRepository,
                    syncPlayRealtimeClient = activeRealtimeClient,
                    manageSyncPlayUseCase = ManageSyncPlayUseCase(syncPlayRepository()),
                    settingsRepository = settingsRepository,
                    getNextEpisodeUseCase = GetNextEpisodeUseCase(mediaRepository),
                    manageDownloadsUseCase = ManageDownloadsUseCase(emptyDownloadRepository()),
                    networkMonitor = object : NetworkMonitor {
                        override val isOnline = flowOf(true)
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
            viewModelStore.put("syncplay-player", playerViewModel)
            activeRealtimeClient.start("group-1")
            assertTrue("SyncPlay WebSocket did not open", socketOpened.await(10, TimeUnit.SECONDS))
            val socket = checkNotNull(socketServer.takeRequest(5, TimeUnit.SECONDS))
            assertEquals("/socket", socket.path)
            val peer = waitForSocketPeer()

            peer.send(
                """{"MessageType":"SyncPlayPlayQueueUpdate","Data":{"GroupId":"group-1","Data":{"Playlist":[{"ItemId":"$mediaId","PlaylistItemId":"$playlistItemId"}],"PlayingItemIndex":0,"StartPositionTicks":10000000,"IsPlaying":true}}}""",
            )
            assertTrue("Queue update did not begin loading its media", mediaLoadStarted.await(10, TimeUnit.SECONDS))

            // Unpause then Pause makes command processing observable while details are held.
            peer.send(
                """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"group-1","PlaylistItemId":"$playlistItemId","Command":"Unpause","PositionTicks":40000000}}""",
            )
            awaitPlayerCondition(
                message = "newer Unpause reaches the active player",
                condition = { playerViewModel.player.playWhenReady },
            )
            peer.send(
                """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"group-1","PlaylistItemId":"$playlistItemId","Command":"Pause","PositionTicks":70000000}}""",
            )
            awaitPlayerCondition(
                message = "newer Pause reaches the active player",
                condition = { !playerViewModel.player.playWhenReady },
            )

            peer.send(
                """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"group-1","PlaylistItemId":"$playlistItemId","Command":"Unpause","PositionTicks":50000000}}""",
            )
            awaitPlayerCondition(
                message = "newer Unpause reaches the active player before Stop",
                condition = { playerViewModel.player.playWhenReady },
            )
            peer.send(
                """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"group-1","PlaylistItemId":"$playlistItemId","Command":"Pause","PositionTicks":70000000}}""",
            )
            awaitPlayerCondition(
                message = "newer Pause reaches the active player",
                condition = { !playerViewModel.player.playWhenReady },
            )

            allowMediaDetails.complete(Unit)
            awaitPlayerCondition(
                message = "Media3 prepares the queue-selected WAV",
                condition = {
                    playerViewModel.player.playbackState == Player.STATE_READY &&
                        playerViewModel.player.currentMediaItem?.mediaId == mediaId
                },
            )
            awaitPlayerCondition(
                message = "the newer Pause remains applied after preparation",
                condition = { !playerViewModel.player.playWhenReady },
            )
            awaitPlayerCondition(
                message = "the newer Pause position is retained after preparation",
                timeoutDetails = { playerSnapshot(playerViewModel) },
                condition = { playerViewModel.player.currentPosition in 6_500L..8_500L },
            )

            var finalPlaybackState = Player.STATE_IDLE
            var finalMediaId: String? = null
            var finalPositionMs = 0L
            var finalPlayWhenReady = true
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                finalPlaybackState = playerViewModel.player.playbackState
                finalMediaId = playerViewModel.player.currentMediaItem?.mediaId
                finalPositionMs = playerViewModel.player.currentPosition
                finalPlayWhenReady = playerViewModel.player.playWhenReady
            }
            assertEquals(Player.STATE_READY, finalPlaybackState)
            assertEquals(mediaId, finalMediaId)
            assertTrue("Pause position should supersede the older queue position", finalPositionMs in 6_500L..8_500L)
            assertTrue("The newer remote Pause must win after the queue item is prepared", !finalPlayWhenReady)
        } catch (throwable: Throwable) {
            failure = throwable
        } finally {
            allowMediaDetails.complete(Unit)
            realtimeClient?.stop()
            InstrumentationRegistry.getInstrumentation().runOnMainSync { viewModelStore.clear() }
            runCatching { socketServer.shutdown() }.onFailure { if (failure == null) failure = it }
            val closedCleanly = socketClosed.await(5, TimeUnit.SECONDS)
            runCatching { mediaServer.shutdown() }.onFailure { if (failure == null) failure = it }
            teardownScope.cancel()
            mediaFile.delete()
            runCatching {
                runBlocking {
                    settingsRepository.clearLocalPreferences()
                    sessionRepository.clearSession()
                }
                isolatedDataDirectory.deleteRecursively()
            }.onFailure { if (failure == null) failure = it }
            if (failure == null) {
                assertTrue("SyncPlay WebSocket did not complete its close handshake", closedCleanly)
            }
        }
        failure?.let { throw it }
    }

    private fun waitForSocketPeer(): WebSocket {
        // The server listener retains the peer for deterministic server-originated events.
        return socketPeer ?: error("MockWebServer did not expose the SyncPlay peer")
    }

    private var socketPeer: WebSocket? = null

    private fun awaitPlayerCondition(
        message: String,
        timeoutDetails: () -> String = { "" },
        condition: () -> Boolean,
    ) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(20)
        while (System.nanoTime() < deadline) {
            var current = false
            InstrumentationRegistry.getInstrumentation().runOnMainSync { current = condition() }
            if (current) return
            Thread.sleep(40)
        }
        var current = false
        InstrumentationRegistry.getInstrumentation().runOnMainSync { current = condition() }
        assertTrue("Timed out: $message ${timeoutDetails()}", current)
    }

    private fun playerSnapshot(playerViewModel: PlayerViewModel): String {
        var snapshot = ""
        InstrumentationRegistry.getInstrumentation().runOnMainSync {
            snapshot = "playbackState=${playerViewModel.player.playbackState}, " +
                "mediaId=${playerViewModel.player.currentMediaItem?.mediaId}, " +
                "playWhenReady=${playerViewModel.player.playWhenReady}, " +
                "positionMs=${playerViewModel.player.currentPosition}"
        }
        return snapshot
    }

    private fun emptyDownloadRepository(): DownloadRepository =
        java.lang.reflect.Proxy.newProxyInstance(
            DownloadRepository::class.java.classLoader,
            arrayOf(DownloadRepository::class.java),
        ) { _, method, _ ->
            if (method.name == "observeDownloads") flowOf(emptyList<DownloadEntry>())
            else throw AssertionError("Unexpected download repository call: ${method.name}")
        } as DownloadRepository

    private fun syncPlayRepository(): SyncPlayRepository =
        java.lang.reflect.Proxy.newProxyInstance(
            SyncPlayRepository::class.java.classLoader,
            arrayOf(SyncPlayRepository::class.java),
        ) { _, method, _ ->
            when {
                method.name.startsWith("reportBuffering") || method.name.startsWith("reportReady") -> Result.success(Unit)
                else -> throw AssertionError("Unexpected SyncPlay repository call: ${method.name}")
            }
        } as SyncPlayRepository

    private inline fun <reified T : Any> unusedDependency(): T =
        java.lang.reflect.Proxy.newProxyInstance(T::class.java.classLoader, arrayOf(T::class.java)) { _, method, _ ->
            throw AssertionError("Unexpected test dependency call: ${T::class.java.simpleName}.${method.name}")
        } as T

    private class BlockingMediaRepository(
        private val item: MediaItem,
        private val loadStarted: CountDownLatch,
        private val allowLoad: CompletableDeferred<Unit>,
    ) : MediaRepository {
        override suspend fun getItem(userId: String, itemId: String): Result<MediaItem> {
            loadStarted.countDown()
            allowLoad.await()
            return Result.success(item)
        }

        override suspend fun getResumeItems(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLatestItems(userId: String, parentId: String?, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getNextUp(userId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getLibraries(userId: String) = Result.success(emptyList<MediaItem>())
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
        ) = Result.success(emptyList<MediaItem>() to 0)

        override suspend fun getSimilarItems(userId: String, itemId: String, limit: Int) = Result.success(emptyList<MediaItem>())
        override suspend fun getSeasons(userId: String, seriesId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun getEpisodes(userId: String, seriesId: String, seasonId: String?) = Result.success(emptyList<MediaItem>())
        override suspend fun getSpecialFeatures(userId: String, itemId: String) = Result.success(emptyList<MediaItem>())
        override suspend fun markAsPlayed(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun markAsUnplayed(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun markAsFavorite(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun unmarkAsFavorite(userId: String, itemId: String) = Result.success(Unit)
        override suspend fun getLiveTvChannelPreview(userId: String) = Result.success(emptyList<MediaItem>())
    }

    private class FixturePlaybackRepository(private val streamUrl: String) : PlaybackRepository {
        override suspend fun getPlaybackInfo(
            itemId: String,
            userId: String,
            audioStreamIndex: Int?,
            subtitleStreamIndex: Int?,
            startTimeTicks: Long?,
        ) = Result.success(
            PlaybackInfo(
                playSessionId = "syncplay-session",
                mediaSources = listOf(
                    MediaSource(
                        id = "syncplay-source",
                        container = "wav",
                        directStreamUrl = streamUrl,
                        supportsDirectPlay = true,
                    ),
                ),
            ),
        )

        override suspend fun reportPlaybackStart(
            itemId: String,
            playSessionId: String?,
            mediaSourceId: String?,
            audioIndex: Int?,
            subtitleIndex: Int?,
            positionTicks: Long,
        ) = Result.success(Unit)

        override suspend fun reportPlaybackProgress(
            itemId: String,
            playSessionId: String?,
            mediaSourceId: String?,
            audioIndex: Int?,
            subtitleIndex: Int?,
            positionTicks: Long,
            isPaused: Boolean,
        ) = Result.success(Unit)

        override suspend fun reportPlaybackStopped(
            itemId: String,
            playSessionId: String?,
            mediaSourceId: String?,
            positionTicks: Long,
        ) = Result.success(Unit)
    }
}
