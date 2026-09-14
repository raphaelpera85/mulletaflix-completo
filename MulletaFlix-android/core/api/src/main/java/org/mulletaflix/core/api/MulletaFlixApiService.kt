package org.mulletaflix.core.api

import retrofit2.http.*
import org.mulletaflix.core.api.dto.*

/**
 * MulletaFlix REST API service.
 *
 * Maps all 70 confirmed controllers from Jellyfin.Api/Controllers/ to Kotlin suspend functions.
 * Base URL is injected at runtime from the saved server URL in DataStore.
 *
 * Auth: Bearer token added via [AuthInterceptor] on every request.
 */
interface MulletaFlixApiService {

    // ── Authentication ──────────────────────────────────────────────────────

    @POST("Users/AuthenticateByName")
    suspend fun authenticateByName(@Body body: AuthenticateByNameDto): AuthenticationResultDto

    @POST("Users/Register")
    suspend fun registerUser(@Body body: RegisterUserDto): RegisterUserResultDto

    @GET("QuickConnect/Initiate")
    suspend fun initiateQuickConnect(): QuickConnectResultDto

    @POST("QuickConnect/Authorize")
    suspend fun authorizeQuickConnect(@Query("code") code: String): Boolean

    @POST("QuickConnect/Connect")
    suspend fun connectQuickConnect(@Body body: QuickConnectDto): AuthenticationResultDto

    // ── Users ───────────────────────────────────────────────────────────────

    @GET("Users")
    suspend fun getUsers(): List<UserDto>

    @GET("Users/{userId}")
    suspend fun getUser(@Path("userId") userId: String): UserDto

    @GET("Users/Me")
    suspend fun getCurrentUser(): UserDto

    // ── Views / Libraries ────────────────────────────────────────────────────

    @GET("Users/{userId}/Views")
    suspend fun getUserViews(@Path("userId") userId: String): BaseItemDtoQueryResultDto

    // ── Items ───────────────────────────────────────────────────────────────

    @GET("Users/{userId}/Items")
    suspend fun getItems(
        @Path("userId") userId: String,
        @Query("ParentId") parentId: String? = null,
        @Query("IncludeItemTypes") includeItemTypes: String? = null,
        @Query("SortBy") sortBy: String? = null,
        @Query("SortOrder") sortOrder: String? = null,
        @Query("Filters") filters: String? = null,
        @Query("Recursive") recursive: Boolean = true,
        @Query("Fields") fields: String? = null,
        @Query("StartIndex") startIndex: Int? = null,
        @Query("Limit") limit: Int? = null,
        @Query("SearchTerm") searchTerm: String? = null,
        @Query("Genres") genres: String? = null,
        @Query("Years") years: String? = null,
        @Query("OfficialRatings") officialRatings: String? = null,
        @Query("IsPlayed") isPlayed: Boolean? = null,
        @Query("IsFavorite") isFavorite: Boolean? = null,
        @Query("ImageTypeLimit") imageTypeLimit: Int = 1,
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Backdrop,Thumb",
    ): BaseItemDtoQueryResultDto

    @GET("Users/{userId}/Items/Resume")
    suspend fun getResumeItems(
        @Path("userId") userId: String,
        @Query("Limit") limit: Int = 12,
        @Query("Fields") fields: String = "Overview,MediaSources",
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Backdrop,Thumb",
        @Query("ImageTypeLimit") imageTypeLimit: Int = 1,
    ): BaseItemDtoQueryResultDto

    @GET("Users/{userId}/Items/Latest")
    suspend fun getLatestItems(
        @Path("userId") userId: String,
        @Query("ParentId") parentId: String? = null,
        @Query("Limit") limit: Int = 16,
        @Query("Fields") fields: String = "PrimaryImageAspectRatio,Overview",
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Backdrop",
        @Query("ImageTypeLimit") imageTypeLimit: Int = 1,
    ): List<BaseItemDto>

    @GET("Shows/NextUp")
    suspend fun getNextUp(
        @Query("UserId") userId: String,
        @Query("Limit") limit: Int = 12,
        @Query("Fields") fields: String = "Overview,MediaSources",
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Thumb",
        @Query("ImageTypeLimit") imageTypeLimit: Int = 1,
    ): BaseItemDtoQueryResultDto

    // ── Item Detail ─────────────────────────────────────────────────────────

    @GET("Users/{userId}/Items/{itemId}")
    suspend fun getItem(
        @Path("userId") userId: String,
        @Path("itemId") itemId: String,
    ): BaseItemDto

    @GET("Items/{itemId}/Similar")
    suspend fun getSimilarItems(
        @Path("itemId") itemId: String,
        @Query("UserId") userId: String,
        @Query("Limit") limit: Int = 12,
        @Query("Fields") fields: String = "PrimaryImageAspectRatio",
    ): BaseItemDtoQueryResultDto

    @GET("Items/{itemId}/SpecialFeatures")
    suspend fun getSpecialFeatures(
        @Path("itemId") itemId: String,
        @Query("UserId") userId: String,
    ): List<BaseItemDto>

    @GET("Shows/{seriesId}/Seasons")
    suspend fun getSeasons(
        @Path("seriesId") seriesId: String,
        @Query("UserId") userId: String,
        @Query("Fields") fields: String = "Overview",
    ): BaseItemDtoQueryResultDto

    @GET("Shows/{seriesId}/Episodes")
    suspend fun getEpisodes(
        @Path("seriesId") seriesId: String,
        @Query("UserId") userId: String,
        @Query("SeasonId") seasonId: String? = null,
        @Query("Fields") fields: String = "Overview,MediaSources",
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Thumb",
    ): BaseItemDtoQueryResultDto

    // ── Playstate ───────────────────────────────────────────────────────────

    @POST("Users/{userId}/PlayedItems/{itemId}")
    suspend fun markAsPlayed(
        @Path("userId") userId: String,
        @Path("itemId") itemId: String,
    ): UserItemDataDto

    @DELETE("Users/{userId}/PlayedItems/{itemId}")
    suspend fun markAsUnplayed(
        @Path("userId") userId: String,
        @Path("itemId") itemId: String,
    ): UserItemDataDto

    @POST("Users/{userId}/FavoriteItems/{itemId}")
    suspend fun markAsFavorite(
        @Path("userId") userId: String,
        @Path("itemId") itemId: String,
    ): UserItemDataDto

    @DELETE("Users/{userId}/FavoriteItems/{itemId}")
    suspend fun unmarkAsFavorite(
        @Path("userId") userId: String,
        @Path("itemId") itemId: String,
    ): UserItemDataDto

    // ── Playlists ───────────────────────────────────────────────────────────

    @GET("Users/{userId}/Items")
    suspend fun getPlaylists(
        @Path("userId") userId: String,
        @Query("IncludeItemTypes") includeItemTypes: String = "Playlist",
        @Query("SortBy") sortBy: String = "SortName",
        @Query("SortOrder") sortOrder: String = "Ascending",
        @Query("Recursive") recursive: Boolean = true,
    ): BaseItemDtoQueryResultDto

    @POST("Playlists")
    suspend fun createPlaylist(
        @Query("Name") name: String,
        @Query("UserId") userId: String,
        @Query("Ids") ids: String? = null,
    ): PlaylistCreationResultDto

    @POST("Playlists/{playlistId}/Items")
    suspend fun addItemToPlaylist(
        @Path("playlistId") playlistId: String,
        @Query("Ids") ids: String,
        @Query("UserId") userId: String,
    )

    // ── Playback session reporting ───────────────────────────────────────────

    @POST("Sessions/Playing")
    suspend fun reportPlaybackStart(@Body body: PlaybackStartInfoDto)

    @POST("Sessions/Playing/Progress")
    suspend fun reportPlaybackProgress(@Body body: PlaybackProgressInfoDto)

    @POST("Sessions/Playing/Stopped")
    suspend fun reportPlaybackStopped(@Body body: PlaybackStopInfoDto)

    // ── Media Info ───────────────────────────────────────────────────────────

    @POST("Items/{itemId}/PlaybackInfo")
    suspend fun getPlaybackInfo(
        @Path("itemId") itemId: String,
        @Query("UserId") userId: String,
        @Body body: PlaybackInfoRequestDto,
    ): PlaybackInfoResponseDto

    // ── Search ───────────────────────────────────────────────────────────────

    @GET("Search/Hints")
    suspend fun searchHints(
        @Query("SearchTerm") searchTerm: String,
        @Query("UserId") userId: String? = null,
        @Query("Limit") limit: Int = 20,
        @Query("IncludeItemTypes") includeItemTypes: String? = null,
    ): SearchHintResultDto

    // ── Live TV ─────────────────────────────────────────────────────────────

    @GET("LiveTv/Channels")
    suspend fun getLiveTvChannels(
        @Query("UserId") userId: String,
        @Query("EnableImageTypes") enableImageTypes: String = "Primary",
        @Query("Fields") fields: String = "Overview",
        @Query("Limit") limit: Int = 100,
    ): BaseItemDtoQueryResultDto

    @GET("LiveTv/EPG")
    suspend fun getEpg(
        @Query("ChannelIds") channelIds: String,
        @Query("StartIndex") startIndex: Int = 0,
        @Query("Limit") limit: Int = 50,
        @Query("MinStartDate") minStartDate: String? = null,
        @Query("MaxEndDate") maxEndDate: String? = null,
    ): BaseItemDtoQueryResultDto

    @GET("LiveTv/Recordings")
    suspend fun getRecordings(
        @Query("UserId") userId: String,
        @Query("Limit") limit: Int = 20,
    ): BaseItemDtoQueryResultDto

    // ── Lyrics ───────────────────────────────────────────────────────────────

    @GET("Audio/{itemId}/Lyrics")
    suspend fun getLyrics(@Path("itemId") itemId: String): LyricsDto

    // ── Images ────────────────────────────────────────────────────────────────

    /**
     * Returns the image URL (not the binary — Coil fetches the binary directly).
     * Builds the URL string client-side using [imageUrl] extension.
     */
    @GET("Items/{itemId}/Images/{imageType}")
    suspend fun getItemImage(
        @Path("itemId") itemId: String,
        @Path("imageType") imageType: String,
        @Query("Tag") tag: String? = null,
        @Query("Quality") quality: Int = 90,
        @Query("MaxWidth") maxWidth: Int? = null,
    ): okhttp3.ResponseBody

    // ── System ────────────────────────────────────────────────────────────────

    @GET("System/Info")
    suspend fun getSystemInfo(): SystemInfoDto

    @GET("System/Info/Public")
    suspend fun getPublicSystemInfo(): PublicSystemInfoDto

    @GET("Health")
    suspend fun getHealth(): Map<String, String>

    // ── Branding ──────────────────────────────────────────────────────────────

    @GET("Branding/Configuration")
    suspend fun getBrandingConfig(): BrandingOptionsDto

    // ── Suggestions ────────────────────────────────────────────────────────────

    @GET("Items/{itemId}/Suggestions")
    suspend fun getSuggestions(
        @Path("itemId") itemId: String,
        @Query("UserId") userId: String,
        @Query("Limit") limit: Int = 12,
    ): BaseItemDtoQueryResultDto

    // ── SyncPlay ───────────────────────────────────────────────────────────────

    @POST("SyncPlay/New")
    suspend fun createSyncPlayGroup(@Body body: NewGroupRequestDto)

    @POST("SyncPlay/Join")
    suspend fun joinSyncPlayGroup(@Body body: JoinGroupRequestDto)

    @POST("SyncPlay/Leave")
    suspend fun leaveSyncPlayGroup()

    @GET("SyncPlay/List")
    suspend fun getSyncPlayGroups(): List<GroupInfoDto>

    // ── Subtitles ─────────────────────────────────────────────────────────────

    @GET("Items/{itemId}/Subtitles/{index}/Stream")
    suspend fun getSubtitleStream(
        @Path("itemId") itemId: String,
        @Path("index") index: Int,
        @Query("MediaSourceId") mediaSourceId: String? = null,
    ): okhttp3.ResponseBody
}
