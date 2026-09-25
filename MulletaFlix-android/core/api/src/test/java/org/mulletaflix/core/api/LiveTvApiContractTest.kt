package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import retrofit2.http.GET
import retrofit2.http.DELETE
import retrofit2.http.Path
import retrofit2.http.POST
import retrofit2.http.Query

/**
 * Pins the Live TV routes to the server's actual contract.
 *
 * `getEpg` used to call `LiveTv/EPG`, which does not exist on any
 * MulletaFlix/Jellyfin release. The request answered 404, so the EPG dialog
 * showed "HTTP 404 Not Found" and listed no programmes — the whole guide was
 * dead, and nothing in the test suite noticed because only the ViewModel was
 * covered, never the route.
 *
 * The server's routes are declared in
 * `Jellyfin.Api/Controllers/LiveTvController.cs`; these assertions exist so a
 * rename on either side fails here instead of in front of the user.
 */
class LiveTvApiContractTest {

    private fun getPath(methodName: String): String {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == methodName }
        val get = method.getAnnotation(GET::class.java)
        assertNotNull("$methodName must be a GET", get)
        return get!!.value
    }

    @Test
    fun `the guide uses the server's Programs route`() {
        assertEquals("LiveTv/Programs", getPath("getEpg"))
    }

    @Test
    fun `the guide sends the channel filter, the page and the overlap window`() {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "getEpg" }
        val queryNames = method.parameterAnnotations
            .mapNotNull { annotations -> annotations.filterIsInstance<Query>().firstOrNull()?.value }
            .toSet()

        assertEquals(
            "the server's Programs route filters by these query parameters",
            setOf("ChannelIds", "StartIndex", "Limit", "MinEndDate", "MaxStartDate"),
            queryNames,
        )
    }

    @Test
    fun `the guide window is an overlap window, not a start-date filter`() {
        // `MinStartDate`/`MaxEndDate` mean "inside the window", so the programme on the
        // air right now — the one the viewer is watching — was excluded from the guide.
        // `MinEndDate`/`MaxStartDate` are the overlap pair the server understands
        // (`BaseItemRepository.TranslateQuery.cs`), and both have to be on the wire.
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "getEpg" }
        val queryNames = method.parameterAnnotations
            .mapNotNull { annotations -> annotations.filterIsInstance<Query>().firstOrNull()?.value }
            .toSet()

        org.junit.Assert.assertTrue(
            "getEpg must send MinEndDate and MaxStartDate; found $queryNames",
            queryNames.containsAll(setOf("MinEndDate", "MaxStartDate")),
        )
        org.junit.Assert.assertFalse(
            "getEpg must not go back to a start-date filter; found $queryNames",
            queryNames.contains("MinStartDate") || queryNames.contains("MaxEndDate"),
        )
    }

    @Test
    fun `channels use the server's Channels route`() {
        assertEquals("LiveTv/Channels", getPath("getLiveTvChannels"))
    }

    @Test
    fun `channels are requested as a page so the catalogue is not truncated`() {
        // `Limit = 100` with no `StartIndex` was the entire catalogue as far as the
        // app was concerned: a provider with 250 channels showed 100 and nothing
        // said the rest existed. Both parameters have to be on the wire.
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "getLiveTvChannels" }
        val queries = method.parameterAnnotations
            .mapNotNull { annotations -> annotations.filterIsInstance<Query>().firstOrNull()?.value }
            .toSet()

        org.junit.Assert.assertTrue(
            "getLiveTvChannels must page with StartIndex and Limit; found $queries",
            queries.containsAll(setOf("StartIndex", "Limit")),
        )
    }

    @Test
    fun `recordings use the server's Recordings route`() {
        assertEquals("LiveTv/Recordings", getPath("getRecordings"))
    }

    @Test
    fun `creating a timer posts to the server's Timers route`() {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "createLiveTvTimer" }
        val post = method.getAnnotation(POST::class.java)

        assertNotNull("createLiveTvTimer must be a POST", post)
        assertEquals("LiveTv/Timers", post!!.value)
    }

    @Test
    fun `the timer body carries the service name the server requires`() {
        // `LiveTvManager.CreateTimer` calls `GetService(timer.ServiceName)` as its
        // first statement, and the only registered service is named "Emby".
        // A body without ServiceName throws KeyNotFoundException and the server
        // answers 500, so "Gravar" could never succeed.
        val fields = org.mulletaflix.core.api.dto.CreateLiveTvTimerDto::class.java.declaredFields
            .map { it.name }

        org.junit.Assert.assertTrue(
            "CreateLiveTvTimerDto must expose ServiceName; found $fields",
            fields.any { it.equals("serviceName", ignoreCase = true) },
        )
    }

    @Test
    fun `listing timers uses the server's Timers route`() {
        assertEquals("LiveTv/Timers", getPath("getLiveTvTimers"))
    }

    @Test
    fun `listing timers asks only for the pending ones`() {
        // `LiveTvManager.GetTimers` maps IsScheduled=true to
        // `Status == RecordingStatus.New`. Without the filter the response also
        // carries completed and cancelled timers, and marking those would leave
        // finished recordings shown as if they were still going to happen.
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "getLiveTvTimers" }
        val queries = method.parameterAnnotations
            .mapNotNull { annotations -> annotations.filterIsInstance<Query>().firstOrNull()?.value }

        assertEquals(listOf("IsScheduled"), queries)
    }

    @Test
    fun `cancelling a scheduled recording deletes its timer by id`() {
        val method = MulletaFlixApiService::class.java.methods.single { it.name == "cancelLiveTvTimer" }
        val delete = method.getAnnotation(DELETE::class.java)
        assertNotNull("cancelLiveTvTimer must be a DELETE", delete)
        assertEquals("LiveTv/Timers/{timerId}", delete!!.value)
        val pathParameters = method.parameterAnnotations
            .flatMap { annotations -> annotations.filterIsInstance<Path>() }
        assertEquals("timerId", pathParameters.single().value)
    }

    @Test
    fun `a timer exposes the programme it records`() {
        // `BaseTimerInfoDto.ProgramId` is what lets the guide mark a programme as
        // already scheduled; without it the "Gravar" button offered a duplicate.
        val fields = org.mulletaflix.core.api.dto.LiveTvTimerDto::class.java.declaredFields
            .map { it.name }

        org.junit.Assert.assertTrue(
            "LiveTvTimerDto must expose ProgramId; found $fields",
            fields.any { it.equals("programId", ignoreCase = true) },
        )
    }
}
