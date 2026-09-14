package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

@JsonClass(generateAdapter = true)
data class AuthenticateByNameDto(
    @Json(name = "Username") val username: String,
    @Json(name = "Pw") val pw: String? = null,
)

@JsonClass(generateAdapter = true)
data class RegisterUserDto(
    @Json(name = "Name") val name: String,
    @Json(name = "Password") val password: String,
)

@JsonClass(generateAdapter = true)
data class RegisterUserResultDto(
    @Json(name = "Success") val success: Boolean = false,
    @Json(name = "Message") val message: String? = null,
)

@JsonClass(generateAdapter = true)
data class QuickConnectDto(
    @Json(name = "Secret") val secret: String,
)

@JsonClass(generateAdapter = true)
data class QuickConnectResultDto(
    @Json(name = "Code") val code: String,
    @Json(name = "Secret") val secret: String,
    @Json(name = "Authentication") val authentication: String? = null,
    @Json(name = "Authenticated") val authenticated: Boolean = false,
)

@JsonClass(generateAdapter = true)
data class AuthenticationResultDto(
    @Json(name = "AccessToken") val accessToken: String? = null,
    @Json(name = "ServerId") val serverId: String? = null,
    @Json(name = "User") val user: UserDto? = null,
    @Json(name = "SessionInfo") val sessionInfo: SessionInfoDto? = null,
)

@JsonClass(generateAdapter = true)
data class UserDto(
    @Json(name = "Id") val id: String,
    @Json(name = "Name") val name: String,
    @Json(name = "ServerId") val serverId: String? = null,
    @Json(name = "HasPassword") val hasPassword: Boolean = false,
    @Json(name = "HasConfiguredPassword") val hasConfiguredPassword: Boolean = false,
    @Json(name = "HasConfiguredEasyPassword") val hasConfiguredEasyPassword: Boolean = false,
    @Json(name = "EnableAutoLogin") val enableAutoLogin: Boolean? = null,
    @Json(name = "PrimaryImageTag") val primaryImageTag: String? = null,
    @Json(name = "Configuration") val configuration: UserConfigurationDto? = null,
    @Json(name = "Policy") val policy: UserPolicyDto? = null,
)

@JsonClass(generateAdapter = true)
data class UserConfigurationDto(
    @Json(name = "AudioLanguagePreference") val audioLanguagePreference: String? = null,
    @Json(name = "PlayDefaultAudioTrack") val playDefaultAudioTrack: Boolean = true,
    @Json(name = "SubtitleLanguagePreference") val subtitleLanguagePreference: String? = null,
)

@JsonClass(generateAdapter = true)
data class UserPolicyDto(
    @Json(name = "IsAdministrator") val isAdministrator: Boolean = false,
    @Json(name = "IsHidden") val isHidden: Boolean = false,
    @Json(name = "IsDisabled") val isDisabled: Boolean = false,
    @Json(name = "EnableContentDownloading") val enableContentDownloading: Boolean = true,
    @Json(name = "EnableLiveTvAccess") val enableLiveTvAccess: Boolean = true,
    @Json(name = "EnableMediaPlayback") val enableMediaPlayback: Boolean = true,
)

@JsonClass(generateAdapter = true)
data class SessionInfoDto(
    @Json(name = "Id") val id: String? = null,
    @Json(name = "UserId") val userId: String? = null,
    @Json(name = "UserName") val userName: String? = null,
    @Json(name = "Client") val client: String? = null,
    @Json(name = "DeviceName") val deviceName: String? = null,
    @Json(name = "DeviceId") val deviceId: String? = null,
    @Json(name = "ApplicationVersion") val applicationVersion: String? = null,
)
