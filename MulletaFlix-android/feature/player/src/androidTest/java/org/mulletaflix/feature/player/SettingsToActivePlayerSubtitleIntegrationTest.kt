package org.mulletaflix.feature.player

import android.content.Context
import android.content.ContextWrapper
import android.graphics.Bitmap
import android.graphics.Canvas
import android.net.Uri
import androidx.activity.ComponentActivity
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.ViewModelStore
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.Player
import androidx.media3.common.text.CueGroup
import androidx.media3.common.util.UnstableApi
import androidx.media3.ui.PlayerView
import androidx.media3.ui.SubtitleView
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performScrollTo
import com.squareup.moshi.Moshi
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flowOf
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import okio.Buffer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SyncPlayRealtimeClient
import org.mulletaflix.core.common.network.NetworkMonitor
import org.mulletaflix.data.repository.SessionRepositoryImpl
import org.mulletaflix.data.repository.SettingsRepositoryImpl
import org.mulletaflix.designsystem.subtitle.SUBTITLE_OUTLINE_COLOR
import org.mulletaflix.designsystem.subtitle.subtitleForegroundColor
import org.mulletaflix.domain.model.SUBTITLE_BACKGROUND_BLACK_80
import org.mulletaflix.domain.model.SUBTITLE_COLOR_CYAN
import org.mulletaflix.domain.repository.AuthRepository
import org.mulletaflix.domain.repository.AvailableUser
import org.mulletaflix.domain.repository.DownloadRepository
import org.mulletaflix.domain.repository.MediaRepository
import org.mulletaflix.domain.repository.PlaybackIssueQueue
import org.mulletaflix.domain.repository.PlaybackRepository
import org.mulletaflix.domain.repository.RegistrationResult
import org.mulletaflix.domain.repository.ServerVerification
import org.mulletaflix.domain.repository.SettingsRepository
import org.mulletaflix.domain.repository.SyncPlayRepository
import org.mulletaflix.domain.repository.UserFeedbackRepository
import org.mulletaflix.domain.repository.UserSession
import org.mulletaflix.domain.usecase.GetItemDetailUseCase
import org.mulletaflix.domain.usecase.GetNextEpisodeUseCase
import org.mulletaflix.domain.usecase.LogoutUseCase
import org.mulletaflix.domain.usecase.ManageDownloadsUseCase
import org.mulletaflix.domain.usecase.ManageSyncPlayUseCase
import org.mulletaflix.feature.settings.SettingsScreen
import org.mulletaflix.feature.settings.SettingsViewModel
import java.io.ByteArrayOutputStream
import java.io.File
import java.lang.reflect.Proxy
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.nio.charset.StandardCharsets
import java.util.UUID
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference

@RunWith(AndroidJUnit4::class)
@UnstableApi
class SettingsToActivePlayerSubtitleIntegrationTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun settingsScreenRestylesSubtitleOfCurrentlyPlayingMedia() {
        val server = MockWebServer()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse = when (request.path) {
                "/silence.wav" -> MockResponse()
                    .setHeader("Content-Type", "audio/wav")
                    .setBody(Buffer().write(wavSilence()))
                "/subtitles/active.srt" -> MockResponse()
                    .setHeader("Content-Type", "application/x-subrip; charset=utf-8")
                    .setBody(SRT_CUE)
                else -> MockResponse().setResponseCode(404)
            }
        }
        val appContext = ApplicationProvider.getApplicationContext<Context>()
        val isolatedFilesDir = File(appContext.cacheDir, "settings-player-${UUID.randomUUID()}")
        val isolatedContext = object : ContextWrapper(appContext) {
            override fun getApplicationContext(): Context = this
            override fun getFilesDir(): File = isolatedFilesDir.apply { mkdirs() }
        }
        val sessionRepository = SessionRepositoryImpl(isolatedContext)
        val settingsRepository = SettingsRepositoryImpl(isolatedContext, sessionRepository)
        val authRepository = EmptyAuthRepository()
        val teardownScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
        val viewModelStore = ViewModelStore()
        try {
        server.start()
        val settingsViewModel = SettingsViewModel(
            context = appContext,
            settingsRepository = settingsRepository,
            authRepository = authRepository,
            logoutUseCase = LogoutUseCase(authRepository),
        )
        viewModelStore.put("settings", settingsViewModel)
        val mediaRepository = unusedDependency<MediaRepository>()
        lateinit var playerViewModel: PlayerViewModel
        composeRule.runOnUiThread {
            playerViewModel = PlayerViewModel(
                context = appContext,
                getItemDetailUseCase = GetItemDetailUseCase(mediaRepository),
                playbackRepository = unusedDependency<PlaybackRepository>(),
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
                manageDownloadsUseCase = ManageDownloadsUseCase(unusedDependency<DownloadRepository>()),
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
        viewModelStore.put("player", playerViewModel)

        val subtitleViewRef = AtomicReference<SubtitleView?>()
        val subtitleStyleListener = AtomicReference<Player.Listener?>()
        val cueDisplayed = CountDownLatch(1)
        val playbackError = AtomicReference<androidx.media3.common.PlaybackException?>()
        val cueObserver = object : Player.Listener {
            override fun onCues(cueGroup: CueGroup) {
                if (cueGroup.cues.any { it.text?.toString() == EXPECTED_CUE }) cueDisplayed.countDown()
            }

            override fun onPlayerError(error: androidx.media3.common.PlaybackException) {
                playbackError.set(error)
            }
        }
        playerViewModel.player.addListener(cueObserver)
        val subtitle = MediaItem.SubtitleConfiguration.Builder(
            Uri.parse(server.url("/subtitles/active.srt").toString()),
        )
            .setMimeType(MimeTypes.APPLICATION_SUBRIP)
            .setLanguage("pt-BR")
            .setLabel("Português")
            .setSelectionFlags(C.SELECTION_FLAG_DEFAULT)
            .build()
        val mediaItem = MediaItem.Builder()
            .setUri(server.url("/silence.wav").toString())
            .setSubtitleConfigurations(listOf(subtitle))
            .build()

        try {
            composeRule.setContent {
                val playerState by playerViewModel.state.collectAsStateWithLifecycle()
                Column(Modifier.fillMaxSize()) {
                    AndroidView(
                        modifier = Modifier.fillMaxWidth().height(220.dp),
                        factory = { viewContext ->
                            PlayerView(viewContext).apply {
                                useController = false
                                player = playerViewModel.player
                                subtitleView?.let { activeSubtitleView ->
                                    subtitleViewRef.set(activeSubtitleView)
                                    val listener = userSubtitleCueStyleListener(activeSubtitleView)
                                    subtitleStyleListener.set(listener)
                                    playerViewModel.player.addListener(listener)
                                    listener.onCues(playerViewModel.player.currentCues)
                                }
                            }
                        },
                        update = { playerView ->
                            playerView.subtitleView?.let { activeSubtitleView ->
                                applyUserSubtitlePreferences(
                                    subtitleView = activeSubtitleView,
                                    fontSizePercent = playerState.subtitleFontSize,
                                    style = subtitleCaptionStyle(
                                        foregroundColor = subtitleForegroundColor(playerState.subtitleColor).toArgb(),
                                        edgeColor = SUBTITLE_OUTLINE_COLOR.toArgb(),
                                        backgroundCode = playerState.subtitleBackground,
                                    ),
                                )
                            }
                        },
                        onRelease = { playerView -> playerView.player = null },
                    )
                    Box(Modifier.weight(1f)) {
                        SettingsScreen(onLogout = {}, viewModel = settingsViewModel)
                    }
                }
            }
            composeRule.waitUntil(timeoutMillis = 5_000) { (subtitleViewRef.get()?.width ?: 0) > 0 }
            composeRule.runOnUiThread {
                playerViewModel.player.setMediaItem(mediaItem)
                playerViewModel.player.prepare()
                playerViewModel.player.playWhenReady = true
            }

            assertTrue("Media3 did not render the active SRT cue", cueDisplayed.await(10, TimeUnit.SECONDS))
            assertEquals(null, playbackError.get())
            composeRule.waitUntil(timeoutMillis = 5_000) {
                countBrightGlyphPixels(captureSubtitlePixels(subtitleViewRef)) > 0
            }
            val initialGlyphPixels = countBrightGlyphPixels(captureSubtitlePixels(subtitleViewRef))

            composeRule.onNodeWithText("Tamanho da Fonte").performScrollTo().performClick()
            composeRule.onNodeWithText("150", useUnmergedTree = true).performClick()
            awaitPlayerPreference(playerViewModel) { it.subtitleFontSize == 150 }
            composeRule.waitUntil(timeoutMillis = 5_000) {
                countBrightGlyphPixels(captureSubtitlePixels(subtitleViewRef)) > initialGlyphPixels
            }
            composeRule.runOnIdle {
                assertTrue("A reprodução deve continuar após mudar o tamanho", playerViewModel.player.isPlaying)
            }

            composeRule.onNodeWithText("Cor da Legenda").performScrollTo().performClick()
            composeRule.onNodeWithText("Ciano").performClick()
            awaitPlayerPreference(playerViewModel) { it.subtitleColor == SUBTITLE_COLOR_CYAN }
            composeRule.waitUntil(timeoutMillis = 5_000) {
                countCyanPixels(captureSubtitlePixels(subtitleViewRef)) > 0
            }
            composeRule.runOnIdle {
                assertTrue("A reprodução deve continuar após mudar a cor", playerViewModel.player.isPlaying)
            }

            composeRule.onNodeWithText("Fundo da Legenda").performScrollTo().performClick()
            composeRule.onNodeWithText("Preto 80%").performClick()
            awaitPlayerPreference(playerViewModel) {
                it.subtitleBackground == SUBTITLE_BACKGROUND_BLACK_80
            }
            composeRule.waitUntil(timeoutMillis = 5_000) {
                countBlackBacking(captureSubtitlePixels(subtitleViewRef), 0xCC) > 0
            }
            composeRule.runOnIdle {
                assertEquals(SUBTITLE_BACKGROUND_BLACK_80, playerViewModel.state.value.subtitleBackground)
                assertTrue("A reprodução deve continuar após mudar o fundo", playerViewModel.player.isPlaying)
            }
            assertEquals(null, playbackError.get())
        } finally {
            composeRule.runOnUiThread {
                subtitleStyleListener.getAndSet(null)?.let(playerViewModel.player::removeListener)
                playerViewModel.player.removeListener(cueObserver)
            }
        }
        } finally {
            composeRule.runOnUiThread { viewModelStore.clear() }
            teardownScope.cancel()
            runBlocking {
                settingsRepository.clearLocalPreferences()
                sessionRepository.clearSession()
            }
            isolatedFilesDir.deleteRecursively()
            server.shutdown()
        }
    }

    private fun awaitPlayerPreference(
        viewModel: PlayerViewModel,
        condition: (PlayerState) -> Boolean,
    ) {
        composeRule.waitUntil(timeoutMillis = 5_000) { condition(viewModel.state.value) }
    }

    private fun captureSubtitlePixels(subtitleView: AtomicReference<SubtitleView?>): IntArray {
        val captured = AtomicReference<IntArray>()
        composeRule.runOnUiThread {
            val view = checkNotNull(subtitleView.get())
            val bitmap = Bitmap.createBitmap(view.width, view.height, Bitmap.Config.ARGB_8888)
            try {
                view.draw(Canvas(bitmap))
                val pixels = IntArray(bitmap.width * bitmap.height)
                bitmap.getPixels(pixels, 0, bitmap.width, 0, 0, bitmap.width, bitmap.height)
                captured.set(pixels)
            } finally {
                bitmap.recycle()
            }
        }
        return checkNotNull(captured.get())
    }

    private fun countBlackBacking(pixels: IntArray, alpha: Int): Int = pixels.count {
        android.graphics.Color.alpha(it) == alpha &&
            android.graphics.Color.red(it) == 0 &&
            android.graphics.Color.green(it) == 0 &&
            android.graphics.Color.blue(it) == 0
    }

    private fun countCyanPixels(pixels: IntArray): Int = pixels.count {
        android.graphics.Color.red(it) < 32 &&
            android.graphics.Color.green(it) > 120 &&
            android.graphics.Color.blue(it) > 120
    }

    private fun countBrightGlyphPixels(pixels: IntArray): Int = pixels.count {
        android.graphics.Color.alpha(it) > 32 &&
            android.graphics.Color.red(it) > 160 &&
            android.graphics.Color.green(it) > 160 &&
            android.graphics.Color.blue(it) > 160
    }

    private fun wavSilence(): ByteArray {
        val sampleRate = 8_000
        val sampleCount = sampleRate * 60
        val dataSize = sampleCount * 2
        val output = ByteArrayOutputStream(44 + dataSize)
        val header = ByteBuffer.allocate(44).order(ByteOrder.LITTLE_ENDIAN)
        header.put("RIFF".toByteArray(StandardCharsets.US_ASCII))
        header.putInt(36 + dataSize)
        header.put("WAVE".toByteArray(StandardCharsets.US_ASCII))
        header.put("fmt ".toByteArray(StandardCharsets.US_ASCII))
        header.putInt(16)
        header.putShort(1)
        header.putShort(1)
        header.putInt(sampleRate)
        header.putInt(sampleRate * 2)
        header.putShort(2)
        header.putShort(16)
        header.put("data".toByteArray(StandardCharsets.US_ASCII))
        header.putInt(dataSize)
        output.write(header.array())
        output.write(ByteArray(dataSize))
        return output.toByteArray()
    }

    private class EmptyAuthRepository : AuthRepository {
        override suspend fun verifyServer(url: String) = Result.failure<ServerVerification>(UnsupportedOperationException())
        override suspend fun register(username: String, password: String) = Result.failure<RegistrationResult>(UnsupportedOperationException())
        override suspend fun login(username: String, password: String) = Result.failure<UserSession>(UnsupportedOperationException())
        override suspend fun getAvailableUsers() = Result.success(emptyList<AvailableUser>())
        override suspend fun initiateQuickConnect() = Result.failure<org.mulletaflix.domain.repository.QuickConnectState>(UnsupportedOperationException())
        override suspend fun checkQuickConnect(secret: String) = Result.success<UserSession?>(null)
        override suspend fun logout() = Result.success(Unit)
        override fun getSavedServerUrl() = flowOf("")
        override suspend fun setServerUrl(url: String) = Unit
        override fun getSavedUserId() = flowOf<String?>(null)
        override fun getSavedToken() = flowOf<String?>(null)
    }

    private inline fun <reified T : Any> unusedDependency(): T =
        Proxy.newProxyInstance(
            T::class.java.classLoader,
            arrayOf(T::class.java),
        ) { _, method, _ ->
            throw AssertionError("Unexpected test dependency call: ${T::class.java.simpleName}.${method.name}")
        } as T

    private companion object {
        const val EXPECTED_CUE = "Legenda de teste em reprodução"
        const val SRT_CUE = """
1
00:00:00,000 --> 00:10:00,000
Legenda de teste em reprodução
"""
    }
}
