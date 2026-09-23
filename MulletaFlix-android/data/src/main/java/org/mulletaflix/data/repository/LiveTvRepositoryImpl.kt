package org.mulletaflix.data.repository

import org.mulletaflix.core.api.LIVE_TV_CHANNEL_PAGE_SIZE
import org.mulletaflix.core.api.LIVE_TV_GUIDE_PAGE_SIZE
import org.mulletaflix.core.api.LIVE_TV_RECORDINGS_PAGE_SIZE
import org.mulletaflix.core.api.MAX_LIVE_TV_CHANNEL_PAGES
import org.mulletaflix.core.api.MAX_LIVE_TV_GUIDE_PAGES
import org.mulletaflix.core.api.MAX_LIVE_TV_RECORDINGS_PAGES
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.CreateLiveTvTimerDto
import org.mulletaflix.data.mapper.toDomain
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.paging.hasMorePages
import org.mulletaflix.domain.repository.LiveTvRepository
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Channels per guide request.
 *
 * Each id is a 32-character GUID, so 100 of them make a query string of roughly
 * 3.3 KB — comfortably inside Kestrel's default 8 KB request-line limit. The batch
 * exists because the channel list is no longer capped, so the joined id string
 * would otherwise grow with the provider's catalogue and eventually answer 414.
 */
internal const val LIVE_TV_CHANNEL_BATCH_SIZE = 100

/**
 * Whether the list has another page when the server may not report a total.
 *
 * `TotalRecordCount` is a non-null `Int` that defaults to `0` when the server omits it,
 * so zero has to mean "unknown" rather than "nothing to paginate" — otherwise a server
 * that does not send the field would silently truncate at the first page. With no total,
 * a full page means there may be more and a short page means the end.
 */
internal fun hasMorePagesWithUnknownTotal(
    loadedItemCount: Int,
    receivedItemCount: Int,
    totalItemCount: Int,
    pageSize: Int,
): Boolean = if (totalItemCount > 0) {
    hasMorePages(loadedItemCount, receivedItemCount, totalItemCount)
} else {
    receivedItemCount >= pageSize
}

/** The channel list's rule; see [hasMorePagesWithUnknownTotal]. */
internal fun hasMoreChannelPages(
    loadedItemCount: Int,
    receivedItemCount: Int,
    totalItemCount: Int,
): Boolean = hasMorePagesWithUnknownTotal(
    loadedItemCount = loadedItemCount,
    receivedItemCount = receivedItemCount,
    totalItemCount = totalItemCount,
    pageSize = LIVE_TV_CHANNEL_PAGE_SIZE,
)

@Singleton
class LiveTvRepositoryImpl @Inject constructor(
    private val api: MulletaFlixApiService,
) : LiveTvRepository {

    override suspend fun getChannels(userId: String): Result<List<MediaItem>> = suspendRunCatching {
        fetchAllChannels(userId).map { it.toDomain() }
    }

    /**
     * Reads every page of channels.
     *
     * One request with `Limit = 100` was the whole catalogue as far as the app was
     * concerned, and it never looked at `TotalRecordCount`, so a provider with 300
     * channels showed the first 100 and gave no hint the rest existed. Paging here
     * rather than in the screen keeps the Live TV list, its TV focus handling and
     * the guide's channel-id set unchanged.
     *
     * The loop is bounded twice: it stops when the reported total is reached or an
     * empty page arrives ([hasMorePages]), and at [MAX_LIVE_TV_CHANNEL_PAGES]
     * pages so a server that keeps promising more than it returns cannot spin.
     */
    private suspend fun fetchAllChannels(userId: String): List<BaseItemDto> {
        val collected = mutableListOf<BaseItemDto>()
        var startIndex = 0
        var pages = 0
        while (pages < MAX_LIVE_TV_CHANNEL_PAGES) {
            val page = api.getLiveTvChannels(
                userId = userId,
                startIndex = startIndex,
                limit = LIVE_TV_CHANNEL_PAGE_SIZE,
            )
            val items = page.items
            collected += items
            pages++
            if (!hasMoreChannelPages(collected.size, items.size, page.totalRecordCount)) break
            // Page from what actually arrived: the server may cap a page below the
            // requested size, and stepping by the requested size would skip items.
            startIndex += items.size
        }
        return collected
    }

    override suspend fun getPrograms(
        channelIds: List<String>,
        windowStartUtc: String?,
        windowEndUtc: String?,
    ): Result<List<MediaItem>> = suspendRunCatching {
        // The guide is fetched per batch of channels. A single request carrying
        // every channel id built a query string that grows with the provider's
        // catalogue; splitting it keeps the URL bounded now that the channel list
        // is no longer capped at 100.
        //
        // Each batch is paged to the end: the server orders programmes by start date
        // and caps the response, so one page belonged to a handful of channels and the
        // rest of the guide looked empty. `hasMorePages` is the same rule the rest of
        // the app uses — an empty page against a stale total stops the loop, and
        // `MAX_LIVE_TV_GUIDE_PAGES` caps a server that keeps claiming more.
        channelIds.chunked(LIVE_TV_CHANNEL_BATCH_SIZE).flatMap { batch ->
            val batchIds = batch.joinToString(",")
            val programmes = mutableListOf<BaseItemDto>()
            var startIndex = 0
            var pages = 0
            do {
                val page = api.getEpg(
                    channelIds = batchIds,
                    startIndex = startIndex,
                    limit = LIVE_TV_GUIDE_PAGE_SIZE,
                    minEndDate = windowStartUtc,
                    maxStartDate = windowEndUtc,
                )
                programmes += page.items
                startIndex += page.items.size
                pages++
            } while (
                hasMorePages(programmes.size, page.items.size, page.totalRecordCount) &&
                pages < MAX_LIVE_TV_GUIDE_PAGES
            )
            programmes
        }.distinctBy { it.id }.map { it.toDomain() }
    }

    override suspend fun getRecordings(userId: String): Result<List<MediaItem>> = suspendRunCatching {
        // Paged to the end: the list used to be one request of 20 with no `StartIndex`,
        // so a viewer with 35 recordings saw 20 and nothing hinted at the rest.
        val recordings = mutableListOf<BaseItemDto>()
        var startIndex = 0
        var pages = 0
        while (pages < MAX_LIVE_TV_RECORDINGS_PAGES) {
            val page = api.getRecordings(
                userId = userId,
                startIndex = startIndex,
                limit = LIVE_TV_RECORDINGS_PAGE_SIZE,
            )
            recordings += page.items
            pages++
            if (!hasMorePagesWithUnknownTotal(
                    loadedItemCount = recordings.size,
                    receivedItemCount = page.items.size,
                    totalItemCount = page.totalRecordCount,
                    pageSize = LIVE_TV_RECORDINGS_PAGE_SIZE,
                )
            ) {
                break
            }
            // Page from what actually arrived; the server may cap a page below the
            // requested size.
            startIndex += page.items.size
        }
        recordings.distinctBy { it.id }.map { it.toDomain() }
    }

    override suspend fun getScheduledProgramIds(): Result<Set<String>> = suspendRunCatching {
        api.getLiveTvTimers(isScheduled = true)
            .items
            .orEmpty()
            .mapNotNull { timer -> timer.programId?.takeIf(String::isNotBlank) }
            .toSet()
    }

    override suspend fun scheduleRecording(program: MediaItem): Result<Unit> = suspendRunCatching {
        val channelId = program.channelId?.takeIf(String::isNotBlank)
            ?: error("O programa não possui um canal válido.")
        val startDate = program.startDate?.takeIf(String::isNotBlank)
            ?: error("O programa não possui horário de início.")
        val endDate = program.endDate?.takeIf(String::isNotBlank)
            ?: error("O programa não possui horário de término.")

        // The server's `LiveTvManager.CreateTimer` starts with
        // `GetService(timer.ServiceName)`, and the only registered service is
        // named "Emby". A hand-built body has no ServiceName, so the call threw
        // KeyNotFoundException and answered 500 — "Gravar" could never succeed.
        //
        // `Timers/Defaults?programId=` exists to supply exactly that: it returns
        // a prefilled timer for the programme, including `ServiceName` and the
        // server's own padding policy. Its authoritative fields are copied over
        // the program we are scheduling.
        val defaults = api.getLiveTvTimerDefaults(programId = program.id)

        api.createLiveTvTimer(
            CreateLiveTvTimerDto(
                programId = program.id,
                channelId = defaults.channelId?.takeIf(String::isNotBlank) ?: channelId,
                name = defaults.name?.takeIf(String::isNotBlank) ?: program.name,
                overview = defaults.overview?.takeIf(String::isNotBlank) ?: program.overview,
                startDate = defaults.startDate?.takeIf(String::isNotBlank) ?: startDate,
                endDate = defaults.endDate?.takeIf(String::isNotBlank) ?: endDate,
                serviceName = defaults.serviceName?.takeIf(String::isNotBlank)
                    ?: error("O servidor não informou o serviço de TV para este programa."),
                prePaddingSeconds = defaults.prePaddingSeconds ?: 0,
                postPaddingSeconds = defaults.postPaddingSeconds ?: 0,
            ),
        )
    }
}
