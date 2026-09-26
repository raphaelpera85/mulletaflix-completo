package org.mulletaflix.data.repository

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.*
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.emitAll
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import org.mulletaflix.core.api.SessionRepository
import org.mulletaflix.domain.model.normalizeSubtitleColor
import org.mulletaflix.domain.model.normalizeSubtitleSizePercent
import org.mulletaflix.domain.model.UserMediaPreferenceScope
import org.mulletaflix.domain.repository.AppThemeSetting
import org.mulletaflix.domain.repository.SettingsRepository
import java.nio.charset.StandardCharsets
import java.security.MessageDigest
import javax.inject.Inject
import javax.inject.Singleton

internal val Context.settingsDataStore: DataStore<Preferences> by preferencesDataStore(name = "mulletaflix_settings")

@Singleton
class SettingsRepositoryImpl @Inject constructor(
    @param:ApplicationContext private val context: Context,
    private val sessionRepository: SessionRepository,
) : SettingsRepository {

    private object Keys {
        val THEME = stringPreferencesKey("app_theme")
        val PIP_ENABLED = booleanPreferencesKey("pip_enabled")
        val AUDIO_LANG = stringPreferencesKey("preferred_audio_lang")
        val SUBTITLE_LANG = stringPreferencesKey("preferred_subtitle_lang")
        val AUTOPLAY_ENABLED = booleanPreferencesKey("autoplay_enabled")
        val SKIP_INTRO_ENABLED = booleanPreferencesKey("skip_intro_enabled")
        val AUTOMATIC_INTRO_SKIP_ENABLED = booleanPreferencesKey("automatic_intro_skip_enabled")
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

    private data class PreferenceScope(val token: String)

    private val currentPreferenceScope: Flow<PreferenceScope?> = combine(
        sessionRepository.getCurrentUserId(),
        sessionRepository.getServerId(),
        sessionRepository.getBaseUrl(),
    ) { userId, serverId, serverUrl -> preferenceScope(userId, serverId, serverUrl) }
        .distinctUntilChanged()

    override fun getTheme(): Flow<AppThemeSetting> {
        return context.settingsDataStore.data.map { pref ->
            val name = pref[Keys.THEME] ?: AppThemeSetting.Dark.name
            runCatching { AppThemeSetting.valueOf(name) }.getOrDefault(AppThemeSetting.Dark)
        }
    }

    override suspend fun setTheme(theme: AppThemeSetting) {
        context.settingsDataStore.edit { it[Keys.THEME] = theme.name }
    }

    override fun isPiPEnabled(): Flow<Boolean> {
        return context.settingsDataStore.data.map { it[Keys.PIP_ENABLED] ?: true }
    }

    override suspend fun setPiPEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { it[Keys.PIP_ENABLED] = enabled }
    }

    @OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
    override fun getPreferredAudioLanguage(): Flow<String?> =
        currentPreferenceScope.flatMapLatest { preferredLanguageFlow(Keys.AUDIO_LANG, it) }

    override fun getPreferredAudioLanguage(scope: UserMediaPreferenceScope): Flow<String?> =
        preferredLanguageFlow(Keys.AUDIO_LANG, preferenceScope(scope.userId, scope.serverId, scope.serverUrl))

    override suspend fun setPreferredAudioLanguage(language: String?) =
        setPreferredLanguage(Keys.AUDIO_LANG, language)

    override suspend fun setPreferredAudioLanguage(scope: UserMediaPreferenceScope, language: String?) =
        setPreferredLanguage(
            Keys.AUDIO_LANG,
            language,
            preferenceScope(scope.userId, scope.serverId, scope.serverUrl),
        )

    @OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
    override fun getPreferredSubtitleLanguage(): Flow<String?> =
        currentPreferenceScope.flatMapLatest { preferredLanguageFlow(Keys.SUBTITLE_LANG, it) }

    override fun getPreferredSubtitleLanguage(scope: UserMediaPreferenceScope): Flow<String?> =
        preferredLanguageFlow(Keys.SUBTITLE_LANG, preferenceScope(scope.userId, scope.serverId, scope.serverUrl))

    override suspend fun setPreferredSubtitleLanguage(language: String?) =
        setPreferredLanguage(Keys.SUBTITLE_LANG, language)

    override suspend fun setPreferredSubtitleLanguage(scope: UserMediaPreferenceScope, language: String?) =
        setPreferredLanguage(
            Keys.SUBTITLE_LANG,
            language,
            preferenceScope(scope.userId, scope.serverId, scope.serverUrl),
        )

    @OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
    private fun preferredLanguageFlow(
        key: Preferences.Key<String>,
        scope: PreferenceScope?,
    ): Flow<String?> = if (scope == null) {
        context.settingsDataStore.data.map { it[key] ?: "por" }
    } else {
        flow {
            migrateLegacyLanguagePreferences(scope)
            emitAll(context.settingsDataStore.data.map { preferences ->
                preferences[scopedLanguageKey(key, scope)] ?: "por"
            })
        }
    }

    private suspend fun setPreferredLanguage(key: Preferences.Key<String>, language: String?) {
        setPreferredLanguage(key, language, currentPreferenceScope.first())
    }

    private suspend fun setPreferredLanguage(
        key: Preferences.Key<String>,
        language: String?,
        scope: PreferenceScope?,
    ) {
        if (scope != null) migrateLegacyLanguagePreferences(scope)
        context.settingsDataStore.edit { preferences ->
            if (scope == null) {
                if (language == null) preferences.remove(key) else preferences[key] = language
            } else {
                val scopedKey = scopedLanguageKey(key, scope)
                if (language == null) preferences.remove(scopedKey) else preferences[scopedKey] = language
            }
        }
    }

    private fun preferenceScope(userId: String?, serverId: String?, serverUrl: String): PreferenceScope? {
        val cleanUserId = userId?.trim()?.takeIf { it.isNotEmpty() } ?: return null
        val serverIdentity = serverId?.trim()?.takeIf { it.isNotEmpty() }
            ?: serverUrl.trim().trimEnd('/').lowercase().takeIf { it.isNotEmpty() }
            ?: return null
        return PreferenceScope(scopeToken(serverIdentity, cleanUserId))
    }

    private suspend fun migrateLegacyLanguagePreferences(scope: PreferenceScope) {
        context.settingsDataStore.edit { preferences ->
            listOf(Keys.AUDIO_LANG, Keys.SUBTITLE_LANG).forEach { key ->
                val legacyValue = preferences[key]
                preferences.remove(key)
                if (legacyValue == null) return@forEach
                val scopedKey = scopedLanguageKey(key, scope)
                if (scopedKey !in preferences) preferences[scopedKey] = legacyValue
            }
        }
    }

    private fun scopedLanguageKey(
        key: Preferences.Key<String>,
        scope: PreferenceScope,
    ): Preferences.Key<String> = stringPreferencesKey("${key.name}_${scope.token}")

    private fun scopeToken(serverIdentity: String, userId: String): String {
        val digest = MessageDigest.getInstance("SHA-256")
            .digest("$serverIdentity\u0000$userId".toByteArray(StandardCharsets.UTF_8))
        val alphabet = "0123456789abcdef"
        return buildString(digest.size * 2) {
            digest.forEach { byte ->
                val value = byte.toInt() and 0xff
                append(alphabet[value ushr 4])
                append(alphabet[value and 0x0f])
            }
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

    override fun isAutomaticIntroSkipEnabled(): Flow<Boolean> =
        context.settingsDataStore.data.map { it[Keys.AUTOMATIC_INTRO_SKIP_ENABLED] ?: false }

    override suspend fun setAutomaticIntroSkipEnabled(enabled: Boolean) {
        context.settingsDataStore.edit { it[Keys.AUTOMATIC_INTRO_SKIP_ENABLED] = enabled }
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
        context.settingsDataStore.data.map {
            // The clamp comes from `:domain` rather than being repeated here. While the
            // `50..200` bound was written down in this file too, widening the range in
            // the UI would have left this layer silently truncating what was stored.
            normalizeSubtitleSizePercent(it[Keys.SUBTITLE_FONT_SIZE] ?: 100)
        }

    override suspend fun setSubtitleFontSize(size: Int) {
        context.settingsDataStore.edit {
            it[Keys.SUBTITLE_FONT_SIZE] = normalizeSubtitleSizePercent(size)
        }
    }

    override fun getSubtitleColor(): Flow<String> =
        context.settingsDataStore.data.map { normalizeSubtitleColor(it[Keys.SUBTITLE_COLOR]) }

    override suspend fun setSubtitleColor(color: String) {
        // Same reason: the accepted set is defined once, in `:domain`.
        context.settingsDataStore.edit { it[Keys.SUBTITLE_COLOR] = normalizeSubtitleColor(color) }
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
