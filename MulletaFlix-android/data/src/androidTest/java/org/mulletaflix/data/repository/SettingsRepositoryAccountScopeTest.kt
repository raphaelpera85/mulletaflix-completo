package org.mulletaflix.data.repository

import android.content.Context
import android.content.ContextWrapper
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import java.io.File
import java.util.UUID
import java.util.concurrent.atomic.AtomicInteger
import org.mulletaflix.domain.repository.AppThemeSetting
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import androidx.test.ext.junit.runners.AndroidJUnit4

@RunWith(AndroidJUnit4::class)
class SettingsRepositoryAccountScopeTest {
    private val targetContext = InstrumentationRegistry.getInstrumentation().targetContext
    private val isolatedFilesDir = File(
        targetContext.cacheDir,
        "settings-profile-scope-${UUID.randomUUID()}",
    )
    private val context = object : ContextWrapper(targetContext) {
        override fun getApplicationContext(): Context = this
        override fun getFilesDir(): File = isolatedFilesDir.apply { mkdirs() }
    }
    private val sessionRepository = SessionRepositoryImpl(context)
    private val settingsRepository = SettingsRepositoryImpl(context, sessionRepository)

    @Before
    fun clearStoredState() = runBlocking {
        isolatedFilesDir.mkdirs()
        settingsRepository.clearLocalPreferences()
        sessionRepository.clearSession()
        sessionRepository.setServerId(null)
    }

    @After
    fun removeStoredState() = runBlocking {
        settingsRepository.clearLocalPreferences()
        sessionRepository.clearSession()
        sessionRepository.setServerId(null)
        isolatedFilesDir.deleteRecursively()
        Unit
    }

    @Test
    fun seekJumpIntervalDefaultsToTenAndNormalizesStoredValues() = runBlocking {
        assertEquals(10, settingsRepository.getSeekJumpSeconds().first())

        settingsRepository.setSeekJumpSeconds(15)
        assertEquals(15, settingsRepository.getSeekJumpSeconds().first())

        settingsRepository.setSeekJumpSeconds(11)
        assertEquals(10, settingsRepository.getSeekJumpSeconds().first())

        context.settingsDataStore.edit { it[intPreferencesKey("seek_jump_interval_seconds")] = 30 }
        assertEquals(30, settingsRepository.getSeekJumpSeconds().first())

        context.settingsDataStore.edit { it[intPreferencesKey("seek_jump_interval_seconds")] = 11 }
        assertEquals(10, settingsRepository.getSeekJumpSeconds().first())
    }

    @Test
    fun audioAndSubtitleChoicesAreIsolatedAcrossUsersAndServers() = runBlocking {
        signIn(userId = "user-a", serverId = "server-one")
        assertEquals("por", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("por", settingsRepository.getPreferredSubtitleLanguage().first())
        settingsRepository.setPreferredAudioLanguage("eng")
        settingsRepository.setPreferredSubtitleLanguage("off")

        signIn(userId = "user-b", serverId = "server-one")
        assertEquals("por", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("por", settingsRepository.getPreferredSubtitleLanguage().first())
        settingsRepository.setPreferredAudioLanguage("spa")
        settingsRepository.setPreferredSubtitleLanguage("fra")

        // A selection callback from the first playback can complete after B is
        // active; it must still be written into the account captured at load.
        val latePreferenceScope = org.mulletaflix.domain.model.UserMediaPreferenceScope(
            userId = "user-a",
            serverId = "server-one",
            serverUrl = "http://mulletaflix.test:8096",
        )
        settingsRepository.setPreferredAudioLanguage(latePreferenceScope, "ita")
        assertEquals("spa", settingsRepository.getPreferredAudioLanguage().first())

        signIn(userId = "user-a", serverId = "server-one")
        assertEquals("ita", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("off", settingsRepository.getPreferredSubtitleLanguage().first())

        signIn(userId = "user-a", serverId = "server-two")
        assertEquals("por", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("por", settingsRepository.getPreferredSubtitleLanguage().first())
    }

    @Test
    fun legacyGlobalChoicesMoveToFirstSignedInProfileOnly() = runBlocking {
        context.settingsDataStore.edit { preferences ->
            preferences[stringPreferencesKey("preferred_audio_lang")] = "deu"
            preferences[stringPreferencesKey("preferred_subtitle_lang")] = "off"
        }

        signIn(userId = "first-user", serverId = "server-one")
        assertEquals("deu", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("off", settingsRepository.getPreferredSubtitleLanguage().first())

        signIn(userId = "second-user", serverId = "server-one")
        assertEquals("por", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("por", settingsRepository.getPreferredSubtitleLanguage().first())
    }

    @Test
    fun seriesTrackChoicesOverrideAccountDefaultsWithoutCrossingSeriesOrProfile() = runBlocking {
        signIn(userId = "user-a", serverId = "server-one")
        settingsRepository.setPreferredAudioLanguage("eng")
        settingsRepository.setPreferredSubtitleLanguage("off")
        val firstSeries = seriesScope("user-a", "server-one", "series-one")
        val secondSeries = seriesScope("user-a", "server-one", "series-two")

        settingsRepository.setPreferredAudioLanguage(firstSeries, "spa")
        settingsRepository.setPreferredSubtitleLanguage(firstSeries, "fra")

        assertEquals("spa", settingsRepository.getPreferredAudioLanguage(firstSeries).first())
        assertEquals("fra", settingsRepository.getPreferredSubtitleLanguage(firstSeries).first())
        assertEquals("eng", settingsRepository.getPreferredAudioLanguage(secondSeries).first())
        assertEquals("off", settingsRepository.getPreferredSubtitleLanguage(secondSeries).first())
        assertEquals("eng", settingsRepository.getPreferredAudioLanguage().first())

        signIn(userId = "user-b", serverId = "server-one")
        val sameSeriesForOtherUser = seriesScope("user-b", "server-one", "series-one")
        assertEquals("por", settingsRepository.getPreferredAudioLanguage(sameSeriesForOtherUser).first())
        assertEquals("por", settingsRepository.getPreferredSubtitleLanguage(sameSeriesForOtherUser).first())
    }

    @Test
    fun clearingSeriesChoicesPreservesAccountDefaultsAndOtherProfiles() = runBlocking {
        signIn(userId = "user-a", serverId = "server-one")
        settingsRepository.setPreferredAudioLanguage("eng")
        settingsRepository.setPreferredSubtitleLanguage("off")
        val firstSeries = seriesScope("user-a", "server-one", "series-one")
        val otherProfileSeries = seriesScope("user-b", "server-one", "series-one")
        settingsRepository.setPreferredAudioLanguage(firstSeries, "spa")
        settingsRepository.setPreferredSubtitleLanguage(firstSeries, "fra")
        settingsRepository.setPreferredAudioLanguage(otherProfileSeries, "ita")
        settingsRepository.setPreferredSubtitleLanguage(otherProfileSeries, "deu")

        settingsRepository.clearSeriesTrackPreferences()

        assertEquals("eng", settingsRepository.getPreferredAudioLanguage(firstSeries).first())
        assertEquals("off", settingsRepository.getPreferredSubtitleLanguage(firstSeries).first())

        signIn(userId = "user-b", serverId = "server-one")
        settingsRepository.setPreferredAudioLanguage("deu")
        settingsRepository.setPreferredSubtitleLanguage("eng")
        assertEquals("ita", settingsRepository.getPreferredAudioLanguage(otherProfileSeries).first())
        assertEquals("deu", settingsRepository.getPreferredSubtitleLanguage(otherProfileSeries).first())
        assertEquals("deu", settingsRepository.getPreferredAudioLanguage().first())
        assertEquals("eng", settingsRepository.getPreferredSubtitleLanguage().first())
    }

    @Test
    fun seriesTrackOverridesAreBoundedToMostRecentlyUsed32Series() = runBlocking {
        signIn(userId = "user-a", serverId = "server-one")
        settingsRepository.setPreferredAudioLanguage("eng")
        val seriesScopes = (1..33).map { number ->
            seriesScope("user-a", "server-one", "series-$number")
        }

        seriesScopes.forEach { scope -> settingsRepository.setPreferredAudioLanguage(scope, "spa") }

        assertEquals("eng", settingsRepository.getPreferredAudioLanguage(seriesScopes.first()).first())
        assertEquals("spa", settingsRepository.getPreferredAudioLanguage(seriesScopes[1]).first())
        assertEquals("spa", settingsRepository.getPreferredAudioLanguage(seriesScopes.last()).first())
    }

    @Test
    fun readingSeriesOverrideRefreshesUseOrderBeforeEvictingOldestSeries() = runBlocking {
        signIn(userId = "user-a", serverId = "server-one")
        settingsRepository.setPreferredAudioLanguage("eng")
        settingsRepository.setPreferredSubtitleLanguage("off")
        val seriesScopes = (1..33).map { number ->
            seriesScope("user-a", "server-one", "series-$number")
        }

        seriesScopes.take(32).forEach { scope ->
            settingsRepository.setPreferredAudioLanguage(scope, "spa")
            settingsRepository.setPreferredSubtitleLanguage(scope, "fra")
        }

        // Playback reads the override for series 1 after series 32 was used.
        assertEquals("spa", settingsRepository.getPreferredAudioLanguage(seriesScopes.first()).first())

        // Adding series 33 must now evict series 2, not the recently played series 1.
        settingsRepository.setPreferredAudioLanguage(seriesScopes.last(), "ita")
        settingsRepository.setPreferredSubtitleLanguage(seriesScopes.last(), "deu")

        assertEquals("spa", settingsRepository.getPreferredAudioLanguage(seriesScopes.first()).first())
        assertEquals("fra", settingsRepository.getPreferredSubtitleLanguage(seriesScopes.first()).first())
        assertEquals("eng", settingsRepository.getPreferredAudioLanguage(seriesScopes[1]).first())
        assertEquals("off", settingsRepository.getPreferredSubtitleLanguage(seriesScopes[1]).first())
        assertEquals("ita", settingsRepository.getPreferredAudioLanguage(seriesScopes.last()).first())
        assertEquals("deu", settingsRepository.getPreferredSubtitleLanguage(seriesScopes.last()).first())
    }

    @Test
    fun activeSeriesPreferenceFlowsDoNotLoopWhenDataStoreUpdates() = runBlocking {
        signIn(userId = "user-a", serverId = "server-one")
        val firstSeries = seriesScope("user-a", "server-one", "series-one")
        val secondSeries = seriesScope("user-a", "server-one", "series-two")
        settingsRepository.setPreferredAudioLanguage(firstSeries, "spa")
        settingsRepository.setPreferredAudioLanguage(secondSeries, "ita")

        val firstCount = AtomicInteger()
        val secondCount = AtomicInteger()
        val firstCollector = launch {
            settingsRepository.getPreferredAudioLanguage(firstSeries).collect { firstCount.incrementAndGet() }
        }
        val secondCollector = launch {
            settingsRepository.getPreferredAudioLanguage(secondSeries).collect { secondCount.incrementAndGet() }
        }

        try {
            delay(300)
            val firstSettledCount = firstCount.get()
            val secondSettledCount = secondCount.get()
            delay(150)

            assertEquals(firstSettledCount, firstCount.get())
            assertEquals(secondSettledCount, secondCount.get())

            settingsRepository.setTheme(AppThemeSetting.Light)
            delay(150)

            assertEquals(firstSettledCount + 1, firstCount.get())
            assertEquals(secondSettledCount + 1, secondCount.get())
        } finally {
            firstCollector.cancel()
            secondCollector.cancel()
        }
    }

    @Test
    fun episodeWithoutSafeSeriesIdDoesNotOverwriteAccountTrackDefault() = runBlocking {
        val account = seriesScope("user-a", "server-one", "series-one").copy(seriesId = null, isEpisode = false)
        settingsRepository.setPreferredAudioLanguage(account, "eng")
        settingsRepository.setPreferredAudioLanguage(account.copy(seriesId = "x".repeat(129), isEpisode = true), "spa")

        assertEquals("eng", settingsRepository.getPreferredAudioLanguage(account).first())
    }

    private fun seriesScope(userId: String, serverId: String, seriesId: String) =
        org.mulletaflix.domain.model.UserMediaPreferenceScope(
            userId = userId,
            serverId = serverId,
            serverUrl = "http://mulletaflix.test:8096",
            seriesId = seriesId,
            isEpisode = true,
        )

    private suspend fun signIn(userId: String, serverId: String) {
        sessionRepository.saveSession(
            serverUrl = "http://mulletaflix.test:8096",
            token = "test-token",
            userId = userId,
            userName = userId,
            serverId = serverId,
            deviceId = "test-device",
        )
    }
}
