package org.mulletaflix.core.api.dto

import com.squareup.moshi.Json
import com.squareup.moshi.JsonClass

@JsonClass(generateAdapter = true)
data class SearchHintResultDto(
    @Json(name = "SearchHints") val searchHints: List<SearchHintDto> = emptyList(),
    @Json(name = "TotalRecordCount") val totalRecordCount: Int = 0,
)

@JsonClass(generateAdapter = true)
data class SearchHintDto(
    @Json(name = "ItemId") val itemId: String,
    @Json(name = "Id") val id: String? = null,
    @Json(name = "Name") val name: String,
    @Json(name = "Type") val type: String? = null,
    @Json(name = "ProductionYear") val productionYear: Int? = null,
    @Json(name = "PrimaryImageTag") val primaryImageTag: String? = null,
    @Json(name = "Series") val series: String? = null,
    @Json(name = "Album") val album: String? = null,
    @Json(name = "AlbumArtist") val albumArtist: String? = null,
    @Json(name = "ChannelId") val channelId: String? = null,
)

@JsonClass(generateAdapter = true)
data class SystemInfoDto(
    @Json(name = "SystemUpdateLevel") val systemUpdateLevel: String? = null,
    @Json(name = "OperatingSystem") val operatingSystem: String? = null,
    @Json(name = "Id") val id: String? = null,
    @Json(name = "ServerName") val serverName: String? = null,
    @Json(name = "Version") val version: String? = null,
    @Json(name = "ProductName") val productName: String? = null,
    @Json(name = "WebSocketPortNumber") val webSocketPortNumber: Int? = null,
    @Json(name = "CanSelfRestart") val canSelfRestart: Boolean = false,
    @Json(name = "CanLaunchWebBrowser") val canLaunchWebBrowser: Boolean = false,
    @Json(name = "ProgramDataPath") val programDataPath: String? = null,
    @Json(name = "WebPath") val webPath: String? = null,
)

@JsonClass(generateAdapter = true)
data class PublicSystemInfoDto(
    @Json(name = "LocalAddress") val localAddress: String? = null,
    @Json(name = "ServerName") val serverName: String? = null,
    @Json(name = "Version") val version: String? = null,
    @Json(name = "ProductName") val productName: String? = null,
    @Json(name = "OperatingSystem") val operatingSystem: String? = null,
    @Json(name = "Id") val id: String? = null,
    @Json(name = "StartupWizardCompleted") val startupWizardCompleted: Boolean = true,
)

@JsonClass(generateAdapter = true)
data class BrandingOptionsDto(
    @Json(name = "LoginDisclaimer") val loginDisclaimer: String? = null,
    @Json(name = "CustomCss") val customCss: String? = null,
    @Json(name = "SplashscreenEnabled") val splashscreenEnabled: Boolean = true,
)

@JsonClass(generateAdapter = true)
data class LyricsDto(
    @Json(name = "Lyrics") val lyrics: List<LyricLineDto> = emptyList(),
)

@JsonClass(generateAdapter = true)
data class LyricLineDto(
    @Json(name = "Text") val text: String,
    @Json(name = "Start") val start: Long? = null,
)

@JsonClass(generateAdapter = true)
data class NewGroupRequestDto(
    @Json(name = "GroupName") val groupName: String,
)

@JsonClass(generateAdapter = true)
data class JoinGroupRequestDto(
    @Json(name = "GroupId") val groupId: String,
)

@JsonClass(generateAdapter = true)
data class GroupInfoDto(
    @Json(name = "GroupId") val groupId: String,
    @Json(name = "GroupName") val groupName: String,
    @Json(name = "State") val state: String? = null, // "Playing", "Paused", "Idle"
    @Json(name = "Participants") val participants: List<String> = emptyList(),
)

// `PlayingItemId` and `PositionTicks` used to live here. The server never sends
// them: `MediaBrowser.Model/SyncPlay/GroupInfoDto.cs` has no such members, and
// `Group.cs` builds the DTO from GroupId/GroupName/State/Participants/LastUpdatedAt/
// Ping/Host only. Declaring them made the app compile a playback path that could
// never run.
