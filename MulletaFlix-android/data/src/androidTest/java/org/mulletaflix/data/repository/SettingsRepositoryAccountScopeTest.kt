package org.mulletaflix.data.repository

import android.content.Context
import android.content.ContextWrapper
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import java.io.File
import java.util.UUID
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
