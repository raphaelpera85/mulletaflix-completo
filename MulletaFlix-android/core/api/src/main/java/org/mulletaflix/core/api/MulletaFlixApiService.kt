package org.mulletaflix.core.api

import retrofit2.http.*
import org.mulletaflix.core.api.dto.*

/**
 * How many channels one `LiveTv/Channels` request asks for.
 *
 * The list is paged because a single request with `Limit = 100` silently hid
 * every channel beyond the hundredth: a provider with 300 channels showed 100,
 * with nothing on screen saying the rest existed.
 */
const val LIVE_TV_CHANNEL_PAGE_SIZE = 100

/**
 * Safety cap on channel paging, so a server that keeps reporting a larger total
 * than it returns can never loop forever.
 */
const val MAX_LIVE_TV_CHANNEL_PAGES = 40

/**
 * How many programmes one `LiveTv/Programs` request asks for.
 *
 * The guide used to ask for a single page of 50 and ignore `TotalRecordCount`. A
 * provider with a few hundred channels and ten programmes each has thousands of
 * programmes in a 24-hour window, and the server orders them by start date — so the
 * fifty that came back belonged to a handful of channels and the rest of the guide
 * showed "nenhum programa".
 */
const val LIVE_TV_GUIDE_PAGE_SIZE = 100

/** Safety cap on guide paging, per batch of channels. */
const val MAX_LIVE_TV_GUIDE_PAGES = 20

/**
 * How many recordings one `LiveTv/Recordings` request asks for.
 *
 * The list was asked for with `Limit = 20` and no `StartIndex`, so a viewer with 35
 * recordings saw 20 and nothing said the other 15 existed.
 */
const val LIVE_TV_RECORDINGS_PAGE_SIZE = 100

/** Safety cap on recording paging. */
const val MAX_LIVE_TV_RECORDINGS_PAGES = 20

/**
 * MulletaFlix REST API service.
 *
 * Maps all 70 confirmed controllers from Jellyfin.Api/Controllers/ to Kotlin suspend functions.
 * Base URL is injected at runtime from the saved server URL in DataStore.
 *
 * Auth: Bearer token added via [ClientIdentityInterceptor] on every request.
 */
interface MulletaFlixApiService {

    // ── Authentication ──────────────────────────────────────────────────────

    @POST("Users/AuthenticateByName")
    suspend fun authenticateByName(@Body body: AuthenticateByNameDto): AuthenticationResultDto

    @POST("Users/Register")
    suspend fun registerUser(@Body body: RegisterUserDto): RegisterUserResultDto

    @POST("QuickConnect/Initiate")
    suspend fun initiateQuickConnect(): QuickConnectResultDto

    @GET("QuickConnect/Enabled")
    suspend fun isQuickConnectEnabled(): Boolean

    @POST("QuickConnect/Authorize")
    suspend fun authorizeQuickConnect(@Query("code") code: String): Boolean

    @GET("QuickConnect/Connect")
    suspend fun connectQuickConnect(@Query("secret") secret: String): QuickConnectResultDto

    @POST("Users/AuthenticateWithQuickConnect")
    suspend fun authenticateWithQuickConnect(@Body body: QuickConnectDto): AuthenticationResultDto

    // ── Users ───────────────────────────────────────────────────────────────

    @GET("Users")
    suspend fun getUsers(): List<UserDto>

    @GET("Users/Public")
    suspend fun getPublicUsers(): List<UserDto>

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
        @Query("Fields") fields: String? = "ItemCounts",
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
        @Query("Fields") fields: String = "Overview,MediaSources,ItemCounts",
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Backdrop,Thumb",
        @Query("ImageTypeLimit") imageTypeLimit: Int = 1,
    ): BaseItemDtoQueryResultDto

    @GET("Users/{userId}/Items/Latest")
    suspend fun getLatestItems(
        @Path("userId") userId: String,
        @Query("ParentId") parentId: String? = null,
        @Query("Limit") limit: Int = 16,
        @Query("Fields") fields: String = "PrimaryImageAspectRatio,Overview,ItemCounts",
        @Query("EnableImageTypes") enableImageTypes: String = "Primary,Backdrop",
        @Query("ImageTypeLimit") imageTypeLimit: Int = 1,
    ): List<BaseItemDto>

    @GET("Shows/NextUp")
    suspend fun getNextUp(
        @Query("UserId") userId: String,
        @Query("Limit") limit: Int = 12,
        @Query("Fields") fields: String = "Overview,MediaSources,ItemCounts",
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

    /**
     * Opens a live channel that the server marked as requiring it.
     *
     * A tuner channel is not a file: `PlaybackInfo` returns its media source with
     * `RequiresOpening = true` and no `LiveStreamId`, and the stream route only knows
     * where the feed is after this call. The official web client does the same step
     * (`playbackmanager.ts`, `getLiveStream`).
     */
    @POST("LiveStreams/Open")
    suspend fun openLiveStream(
        @Query("UserId") userId: String,
        @Query("ItemId") itemId: String,
        @Query("PlaySessionId") playSessionId: String? = null,
        @Body body: OpenLiveStreamDto,
    ): LiveStreamResponseDto

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
        @Query("StartIndex") startIndex: Int = 0,
        @Query("Limit") limit: Int = LIVE_TV_CHANNEL_PAGE_SIZE,
    ): BaseItemDtoQueryResultDto

    // The server exposes the guide at `LiveTv/Programs`. `LiveTv/EPG` does not
    // exist on any MulletaFlix/Jellyfin release, so this request answered 404 and
    // the whole EPG dialog showed "HTTP 404 Not Found" with no programmes at all.
    // `LiveTvApiContractTest` pins the route so it cannot drift again.
    // The server reads these as "EndDate >= MinEndDate" and "StartDate <=
    // MaxStartDate" (`BaseItemRepository.TranslateQuery.cs`), which is an *overlap*
    // test. `MinStartDate`/`MaxEndDate` are an "inside the window" test and left the
    // programme that is on the air right now out of the guide.
    @GET("LiveTv/Programs")
    suspend fun getEpg(
        @Query("ChannelIds") channelIds: String,
        @Query("StartIndex") startIndex: Int = 0,
        @Query("Limit") limit: Int = LIVE_TV_GUIDE_PAGE_SIZE,
        @Query("MinEndDate") minEndDate: String? = null,
        @Query("MaxStartDate") maxStartDate: String? = null,
    ): BaseItemDtoQueryResultDto

    @GET("LiveTv/Recordings")
    suspend fun getRecordings(
        @Query("UserId") userId: String,
        @Query("StartIndex") startIndex: Int = 0,
        @Query("Limit") limit: Int = LIVE_TV_RECORDINGS_PAGE_SIZE,
    ): BaseItemDtoQueryResultDto

    // Supplies ServiceName and the server's padding policy for a programme.
    @GET("LiveTv/Timers/Defaults")
    suspend fun getLiveTvTimerDefaults(
        @Query("programId") programId: String,
    ): TimerDefaultsDto

    // `IsScheduled=true` is the server's own filter for `Status == New`, i.e. the
    // recordings that are still pending. Used to mark the guide so an already
    // scheduled programme cannot be scheduled a second time.
    @GET("LiveTv/Timers")
    suspend fun getLiveTvTimers(
        @Query("IsScheduled") isScheduled: Boolean? = true,
    ): LiveTvTimerQueryResultDto

    @POST("LiveTv/Timers")
    suspend fun createLiveTvTimer(@Body body: CreateLiveTvTimerDto)

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

    // ── Media Segments (Intro Skipper) ──────────────────────────────────────────

    @GET("MediaSegments/{itemId}")
    suspend fun getMediaSegments(@Path("itemId") itemId: String): MediaSegmentsQueryResultDto
}
