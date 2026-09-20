package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flowOf

enum class AppThemeSetting {
    Dark,
    Light,
    Netflix,
    PurpleHaze,
    BlueRadiance,
    Wmc,
    AppleTv,
    DynamicColor
}

interface SettingsRepository {
    fun getTheme(): Flow<AppThemeSetting>
    suspend fun setTheme(theme: AppThemeSetting)

    fun getMaxBitrate(): Flow<Int>
    suspend fun setMaxBitrate(bitrate: Int)

    fun isPiPEnabled(): Flow<Boolean>
    suspend fun setPiPEnabled(enabled: Boolean)

    fun getPreferredAudioLanguage(): Flow<String?>
    suspend fun setPreferredAudioLanguage(language: String?)

    fun getPreferredSubtitleLanguage(): Flow<String?>
    suspend fun setPreferredSubtitleLanguage(language: String?)

    fun isAutoPlayEnabled(): Flow<Boolean>
    suspend fun setAutoPlayEnabled(enabled: Boolean)

    fun isSkipIntroEnabled(): Flow<Boolean>
    suspend fun setSkipIntroEnabled(enabled: Boolean)

    fun getDefaultQuality(): Flow<String>
    suspend fun setDefaultQuality(quality: String)

    fun getDefaultPlaybackSpeed(): Flow<Float>
    suspend fun setDefaultPlaybackSpeed(speed: Float)

    suspend fun clearLocalPreferences()

    fun getSubtitleFontSize(): Flow<Int>
    suspend fun setSubtitleFontSize(size: Int)

    fun getSubtitleColor(): Flow<String> = flowOf("WHITE")
    suspend fun setSubtitleColor(color: String) = Unit

    fun getDefaultAspectRatio(): Flow<String>
    suspend fun setDefaultAspectRatio(aspectRatio: String)

    fun isLibraryGridViewEnabled(): Flow<Boolean>
    suspend fun setLibraryGridViewEnabled(enabled: Boolean)

    /** Minimum card width used by the adaptive library grid. */
    fun getLibraryGridDensity(): Flow<String> = flowOf("COMFORTABLE")
    suspend fun setLibraryGridDensity(density: String) = Unit

    fun getDefaultLibrarySort(): Flow<String>
    suspend fun setDefaultLibrarySort(sortBy: String)

    fun getDefaultLibraryFilters(): Flow<Set<String>>
    suspend fun setDefaultLibraryFilters(filters: Set<String>)
}
