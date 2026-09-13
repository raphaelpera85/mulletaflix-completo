package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.*
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.SettingsRepository
import javax.inject.Inject
import javax.inject.Singleton

private val Context.settingsDataStore: DataStore<Preferences> by preferencesDataStore(name = "mulletaflix_settings")

@Singleton
class SettingsRepositoryImpl @Inject constructor(
    @ApplicationContext private val context: Context,
) : SettingsRepository {

    private object Keys {
        val THEME = stringPreferencesKey("app_theme")
        val MAX_BITRATE = intPreferencesKey("max_bitrate")
        val PIP_ENABLED = booleanPreferencesKey("pip_enabled")
        val AUDIO_LANG = stringPreferencesKey("preferred_audio_lang")
        val SUBTITLE_LANG = stringPreferencesKey("preferred_subtitle_lang")
    }

    override fun getTheme(): Flow<AppThemeSetting> {
        return context.settingsDataStore.data.map { pref ->
            val name = pref[Keys.THEME] ?: AppThemeSetting.Dark.name
            runCatching { AppThemeSetting.valueOf(name) }.getOrDefault(AppThemeSetting.Dark)
        }
    }

    override suspend fun setTheme(theme: AppThemeSetting) {
        context.settingsDataStore.edit { it[Keys.THEME] = theme.name }
    }

    override fun getMaxBitrate(): Flow<Int> {
        return context.settingsDataStore.data.map { it[Keys.MAX_BITRATE] ?: 120_000_000 }
    }

    override suspend fun setMaxBitrate(bitrate: Int) {
        context.settingsDataStore.edit { it[Keys.MAX_BITRATE] = bitrate }
    }

    override fun isPiPEnabled(): Flow<Boolean> {
        return context.settingsDataStore.data.map { it[Keys.PIP_ENABLED] ?: true }
    }

    override suspend fun setPiPEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { it[Keys.PIP_ENABLED] = enabled }
    }

    override fun getPreferredAudioLanguage(): Flow<String?> {
        return context.settingsDataStore.data.map { it[Keys.AUDIO_LANG] ?: "por" }
    }

    override suspend fun setPreferredAudioLanguage(language: String?) {
        context.settingsDataStore.edit {
            if (language != null) it[Keys.AUDIO_LANG] = language else it.remove(Keys.AUDIO_LANG)
        }
    }

    override fun getPreferredSubtitleLanguage(): Flow<String?> {
        return context.settingsDataStore.data.map { it[Keys.SUBTITLE_LANG] ?: "por" }
    }

    override suspend fun setPreferredSubtitleLanguage(language: String?) {
        context.settingsDataStore.edit {
            if (language != null) it[Keys.SUBTITLE_LANG] = language else it.remove(Keys.SUBTITLE_LANG)
        }
    }
}
