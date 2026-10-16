package org.mulletaflix.android

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.junit4.v2.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.lifecycle.ViewModelStore
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf
import okhttp3.OkHttpClient
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.core.api.SessionState
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.designsystem.theme.MulletaFlixTheme
import org.mulletaflix.domain.repository.SyncPlayGroup
import org.mulletaflix.domain.repository.SyncPlayPlaybackCommand
import org.mulletaflix.domain.repository.SyncPlayPlaybackStatus
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.feature.syncplay.SyncPlayScreen
import org.mulletaflix.feature.syncplay.SyncPlayRealtimeConnectionStatus
import org.mulletaflix.feature.syncplay.SyncPlayViewModel
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

@RunWith(AndroidJUnit4::class)
class SyncPlayScreenRealtimeJourneyTest {
    @get:Rule val composeRule = createComposeRule()

    @Test
    fun joiningRoomRendersCommandReceivedByRealWebSocketClient() {
        val server = MockWebServer()
        server.start()
        val socketAccepted = CountDownLatch(1)
        val socketClosed = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    socketAccepted.countDown()
                    webSocket.send(
                        """{"MessageType":"SyncPlayCommand","Data":{"GroupId":"group-1","PlaylistItemId":"item-1","Command":"Pause","PositionTicks":123}}""",
                    )
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    socketClosed.countDown()
                }
            }),
        )

        val sessionRepository = TestSessionRepository(server.url("/").toString().trimEnd('/'))
        val realtimeScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val realtimeClient = SyncPlayRealtimeClient(
            httpClient = OkHttpClient(),
            sessionRepository = sessionRepository,
            applicationScope = realtimeScope,
            moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build(),
        )
        val viewModelStore = ViewModelStore()
        val viewModel = SyncPlayViewModel(
            manageSyncPlayUseCase = ManageSyncPlayUseCase(TestSyncPlayRepository()),
            sessionRepository = sessionRepository,
            realtimeClient = realtimeClient,
        )
        viewModelStore.put("sync-play-realtime-journey", viewModel)

        try {
            composeRule.setContent {
                MulletaFlixTheme { SyncPlayScreen(viewModel = viewModel) }
            }

            composeRule.onNodeWithText("Entrar na sala").performClick()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                socketAccepted.count == 0L &&
                    viewModel.state.value.lastRealtimeEvent == "Pause recebido pelo grupo"
            }
            assertEquals("group-1", viewModel.state.value.activeGroupId)
            assertEquals(true, viewModel.state.value.realtimeConnected)
            composeRule.waitUntil(timeoutMillis = 5_000) {
                composeRule.onAllNodesWithText("Sincronização em tempo real conectada")
                    .fetchSemanticsNodes().isNotEmpty()
            }
            composeRule.onNodeWithText("Sincronização em tempo real conectada").assertIsDisplayed()
        } finally {
            viewModelStore.clear()
            realtimeClient.stop()
            val closedCleanly = socketClosed.await(5, TimeUnit.SECONDS)
            realtimeScope.cancel()
            server.shutdown()
            assertTrue("SyncPlay WebSocket must close before MockWebServer shuts down", closedCleanly)
        }
    }

    @Test
    fun remoteSocketCloseShowsReconnectStateUntilTheClientConnectsAgain() {
        val server = MockWebServer()
        server.start()
        val firstSocketAccepted = CountDownLatch(1)
        val closeFirstSocket = CountDownLatch(1)
        val firstSocketClosed = CountDownLatch(1)
        val secondSocketAccepted = CountDownLatch(1)
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    firstSocketAccepted.countDown()
                    Thread {
                        if (closeFirstSocket.await(10, TimeUnit.SECONDS)) {
                            webSocket.close(1000, "test reconnect")
                        }
                    }.start()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }

                override fun onClosed(webSocket: WebSocket, code: Int, reason: String) {
                    firstSocketClosed.countDown()
                }
            }),
        )
        server.enqueue(
            MockResponse().withWebSocketUpgrade(object : WebSocketListener() {
                override fun onOpen(webSocket: WebSocket, response: Response) {
                    secondSocketAccepted.countDown()
                }

                override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                    webSocket.close(code, reason)
                }

            }),
        )

        val sessionRepository = TestSessionRepository(server.url("/").toString().trimEnd('/'))
        val realtimeScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val realtimeClient = SyncPlayRealtimeClient(
            httpClient = OkHttpClient(),
            sessionRepository = sessionRepository,
            applicationScope = realtimeScope,
            moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build(),
        )
        val viewModelStore = ViewModelStore()
        val viewModel = SyncPlayViewModel(
            manageSyncPlayUseCase = ManageSyncPlayUseCase(TestSyncPlayRepository()),
            sessionRepository = sessionRepository,
            realtimeClient = realtimeClient,
        )
        viewModelStore.put("sync-play-reconnect-journey", viewModel)

        try {
            composeRule.setContent {
                MulletaFlixTheme { SyncPlayScreen(viewModel = viewModel) }
            }
            composeRule.onNodeWithText("Entrar na sala").performClick()
            assertTrue("initial WebSocket must be accepted", firstSocketAccepted.await(10, TimeUnit.SECONDS))
            composeRule.waitUntil(timeoutMillis = 10_000) {
                viewModel.state.value.realtimeConnected &&
                    composeRule.onAllNodesWithText("Sincronização em tempo real conectada")
                        .fetchSemanticsNodes().isNotEmpty()
            }

            closeFirstSocket.countDown()
            composeRule.waitUntil(timeoutMillis = 10_000) {
                !viewModel.state.value.realtimeConnected &&
                    viewModel.state.value.realtimeConnectionStatus == SyncPlayRealtimeConnectionStatus.RECONNECTING
            }
            composeRule.waitUntil(timeoutMillis = 5_000) {
                composeRule.onAllNodesWithText("Reconectando à sincronização em tempo real…")
                    .fetchSemanticsNodes().size == 1
            }
            assertTrue("remote close handshake must finish", firstSocketClosed.await(5, TimeUnit.SECONDS))

            assertTrue("client must establish the replacement WebSocket", secondSocketAccepted.await(15, TimeUnit.SECONDS))
            composeRule.waitUntil(timeoutMillis = 10_000) {
                viewModel.state.value.realtimeConnected &&
                    composeRule.onAllNodesWithText("Sincronização em tempo real conectada")
                        .fetchSemanticsNodes().isNotEmpty()
            }
            assertEquals(
                1,
                composeRule.onAllNodesWithText("Sincronização em tempo real conectada")
                    .fetchSemanticsNodes().size,
            )
            assertEquals(2, server.requestCount)
        } finally {
            viewModelStore.clear()
            realtimeClient.stop()
            realtimeScope.cancel()
            server.shutdown()
        }
    }

    private class TestSyncPlayRepository : SyncPlayRepository {
        private val group = SyncPlayGroup("group-1", "Sala de teste", "Playing", listOf("Raphael"))

        override suspend fun getGroups(): Result<List<SyncPlayGroup>> = Result.success(listOf(group))
        override suspend fun createGroup(name: String): Result<Unit> = Result.success(Unit)
        override suspend fun joinGroup(groupId: String): Result<Unit> = Result.success(Unit)
        override suspend fun leaveGroup(): Result<Unit> = Result.success(Unit)
        override suspend fun sendPlaybackCommand(command: SyncPlayPlaybackCommand): Result<Unit> = Result.success(Unit)
        override suspend fun reportBuffering(status: SyncPlayPlaybackStatus): Result<Unit> = Result.success(Unit)
        override suspend fun reportReady(status: SyncPlayPlaybackStatus): Result<Unit> = Result.success(Unit)
    }

    private class TestSessionRepository(private val serverUrl: String) : SessionRepository {
        override fun getAccessToken(): Flow<String?> = flowOf("synthetic-token")
        override fun getDeviceId(): Flow<String> = flowOf("synthetic-device")
        override fun getBaseUrl(): Flow<String> = flowOf(serverUrl)
        override fun getCurrentUserId(): Flow<String?> = flowOf("synthetic-user")
        override fun getServerId(): Flow<String?> = flowOf("synthetic-server")
        override fun getSessionState(): Flow<SessionState> =
            flowOf(SessionState(serverUrl, "synthetic-token", "synthetic-user", "synthetic-server"))

        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
