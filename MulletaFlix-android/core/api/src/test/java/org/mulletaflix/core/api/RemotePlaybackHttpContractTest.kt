package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

class RemotePlaybackHttpContractTest {
    private lateinit var server: MockWebServer

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After
    fun tearDown() {
        server.shutdown()
    }

    private fun api(): MulletaFlixApiService {
        val session = object : SessionRepository {
            override fun getAccessToken() = kotlinx.coroutines.flow.flowOf<String?>(null)
            override fun getDeviceId() = kotlinx.coroutines.flow.flowOf("remote-playback-test")
            override fun getBaseUrl() = kotlinx.coroutines.flow.flowOf(server.url("/").toString().trimEnd('/'))
            override fun getCurrentUserId() = kotlinx.coroutines.flow.flowOf<String?>(null)
            override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
            override suspend fun setBaseUrl(url: String) = Unit
            override suspend fun clearSession() = Unit
        }
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val client = OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(session))
            .build()

        return Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()
            .create(MulletaFlixApiService::class.java)
    }

    @Test
    fun `session request sends user filters and decodes active playback`() = runBlocking {
        server.enqueue(
            MockResponse().setBody(
                """[{"Id":"tv-session","DeviceName":"Sala","Client":"Android TV","DeviceId":"tv","NowPlayingItem":{"Id":"movie-1","Name":"Filme","RunTimeTicks":5400000000},"PlayState":{"PositionTicks":1200000000,"CanSeek":true,"IsPaused":false}}]""",
            ),
        )

        val sessions = api().getSessions(controllableByUserId = "user-1")
        val request = server.takeRequest()
        val url = requireNotNull(request.requestUrl)

        assertEquals("GET", request.method)
        assertEquals("/Sessions", url.encodedPath)
        assertEquals("user-1", url.queryParameter("controllableByUserId"))
        assertEquals("300", url.queryParameter("activeWithinSeconds"))
        assertEquals("tv-session", sessions.single().id)
        assertEquals("Filme", sessions.single().nowPlayingItem?.name)
        assertEquals(1_200_000_000L, sessions.single().playState?.positionTicks)
        assertTrue(sessions.single().playState?.canSeek == true)
        assertFalse(sessions.single().playState?.isPaused == true)
    }

    @Test
    fun `seek request sends session command seek position and controlling user`() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(204))

        api().sendSessionPlaystateCommand(
            sessionId = "tv-session",
            command = "Seek",
            seekPositionTicks = 12_300L,
            controllingUserId = "user-1",
        )
        val request = server.takeRequest()
        val url = requireNotNull(request.requestUrl)

        assertEquals("POST", request.method)
        assertEquals("/Sessions/tv-session/Playing/Seek", url.encodedPath)
        assertEquals("12300", url.queryParameter("seekPositionTicks"))
        assertEquals("user-1", url.queryParameter("controllingUserId"))
    }

    @Test
    fun `play pause request omits optional seek position`() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(204))

        api().sendSessionPlaystateCommand(
            sessionId = "tv-session",
            command = "PlayPause",
            controllingUserId = "user-1",
        )
        val request = server.takeRequest()
        val url = requireNotNull(request.requestUrl)

        assertEquals("POST", request.method)
        assertEquals("/Sessions/tv-session/Playing/PlayPause", url.encodedPath)
        assertEquals("user-1", url.queryParameter("controllingUserId"))
        assertEquals(null, url.queryParameter("seekPositionTicks"))
    }

    @Test
    fun `library query sends genre year and official rating facets with server delimiters`() = runBlocking {
        server.enqueue(MockResponse().setBody("""{"Items":[],"TotalRecordCount":0}"""))

        api().getItems(
            userId = "user-1",
            parentId = "library-1",
            genres = "Drama|Ação",
            years = "2023,2024",
            officialRatings = "PG-13|TV-MA",
            isPlayed = false,
            isFavorite = true,
        )

        val request = server.takeRequest()
        val url = requireNotNull(request.requestUrl)
        assertEquals("GET", request.method)
        assertEquals("/Users/user-1/Items", url.encodedPath)
        assertEquals("Drama|Ação", url.queryParameter("Genres"))
        assertEquals("2023,2024", url.queryParameter("Years"))
        assertEquals("PG-13|TV-MA", url.queryParameter("OfficialRatings"))
        assertEquals("false", url.queryParameter("IsPlayed"))
        assertEquals("true", url.queryParameter("IsFavorite"))
    }
}
