package org.mulletaflix.domain.repository

import kotlinx.coroutines.flow.Flow

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
}
