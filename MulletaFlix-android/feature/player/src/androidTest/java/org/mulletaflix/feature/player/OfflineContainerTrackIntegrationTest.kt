package org.mulletaflix.feature.player

import android.net.Uri
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import androidx.activity.ComponentActivity
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.media3.common.C
import androidx.media3.common.MediaItem
import androidx.media3.common.MimeTypes
import androidx.media3.common.Player
import androidx.media3.common.util.UnstableApi
import androidx.media3.datasource.cache.NoOpCacheEvictor
import androidx.media3.datasource.cache.SimpleCache
import androidx.media3.database.DefaultDatabaseProvider
import androidx.media3.exoplayer.ExoPlayer
import androidx.media3.exoplayer.offline.Download
import androidx.media3.exoplayer.offline.DownloadManager
import androidx.media3.exoplayer.offline.DownloadRequest
import androidx.media3.exoplayer.scheduler.Requirements
import androidx.media3.exoplayer.source.DefaultMediaSourceFactory
import androidx.media3.exoplayer.trackselection.DefaultTrackSelector
import androidx.compose.ui.test.assertCountEquals
import androidx.compose.ui.test.assertIsFocused
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.test.hasClickAction
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performKeyInput
import androidx.compose.ui.test.pressKey
import androidx.compose.ui.test.requestFocus
import androidx.compose.ui.test.junit4.v2.createAndroidComposeRule
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.SemanticsProperties
import androidx.compose.ui.test.SemanticsMatcher
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okio.Buffer
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File
import java.util.UUID
import java.util.concurrent.Executors
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit
import java.util.concurrent.atomic.AtomicReference
import org.mulletaflix.core.api.cleartextAwareMediaDataSourceFactory

@RunWith(AndroidJUnit4::class)
@UnstableApi
class OfflineContainerTrackIntegrationTest {
    @get:Rule
    val composeRule = createAndroidComposeRule<ComponentActivity>()

    @Test
    fun completedDownloadPlaysFromCacheAndSelectsItsAudioAndSubtitleTracks() {
        val context = ApplicationProvider.getApplicationContext<android.content.Context>()
        val fixture = InstrumentationRegistry.getInstrumentation().context.assets
            .open("offline-multitrack.mkv")
            .use { it.readBytes() }
        val testId = UUID.randomUUID().toString()
        val cacheDirectory = File(context.cacheDir, "offline-track-cache/$testId")
        assertTrue("Unable to create isolated cache directory", cacheDirectory.mkdirs())
        val databaseName = "offline-track-$testId.db"
        val databaseHelper = object : SQLiteOpenHelper(context, databaseName, null, 1) {
            override fun onCreate(database: SQLiteDatabase) = Unit
            override fun onUpgrade(database: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
        }
        val databaseProvider = DefaultDatabaseProvider(databaseHelper)
        val cache = SimpleCache(cacheDirectory, NoOpCacheEvictor(), databaseProvider)
        val executor = Executors.newSingleThreadExecutor()
        val server = MockWebServer()
        server.enqueue(
            MockResponse()
                .setHeader("Content-Type", "video/x-matroska")
                .setBody(Buffer().write(fixture)),
        )
        var downloadManager: DownloadManager? = null
        var player: ExoPlayer? = null
        val downloadId = "offline-multitrack-$testId"
        lateinit var downloadUri: Uri
        val tracksReady = CountDownLatch(1)
        val trackSnapshot = AtomicReference<androidx.media3.common.Tracks?>()
        val playbackError = AtomicReference<androidx.media3.common.PlaybackException?>()
        val selectedAudioIndex = mutableIntStateOf(-1)
        val selectedSubtitleIndex = mutableIntStateOf(-1)
        val listener = object : Player.Listener {
            override fun onTracksChanged(tracks: androidx.media3.common.Tracks) {
                if (tracks.groups.any { it.type == C.TRACK_TYPE_AUDIO } &&
                    tracks.groups.any { it.type == C.TRACK_TYPE_TEXT }
                ) {
                    trackSnapshot.set(tracks)
                    selectedAudioIndex.intValue = selectedOfflineTrackIndex(
                        containerTracksOfType(tracks, C.TRACK_TYPE_AUDIO),
                    )
                    selectedSubtitleIndex.intValue = selectedOfflineTrackIndex(
                        containerTracksOfType(tracks, C.TRACK_TYPE_TEXT),
                    )
                    tracksReady.countDown()
                }
            }

            override fun onPlayerError(error: androidx.media3.common.PlaybackException) {
                playbackError.set(error)
                tracksReady.countDown()
            }
        }
        try {
            server.start()
            downloadUri = Uri.parse(server.url("/offline-multitrack.mkv").toString())
            val activeDownloadManager = DownloadManager(
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
            downloadManager = activeDownloadManager
            val downloadRequest = DownloadRequest.Builder(downloadId, downloadUri)
                .setMimeType(MimeTypes.VIDEO_MATROSKA)
                .build()
            activeDownloadManager.addDownload(downloadRequest)
            awaitCompletedDownload(activeDownloadManager, downloadId)
            assertTrue("Completed download did not populate the playback cache", cache.cacheSpace >= fixture.size)
            assertEquals("Download should fetch the fixture exactly once", 1, server.requestCount)
            server.shutdown()

            val activePlayer = ExoPlayer.Builder(context)
                .setTrackSelector(DefaultTrackSelector(context))
                .setMediaSourceFactory(
                    DefaultMediaSourceFactory(
                        playbackCacheDataSourceFactory(
                            cache,
                            cleartextAwareMediaDataSourceFactory(connectTimeoutMs = 5_000, readTimeoutMs = 5_000),
                        ),
                    ),
                )
                .build()
            player = activePlayer
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                activePlayer.addListener(listener)
                activePlayer.trackSelectionParameters = activePlayer.trackSelectionParameters
                    .buildUpon()
                    .setTrackTypeDisabled(C.TRACK_TYPE_TEXT, true)
                    .build()
                activePlayer.setMediaItem(MediaItem.fromUri(downloadUri))
                activePlayer.prepare()
            }
            assertTrue("Media3 did not discover tracks in the completed cached download", tracksReady.await(10, TimeUnit.SECONDS))
            assertNull("Media3 failed to read the local Matroska fixture: ${playbackError.get()}", playbackError.get())
            assertEquals("Playback must use the completed cache without contacting the source again", 1, server.requestCount)
            val tracks = checkNotNull(trackSnapshot.get())
            val audio = containerTracksOfType(tracks, C.TRACK_TYPE_AUDIO)
            val subtitles = containerTracksOfType(tracks, C.TRACK_TYPE_TEXT)
            assertEquals(2, audio.size)
            assertEquals(1, subtitles.size)
            val audioLanguages = audio.mapNotNull { it.language?.lowercase() }.toSet()
            val subtitleLanguage = subtitles.single().language?.lowercase()
            assertTrue("Unexpected audio languages/labels: ${audio.map { it.language to it.label }}", audioLanguages.any { it in setOf("por", "pt") })
            assertTrue("Unexpected audio languages/labels: ${audio.map { it.language to it.label }}", audioLanguages.any { it in setOf("eng", "en") })
            assertTrue("Unexpected subtitle language: ${subtitles.single()}", subtitleLanguage in setOf("por", "pt"))
            assertTrue("The fixture subtitle must be selectable", tracks.groups
                .filter { it.type == C.TRACK_TYPE_TEXT }
                .any { group -> (0 until group.length).any { group.isTrackSupported(it) } })

            val audioOptions = offlineTrackInfos(audio, "Áudio")
            val subtitleOptions = offlineTrackInfos(subtitles, "Legenda")
            val showSubtitleOptions = mutableStateOf(false)
            composeRule.setContent {
                MaterialTheme {
                    if (showSubtitleOptions.value) {
                        PlayerTrackMenu(
                            title = "Legendas",
                            tracks = subtitleOptions,
                            selectedIndex = selectedSubtitleIndex.intValue,
                            allowNone = true,
                            onSelect = { menuIndex ->
                                val serverIndex = serverTrackIndexAt(subtitleOptions, menuIndex) ?: return@PlayerTrackMenu
                                val parameters = trackSelectionParametersForServerIndex(
                                    currentParameters = activePlayer.trackSelectionParameters,
                                    tracks = activePlayer.currentTracks,
                                    serverIndex = serverIndex,
                                    trackType = C.TRACK_TYPE_TEXT,
                                    orderedServerIndices = offlineStreamIndices(subtitles.size),
                                )
                                if (parameters != null) activePlayer.trackSelectionParameters = parameters
                            },
                            onDismiss = {},
                        )
                    } else {
                        PlayerTrackMenu(
                            title = "Faixa de Áudio",
                            tracks = audioOptions,
                            selectedIndex = selectedAudioIndex.intValue,
                            allowNone = false,
                            onSelect = { menuIndex ->
                                val serverIndex = serverTrackIndexAt(audioOptions, menuIndex) ?: return@PlayerTrackMenu
                                val parameters = trackSelectionParametersForServerIndex(
                                    currentParameters = activePlayer.trackSelectionParameters,
                                    tracks = activePlayer.currentTracks,
                                    serverIndex = serverIndex,
                                    trackType = C.TRACK_TYPE_AUDIO,
                                    orderedServerIndices = offlineStreamIndices(audio.size),
                                )
                                if (parameters != null) activePlayer.trackSelectionParameters = parameters
                            },
                            onDismiss = {},
                        )
                    }
                }
            }

            val portugueseLabel = trackLabel(audioOptions.single { it.language?.lowercase() in setOf("por", "pt") })
            val englishLabel = trackLabel(audioOptions.single { it.language?.lowercase() in setOf("eng", "en") })
            val isTelevision = InstrumentationRegistry.getArguments()
                .getString("expectedDeviceProfile") == "TV"
            composeRule.onAllNodes(hasText(portugueseLabel) and hasClickAction()).assertCountEquals(1)
            val englishOption = composeRule.onNode(hasText(englishLabel) and hasClickAction()).assertIsDisplayed()
            if (isTelevision) {
                englishOption.requestFocus().performKeyInput { pressKey(Key.DirectionCenter) }
            } else {
                englishOption.performClick()
            }
            composeRule.waitUntil(5_000) { selectedAudioIndex.intValue == 1 }
            composeRule.onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText(englishLabel) and
                    SemanticsMatcher.expectValue(SemanticsProperties.Selected, true),
            ).assertIsDisplayed()

            composeRule.runOnIdle { showSubtitleOptions.value = true }
            val subtitleLabel = trackLabel(subtitleOptions.single())
            val subtitleOption = composeRule.onNode(hasText(subtitleLabel) and hasClickAction()).assertIsDisplayed()
            composeRule.onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText("Nenhuma") and
                    SemanticsMatcher.expectValue(SemanticsProperties.Selected, true),
            ).assertIsDisplayed()
            if (isTelevision) {
                subtitleOption.requestFocus().performKeyInput { pressKey(Key.DirectionCenter) }
            } else {
                subtitleOption.performClick()
            }
            composeRule.waitUntil(5_000) { selectedSubtitleIndex.intValue == 0 }
            composeRule.onNode(
                SemanticsMatcher.expectValue(SemanticsProperties.Role, Role.RadioButton) and
                    hasText(subtitleLabel) and
                    SemanticsMatcher.expectValue(SemanticsProperties.Selected, true),
            ).assertIsDisplayed()
        } finally {
            InstrumentationRegistry.getInstrumentation().runOnMainSync {
                player?.removeListener(listener)
                player?.release()
            }
            downloadManager?.release()
            runCatching { server.shutdown() }
            cache.release()
            SimpleCache.delete(cacheDirectory, databaseProvider)
            databaseHelper.close()
            context.deleteDatabase(databaseName)
            executor.shutdownNow()
        }
    }

    private fun awaitCompletedDownload(downloadManager: DownloadManager, downloadId: String) {
        val deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(30)
        while (System.nanoTime() < deadline) {
            val download = downloadManager.downloadIndex.getDownload(downloadId)
            if (download?.state == Download.STATE_COMPLETED) return
            if (download?.state == Download.STATE_FAILED) {
                throw AssertionError("Media3 download failed: ${download.failureReason}")
            }
            Thread.sleep(50)
        }
        val lastState = downloadManager.downloadIndex.getDownload(downloadId)?.state
        throw AssertionError("Timed out waiting for completed media download; last state=$lastState")
    }
}
