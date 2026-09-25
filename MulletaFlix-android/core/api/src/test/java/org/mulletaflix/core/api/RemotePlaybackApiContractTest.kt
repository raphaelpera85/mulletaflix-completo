package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.api.dto.SessionInfoDto
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

class RemotePlaybackApiContractTest {
    private val moshi = Moshi.Builder().build()

    @Test
    fun `session response decodes current media and seekable playback state`() {
        val response = """
            {"Id":"tv-session","DeviceName":"Sala","Client":"Android TV",
             "DeviceId":"tv","NowPlayingItem":{"Id":"movie-1","Name":"Filme","RunTimeTicks":5400000000},
             "PlayState":{"PositionTicks":1200000000,"CanSeek":true,"IsPaused":false}}
        """.trimIndent()

        val session = requireNotNull(moshi.adapter(SessionInfoDto::class.java).fromJson(response))

        assertEquals("tv-session", session.id)
        assertEquals("Filme", session.nowPlayingItem?.name)
        assertEquals(5_400_000_000L, session.nowPlayingItem?.runTimeTicks)
        assertEquals(1_200_000_000L, session.playState?.positionTicks)
        assertTrue(session.playState?.canSeek == true)
        assertFalse(session.playState?.isPaused == true)
    }

    @Test
    fun `active sessions route filters to controllable user and recent activity`() {
        val method = methodOf("getSessions")
        assertEquals("Sessions", requireNotNull(method.getAnnotation(GET::class.java)).value)
        val queryNames = method.parameterAnnotations.map { annotations ->
            annotations.filterIsInstance<Query>().singleOrNull()?.value
        }.filterNotNull()
        assertEquals(listOf("controllableByUserId", "activeWithinSeconds"), queryNames)
    }

    @Test
    fun `remote playback command uses session path and optional seek position`() {
        val method = methodOf("sendSessionPlaystateCommand")
        assertEquals("Sessions/{sessionId}/Playing/{command}", requireNotNull(method.getAnnotation(POST::class.java)).value)
        assertEquals(listOf("sessionId", "command"), method.parameterAnnotations.take(2).map { annotations ->
            annotations.filterIsInstance<Path>().single().value
        })
        assertEquals("seekPositionTicks", method.parameterAnnotations[2].filterIsInstance<Query>().single().value)
        assertEquals("controllingUserId", method.parameterAnnotations[3].filterIsInstance<Query>().single().value)
    }

    private fun methodOf(name: String) = MulletaFlixApiService::class.java.methods.single { it.name == name }
}
