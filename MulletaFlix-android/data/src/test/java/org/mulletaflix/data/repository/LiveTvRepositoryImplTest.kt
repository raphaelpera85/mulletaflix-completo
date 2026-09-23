package org.mulletaflix.data.repository

import io.mockk.coEvery
import io.mockk.coVerify
import io.mockk.mockk
import io.mockk.slot
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.LIVE_TV_CHANNEL_PAGE_SIZE
import org.mulletaflix.core.api.LIVE_TV_GUIDE_PAGE_SIZE
import org.mulletaflix.core.api.LIVE_TV_RECORDINGS_PAGE_SIZE
import org.mulletaflix.core.api.MAX_LIVE_TV_CHANNEL_PAGES
import org.mulletaflix.core.api.MAX_LIVE_TV_GUIDE_PAGES
import org.mulletaflix.core.api.MAX_LIVE_TV_RECORDINGS_PAGES
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.dto.BaseItemDto
import org.mulletaflix.core.api.dto.BaseItemDtoQueryResultDto
import org.mulletaflix.core.api.dto.CreateLiveTvTimerDto
import org.mulletaflix.core.api.dto.LiveTvTimerDto
import org.mulletaflix.core.api.dto.LiveTvTimerQueryResultDto
import org.mulletaflix.core.api.dto.TimerDefaultsDto
import org.mulletaflix.domain.model.MediaItem

class LiveTvRepositoryImplTest {

    private val api = mockk<MulletaFlixApiService>()
    private val repository = LiveTvRepositoryImpl(api)

    @Test
    fun getChannels_returnsMappedLiveTvChannels() = runTest {
        coEvery { api.getLiveTvChannels(userId = "user-1") } returns BaseItemDtoQueryResultDto(
            items = listOf(
                BaseItemDto(
                    id = "chan-1",
                    name = "HBO HD",
                    type = "TvChannel",
                    channelName = "501"
                )
            ),
            totalRecordCount = 1
        )

        val result = repository.getChannels("user-1")

        assertTrue(result.isSuccess)
        val channels = result.getOrThrow()
        assertEquals(1, channels.size)
        assertEquals("chan-1", channels[0].id)
        assertEquals("HBO HD", channels[0].name)
    }

    @Test
    fun scheduleRecording_failsWhenChannelIdIsMissing() = runTest {
        val invalidProgram = MediaItem(
            id = "prog-1",
            name = "Movie Special",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = null,
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )

        val result = repository.scheduleRecording(invalidProgram)

        assertTrue(result.isFailure)
        assertTrue(result.exceptionOrNull()?.message?.contains("canal válido") == true)
    }

    @Test
    fun scheduleRecording_succeedsWhenAllRequiredFieldsPresent() = runTest {
        val validProgram = MediaItem(
            id = "prog-1",
            name = "Live Football Match",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            overview = "Final championship game",
            channelId = "chan-10",
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )
        coEvery { api.getLiveTvTimerDefaults(any()) } returns TimerDefaultsDto(
            serviceName = "Emby",
            prePaddingSeconds = 30,
            postPaddingSeconds = 45,
        )
        coEvery { api.createLiveTvTimer(any()) } returns Unit

        val result = repository.scheduleRecording(validProgram)

        assertTrue(result.isSuccess)
        coVerify(exactly = 1) { api.createLiveTvTimer(any()) }
    }

    @Test
    fun scheduleRecording_sendsTheServiceNameTheServerRequires() = runTest {
        // `LiveTvManager.CreateTimer` starts with GetService(timer.ServiceName)
        // and throws KeyNotFoundException when it is missing, which the API maps
        // to HTTP 500. The name comes from Timers/Defaults, not from us.
        val program = MediaItem(
            id = "prog-1",
            name = "Live Football Match",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = "chan-10",
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )
        coEvery { api.getLiveTvTimerDefaults(any()) } returns TimerDefaultsDto(serviceName = "Emby")
        val captured = slot<CreateLiveTvTimerDto>()
        coEvery { api.createLiveTvTimer(capture(captured)) } returns Unit

        repository.scheduleRecording(program)

        coVerify(exactly = 1) { api.getLiveTvTimerDefaults("prog-1") }
        assertTrue(
            "the timer body must carry ServiceName",
            captured.captured.serviceName.isNotBlank(),
        )
        assertTrue(
            "the body must keep the programme it is recording",
            captured.captured.programId == "prog-1",
        )
    }

    @Test
    fun scheduleRecording_appliesTheServerPaddingPolicy() = runTest {
        val program = MediaItem(
            id = "prog-1",
            name = "Live Football Match",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = "chan-10",
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )
        coEvery { api.getLiveTvTimerDefaults(any()) } returns TimerDefaultsDto(
            serviceName = "Emby",
            prePaddingSeconds = 60,
            postPaddingSeconds = 90,
        )
        val captured = slot<CreateLiveTvTimerDto>()
        coEvery { api.createLiveTvTimer(capture(captured)) } returns Unit

        repository.scheduleRecording(program)

        assertEquals(60, captured.captured.prePaddingSeconds)
        assertEquals(90, captured.captured.postPaddingSeconds)
    }

    @Test
    fun scheduleRecording_failsClearlyWhenTheServerHasNoTvService() = runTest {
        // Better an actionable message than the opaque 500 the server returns
        // for a missing service name.
        val program = MediaItem(
            id = "prog-1",
            name = "Live Football Match",
            type = org.mulletaflix.domain.model.MediaItemType.LiveTvProgram,
            channelId = "chan-10",
            startDate = "2026-09-16T20:00:00Z",
            endDate = "2026-09-16T22:00:00Z"
        )
        coEvery { api.getLiveTvTimerDefaults(any()) } returns TimerDefaultsDto(serviceName = null)

        val result = repository.scheduleRecording(program)

        assertTrue(result.isFailure)
        coVerify(exactly = 0) { api.createLiveTvTimer(any()) }
    }

    @Test
    fun getChannels_readsEveryPageTheServerReports() = runTest {
        // A single request with Limit = 100 was the whole catalogue as far as the
        // app was concerned, and TotalRecordCount was never read, so a provider
        // with 250 channels showed 100 and said nothing about the rest.
        val starts = mutableListOf<Int>()
        coEvery {
            api.getLiveTvChannels(any(), any(), any(), capture(starts), any())
        } returnsMany listOf(
            channelPage("a", LIVE_TV_CHANNEL_PAGE_SIZE, total = 250),
            channelPage("b", LIVE_TV_CHANNEL_PAGE_SIZE, total = 250),
            channelPage("c", 50, total = 250),
        )

        val channels = repository.getChannels("user-1").getOrThrow()

        assertEquals(250, channels.size)
        assertEquals("a-1", channels.first().id)
        assertEquals("c-50", channels.last().id)
        assertEquals("each page must start where the previous one ended", listOf(0, 100, 200), starts)
    }

    @Test
    fun getChannels_keepsPagingWhenTheServerOmitsTheTotal() = runTest {
        // `TotalRecordCount` defaults to 0 when the field is absent, so zero has to
        // mean "unknown". Treating it as "nothing left" would truncate at 100 again.
        coEvery { api.getLiveTvChannels(any(), any(), any(), any(), any()) } returnsMany listOf(
            channelPage("a", LIVE_TV_CHANNEL_PAGE_SIZE, total = 0),
            channelPage("b", 30, total = 0),
        )

        val channels = repository.getChannels("user-1").getOrThrow()

        assertEquals(130, channels.size)
        coVerify(exactly = 2) { api.getLiveTvChannels(any(), any(), any(), any(), any()) }
    }

    @Test
    fun getChannels_stopsAtThePageCapWhenTheServerNeverFinishes() = runTest {
        // A server that always returns a full page and no total must not spin.
        coEvery { api.getLiveTvChannels(any(), any(), any(), any(), any()) } returns
            channelPage("a", LIVE_TV_CHANNEL_PAGE_SIZE, total = 0)

        repository.getChannels("user-1").getOrThrow()

        coVerify(exactly = MAX_LIVE_TV_CHANNEL_PAGES) {
            api.getLiveTvChannels(any(), any(), any(), any(), any())
        }
    }

    @Test
    fun getChannels_stopsWhenAPageComesBackEmpty() = runTest {
        coEvery { api.getLiveTvChannels(any(), any(), any(), any(), any()) } returnsMany listOf(
            channelPage("a", 10, total = 500),
            BaseItemDtoQueryResultDto(items = emptyList(), totalRecordCount = 500),
        )

        val channels = repository.getChannels("user-1").getOrThrow()

        assertEquals(10, channels.size)
        coVerify(exactly = 2) { api.getLiveTvChannels(any(), any(), any(), any(), any()) }
    }

    @Test
    fun getPrograms_splitsALargeChannelListIntoBoundedRequests() = runTest {
        // The request line grows with the provider's catalogue: 250 ids in one
        // `ChannelIds` parameter is roughly 8 KB, which is Kestrel's default limit
        // for a request line — the request would answer 414 and the guide would
        // simply never load.
        val requested = mutableListOf<String>()
        coEvery { api.getEpg(capture(requested), any(), any(), any(), any()) } returns
            BaseItemDtoQueryResultDto(items = emptyList(), totalRecordCount = 0)

        val ids = (1..250).map { "channel-$it" }
        repository.getPrograms(ids, null, null).getOrThrow()

        assertEquals(3, requested.size)
        assertTrue(
            "every batch must stay inside the channel batch size",
            requested.all { it.split(",").size <= LIVE_TV_CHANNEL_BATCH_SIZE },
        )
        assertEquals(
            "batching must not drop or duplicate a channel",
            ids.toSet(),
            requested.flatMap { it.split(",") }.toSet(),
        )
    }

    @Test
    fun getPrograms_mergesTheBatchesWithoutRepeatingAProgramme() = runTest {
        coEvery { api.getEpg(any(), any(), any(), any(), any()) } returnsMany listOf(
            BaseItemDtoQueryResultDto(
                items = listOf(programme("prog-1"), programme("prog-2")),
                totalRecordCount = 2,
            ),
            BaseItemDtoQueryResultDto(
                items = listOf(programme("prog-2"), programme("prog-3")),
                totalRecordCount = 2,
            ),
        )

        // One id more than a batch, so this really is two requests.
        val ids = (1..(LIVE_TV_CHANNEL_BATCH_SIZE + 1)).map { "channel-$it" }
        val programs = repository.getPrograms(ids, null, null).getOrThrow()

        assertEquals(listOf("prog-1", "prog-2", "prog-3"), programs.map { it.id })
    }

    @Test
    fun getPrograms_pagesTheGuideInsteadOfKeepingTheFirstPage() = runTest {
        // The server orders programmes by start date and caps the response, so a single
        // page of 50 belonged to a handful of channels: with 300 channels the rest of
        // the guide showed "nenhum programa".
        val requestedStartIndexes = mutableListOf<Int>()
        coEvery {
            api.getEpg(any(), capture(requestedStartIndexes), any(), any(), any())
        } answers {
            val startIndex = secondArg<Int>()
            when (startIndex) {
                0 -> BaseItemDtoQueryResultDto(
                    items = (1..LIVE_TV_GUIDE_PAGE_SIZE).map { programme("prog-$it") },
                    totalRecordCount = LIVE_TV_GUIDE_PAGE_SIZE * 2,
                )
                else -> {
                    // Two channels in the batch, so the second page carries the rest.
                    val from = startIndex + 1
                    BaseItemDtoQueryResultDto(
                        items = (from..(from + LIVE_TV_GUIDE_PAGE_SIZE - 1)).map { programme("prog-$it") },
                        totalRecordCount = LIVE_TV_GUIDE_PAGE_SIZE * 2,
                    )
                }
            }
        }

        val programmes = repository.getPrograms(listOf("channel-1", "channel-2"), null, null).getOrThrow()

        assertEquals(
            "the second page must be asked for at the offset of the first",
            listOf(0, LIVE_TV_GUIDE_PAGE_SIZE),
            requestedStartIndexes,
        )
        assertEquals(LIVE_TV_GUIDE_PAGE_SIZE * 2, programmes.size)
        assertEquals(
            "no programme may be repeated across the pages",
            programmes.size,
            programmes.map { it.id }.distinct().size,
        )
    }

    @Test
    fun getPrograms_sendsTheOverlapWindowTheServerUnderstands() = runTest {
        // `MinEndDate`/`MaxStartDate` are an overlap test; `MinStartDate`/`MaxEndDate`
        // are an "inside the window" test and leave out the programme on the air now.
        var minEndDate: String? = null
        var maxStartDate: String? = null
        coEvery { api.getEpg(any(), any(), any(), any(), any()) } answers {
            minEndDate = args[3] as String?
            maxStartDate = args[4] as String?
            BaseItemDtoQueryResultDto()
        }

        repository.getPrograms(
            channelIds = listOf("channel-1"),
            windowStartUtc = "2026-09-14T20:00:00.000Z",
            windowEndUtc = "2026-09-15T20:00:00.000Z",
        ).getOrThrow()

        assertEquals("2026-09-14T20:00:00.000Z", minEndDate)
        assertEquals("2026-09-15T20:00:00.000Z", maxStartDate)
    }

    @Test
    fun getPrograms_stopsPagingWhenTheServerKeepsClaimingMore() = runTest {
        // A server that always reports a larger total than it returns must not loop.
        coEvery { api.getEpg(any(), any(), any(), any(), any()) } returns
            BaseItemDtoQueryResultDto(
                items = listOf(programme("prog-x")),
                totalRecordCount = 10_000,
            )

        repository.getPrograms(listOf("channel-1"), null, null).getOrThrow()

        coVerify(exactly = MAX_LIVE_TV_GUIDE_PAGES) { api.getEpg(any(), any(), any(), any(), any()) }
    }

    @Test
    fun getPrograms_stopsOnAnEmptyPageEvenWithAStaleTotal() = runTest {
        coEvery { api.getEpg(any(), any(), any(), any(), any()) } returnsMany listOf(
            BaseItemDtoQueryResultDto(items = listOf(programme("prog-1")), totalRecordCount = 50),
            BaseItemDtoQueryResultDto(items = emptyList(), totalRecordCount = 50),
        )

        val programmes = repository.getPrograms(listOf("channel-1"), null, null).getOrThrow()

        assertEquals(listOf("prog-1"), programmes.map { it.id })
        coVerify(exactly = 2) { api.getEpg(any(), any(), any(), any(), any()) }
    }

    @Test
    fun getRecordings_readsEveryPageInsteadOfKeepingTheFirstTwenty() = runTest {
        // A lista era uma requisição só com `Limit = 20` e sem `StartIndex`: quem tinha
        // 35 gravações via 20 e nada dizia que faltavam 15.
        val starts = mutableListOf<Int>()
        coEvery { api.getRecordings(any(), capture(starts), any()) } returnsMany listOf(
            recordingPage("a", LIVE_TV_RECORDINGS_PAGE_SIZE, total = 135),
            recordingPage("b", 35, total = 135),
        )

        val recordings = repository.getRecordings("user-1").getOrThrow()

        assertEquals(135, recordings.size)
        assertEquals("a-1", recordings.first().id)
        assertEquals("b-35", recordings.last().id)
        assertEquals("a segunda página começa onde a primeira terminou", listOf(0, 100), starts)
    }

    @Test
    fun getRecordings_stopsWhenTheServerOmitsTheTotal() = runTest {
        coEvery { api.getRecordings(any(), any(), any()) } returnsMany listOf(
            recordingPage("a", LIVE_TV_RECORDINGS_PAGE_SIZE, total = 0),
            recordingPage("b", 12, total = 0),
        )

        val recordings = repository.getRecordings("user-1").getOrThrow()

        assertEquals(112, recordings.size)
        coVerify(exactly = 2) { api.getRecordings(any(), any(), any()) }
    }

    @Test
    fun getRecordings_stopsAtThePageCapWhenTheServerNeverFinishes() = runTest {
        coEvery { api.getRecordings(any(), any(), any()) } returns
            recordingPage("a", LIVE_TV_RECORDINGS_PAGE_SIZE, total = 0)

        repository.getRecordings("user-1").getOrThrow()

        coVerify(exactly = MAX_LIVE_TV_RECORDINGS_PAGES) { api.getRecordings(any(), any(), any()) }
    }

    @Test
    fun getRecordings_doesNotRepeatARecordingAcrossPages() = runTest {
        // Uma gravação nova no topo entre as duas requisições desloca a janela: a
        // segunda página repete as últimas da primeira. A lista não pode mostrar a
        // mesma gravação duas vezes.
        coEvery { api.getRecordings(any(), any(), any()) } returnsMany listOf(
            recordingRange(from = 1, count = LIVE_TV_RECORDINGS_PAGE_SIZE, total = 150),
            recordingRange(from = 96, count = 55, total = 150),
        )

        val recordings = repository.getRecordings("user-1").getOrThrow()

        assertEquals(150, recordings.size)
        assertEquals(recordings.size, recordings.map { it.id }.distinct().size)
    }

    private fun recordingRange(from: Int, count: Int, total: Int) = BaseItemDtoQueryResultDto(
        items = (from until from + count).map { index ->
            BaseItemDto(id = "rec-$index", name = "Gravação $index", type = "Recording")
        },
        totalRecordCount = total,
    )

    private fun recordingPage(prefix: String, count: Int, total: Int) = BaseItemDtoQueryResultDto(
        items = (1..count).map { index ->
            BaseItemDto(id = "$prefix-$index", name = "$prefix $index", type = "Recording")
        },
        totalRecordCount = total,
    )

    private fun channelPage(prefix: String, count: Int, total: Int) = BaseItemDtoQueryResultDto(
        items = (1..count).map { index ->
            BaseItemDto(id = "$prefix-$index", name = "$prefix $index", type = "TvChannel")
        },
        totalRecordCount = total,
    )

    private fun programme(id: String) = BaseItemDto(
        id = id,
        name = "Programa $id",
        type = "Program",
        channelId = "c1",
    )

    @Test
    fun getScheduledProgramIds_returnsTheProgrammesWithAPendingTimer() = runTest {
        coEvery { api.getLiveTvTimers(isScheduled = true) } returns LiveTvTimerQueryResultDto(
            items = listOf(
                LiveTvTimerDto(id = "timer-1", programId = "prog-1"),
                LiveTvTimerDto(id = "timer-2", programId = "prog-2"),
            ),
            totalRecordCount = 2,
        )

        val result = repository.getScheduledProgramIds()

        assertEquals(setOf("prog-1", "prog-2"), result.getOrThrow())
    }

    @Test
    fun getScheduledProgramIds_ignoresTimersWithoutAProgramme() = runTest {
        // A manual timer that is not tied to a guide entry has no ProgramId; it
        // must not turn into an empty-string key that matches nothing, and it
        // must not fail the whole lookup.
        coEvery { api.getLiveTvTimers(isScheduled = true) } returns LiveTvTimerQueryResultDto(
            items = listOf(
                LiveTvTimerDto(id = "timer-1", programId = null),
                LiveTvTimerDto(id = "timer-2", programId = "   "),
                LiveTvTimerDto(id = "timer-3", programId = "prog-3"),
            ),
        )

        val result = repository.getScheduledProgramIds()

        assertEquals(setOf("prog-3"), result.getOrThrow())
    }

    @Test
    fun getScheduledProgramIds_isEmptyWhenTheServerSendsNoItems() = runTest {
        coEvery { api.getLiveTvTimers(isScheduled = true) } returns LiveTvTimerQueryResultDto()

        val result = repository.getScheduledProgramIds()

        assertEquals(emptySet<String>(), result.getOrThrow())
    }
}
