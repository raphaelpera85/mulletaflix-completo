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
    @param:ApplicationContext private val context: Context,
) : SettingsRepository {

    private object Keys {
        val THEME = stringPreferencesKey("app_theme")
        val MAX_BITRATE = intPreferencesKey("max_bitrate")
        val PIP_ENABLED = booleanPreferencesKey("pip_enabled")
        val AUDIO_LANG = stringPreferencesKey("preferred_audio_lang")
        val SUBTITLE_LANG = stringPreferencesKey("preferred_subtitle_lang")
        val AUTOPLAY_ENABLED = booleanPreferencesKey("autoplay_enabled")
        val SKIP_INTRO_ENABLED = booleanPreferencesKey("skip_intro_enabled")
        val DEFAULT_QUALITY = stringPreferencesKey("default_quality")
        val DEFAULT_PLAYBACK_SPEED = floatPreferencesKey("default_playback_speed")
        val SUBTITLE_FONT_SIZE = intPreferencesKey("subtitle_font_size")
        val SUBTITLE_COLOR = stringPreferencesKey("subtitle_color")
        val DEFAULT_ASPECT_RATIO = stringPreferencesKey("default_aspect_ratio")
        val LIBRARY_GRID_VIEW_ENABLED = booleanPreferencesKey("library_grid_view_enabled")
        val LIBRARY_GRID_DENSITY = stringPreferencesKey("library_grid_density")
        val DEFAULT_LIBRARY_SORT = stringPreferencesKey("default_library_sort")
        val DEFAULT_LIBRARY_SORT_ORDER = stringPreferencesKey("default_library_sort_order")
        val DEFAULT_LIBRARY_FILTERS = stringSetPreferencesKey("default_library_filters")
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

    override fun isAutoPlayEnabled(): Flow<Boolean> =
        context.settingsDataStore.data.map { it[Keys.AUTOPLAY_ENABLED] ?: true }

    override suspend fun setAutoPlayEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { it[Keys.AUTOPLAY_ENABLED] = enabled }
    }

    override fun isSkipIntroEnabled(): Flow<Boolean> =
        context.settingsDataStore.data.map { it[Keys.SKIP_INTRO_ENABLED] ?: true }

    override suspend fun setSkipIntroEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { it[Keys.SKIP_INTRO_ENABLED] = enabled }
    }

    override fun getDefaultQuality(): Flow<String> =
        context.settingsDataStore.data.map { it[Keys.DEFAULT_QUALITY] ?: "Auto" }

    override suspend fun setDefaultQuality(quality: String) {
        context.settingsDataStore.edit { it[Keys.DEFAULT_QUALITY] = quality }
    }

    override fun getDefaultPlaybackSpeed(): Flow<Float> =
        context.settingsDataStore.data.map { it[Keys.DEFAULT_PLAYBACK_SPEED] ?: 1f }

    override suspend fun setDefaultPlaybackSpeed(speed: Float) {
        context.settingsDataStore.edit { it[Keys.DEFAULT_PLAYBACK_SPEED] = speed.coerceIn(0.5f, 2f) }
    }

    override suspend fun clearLocalPreferences() {
        context.settingsDataStore.edit { it.clear() }
    }

    override fun getSubtitleFontSize(): Flow<Int> =
        context.settingsDataStore.data.map { (it[Keys.SUBTITLE_FONT_SIZE] ?: 100).coerceIn(50, 200) }

    override suspend fun setSubtitleFontSize(size: Int) {
        context.settingsDataStore.edit { it[Keys.SUBTITLE_FONT_SIZE] = size.coerceIn(50, 200) }
    }

    override fun getSubtitleColor(): Flow<String> =
        context.settingsDataStore.data.map { it[Keys.SUBTITLE_COLOR] ?: "WHITE" }

    override suspend fun setSubtitleColor(color: String) {
        val normalized = color.trim().uppercase().takeIf { it in setOf("WHITE", "YELLOW", "CYAN") } ?: "WHITE"
        context.settingsDataStore.edit { it[Keys.SUBTITLE_COLOR] = normalized }
    }

    override fun getDefaultAspectRatio(): Flow<String> =
        context.settingsDataStore.data.map { it[Keys.DEFAULT_ASPECT_RATIO] ?: "FIT" }

    override suspend fun setDefaultAspectRatio(aspectRatio: String) {
        context.settingsDataStore.edit { it[Keys.DEFAULT_ASPECT_RATIO] = aspectRatio.trim().uppercase() }
    }

    override fun isLibraryGridViewEnabled(): Flow<Boolean> =
        context.settingsDataStore.data.map { it[Keys.LIBRARY_GRID_VIEW_ENABLED] ?: true }

    override suspend fun setLibraryGridViewEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { it[Keys.LIBRARY_GRID_VIEW_ENABLED] = enabled }
    }

    override fun getLibraryGridDensity(): Flow<String> =
        context.settingsDataStore.data.map {
            it[Keys.LIBRARY_GRID_DENSITY]
                ?.trim()
                ?.uppercase()
                ?.takeIf { value -> value == "COMFORTABLE" || value == "COMPACT" }
                ?: "COMFORTABLE"
        }

    override suspend fun setLibraryGridDensity(density: String) {
        val normalized = density.trim().uppercase()
            .takeIf { it == "COMFORTABLE" || it == "COMPACT" }
            ?: "COMFORTABLE"
        context.settingsDataStore.edit { it[Keys.LIBRARY_GRID_DENSITY] = normalized }
    }

    override fun getDefaultLibrarySort(): Flow<String> =
        context.settingsDataStore.data.map { it[Keys.DEFAULT_LIBRARY_SORT] ?: "SortName" }

    override suspend fun setDefaultLibrarySort(sortBy: String) {
        context.settingsDataStore.edit { it[Keys.DEFAULT_LIBRARY_SORT] = sortBy.trim().ifBlank { "SortName" } }
    }

    override fun getDefaultLibrarySortOrder(): Flow<String> =
        context.settingsDataStore.data.map {
            it[Keys.DEFAULT_LIBRARY_SORT_ORDER]
                ?.trim()
                ?.takeIf { value -> value.equals("Descending", ignoreCase = true) }
                ?: "Ascending"
        }

    override suspend fun setDefaultLibrarySortOrder(sortOrder: String) {
        val normalized = if (sortOrder.equals("Descending", ignoreCase = true)) "Descending" else "Ascending"
        context.settingsDataStore.edit { it[Keys.DEFAULT_LIBRARY_SORT_ORDER] = normalized }
    }

    override fun getDefaultLibraryFilters(): Flow<Set<String>> =
        context.settingsDataStore.data.map { it[Keys.DEFAULT_LIBRARY_FILTERS] ?: emptySet() }

    override suspend fun setDefaultLibraryFilters(filters: Set<String>) {
        context.settingsDataStore.edit { it[Keys.DEFAULT_LIBRARY_FILTERS] = filters }
    }
}
