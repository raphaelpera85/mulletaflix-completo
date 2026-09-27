package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.mulletaflix.core.api.dto.MediaRequestDto
import org.mulletaflix.core.api.dto.PlaybackIssueDto
import org.mulletaflix.core.common.session.FeedbackRequestSession
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

class UserFeedbackHttpContractTest {
    private lateinit var server: MockWebServer
    private var replacementServer: MockWebServer? = null

    @Before
    fun setUp() {
        server = MockWebServer()
        server.start()
        replacementServer = null
    }

    @After
    fun tearDown() {
        server.shutdown()
        replacementServer?.shutdown()
    }

    private fun sessionRepository() = MutableSessionRepository(server.url("/").toString().trimEnd('/'))

    private fun api(
        session: SessionRepository = sessionRepository(),
        beforeRouting: (() -> Unit)? = null,
    ): MulletaFlixApiService {
        val client = OkHttpClient.Builder()
            .apply {
                if (beforeRouting != null) addInterceptor { chain ->
                    beforeRouting()
                    chain.proceed(chain.request())
                }
            }
            .addInterceptor(ServerUrlInterceptor(session))
            .addInterceptor(ClientIdentityInterceptor(session))
            .build()
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()

        return Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(moshi))
            .build()
            .create(MulletaFlixApiService::class.java)
    }

    @Test
    fun `media request posts expected fields to authenticated endpoint`() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(204))

        api().requestMedia(
            MediaRequestDto(
                title = "Dune",
                mediaType = "Movie",
                year = 2021,
                notes = "Pedido pelo aplicativo",
            ),
            sessionForServer(),
        )

        val request = server.takeRequest()
        assertEquals("POST", request.method)
        assertEquals("/UserFeedback/MediaRequests", request.requestUrl?.encodedPath)
        assertEquals(
            """{"Title":"Dune","MediaType":"Movie","Year":2021,"Notes":"Pedido pelo aplicativo"}""",
            request.body.readUtf8(),
        )
        assertAuthenticatedIdentity(request.getHeader("Authorization"))
    }

    @Test
    fun `playback issue posts expected fields to authenticated endpoint`() = runBlocking {
        server.enqueue(MockResponse().setResponseCode(204))

        api().reportPlaybackIssue(
            PlaybackIssueDto(
                itemId = "movie-1",
                category = "video",
                description = "A imagem congela aos 10 minutos",
            ),
            sessionForServer(),
        )

        val request = server.takeRequest()
        assertEquals("POST", request.method)
        assertEquals("/UserFeedback/PlaybackIssues", request.requestUrl?.encodedPath)
        assertEquals(
            """{"ItemId":"movie-1","Category":"video","Description":"A imagem congela aos 10 minutos"}""",
            request.body.readUtf8(),
        )
        assertAuthenticatedIdentity(request.getHeader("Authorization"))
    }

    @Test
    fun `feedback request keeps original server and identity after session changes before routing`() = runBlocking {
        val originalSession = sessionForServer()
        val mutableSession = sessionRepository()
        val nextServer = MockWebServer().also { it.start() }
        replacementServer = nextServer
        nextServer.enqueue(MockResponse().setResponseCode(204))
        server.enqueue(MockResponse().setResponseCode(204))

        api(mutableSession) {
            mutableSession.switchTo(
                serverUrl = nextServer.url("/").toString().trimEnd('/'),
                token = "new-account-token",
                userId = "user-2",
            )
        }.requestMedia(MediaRequestDto("Dune", "Movie", null, null), originalSession)

        val request = server.takeRequest()
        assertAuthenticatedIdentity(request.getHeader("Authorization"))
        assertEquals(0, nextServer.requestCount)
    }

    private fun sessionForServer() = FeedbackRequestSession(
        serverUrl = server.url("/").toString().trimEnd('/'),
        accessToken = "feedback-token",
        userId = "user-1",
        deviceId = "feedback-device",
    )

    private class MutableSessionRepository(serverUrl: String) : SessionRepository {
        private val baseUrl = MutableStateFlow(serverUrl)
        private val token = MutableStateFlow<String?>("feedback-token")
        private val userId = MutableStateFlow<String?>("user-1")
        private val deviceId = MutableStateFlow("feedback-device")

        fun switchTo(serverUrl: String, token: String, userId: String) {
            baseUrl.value = serverUrl
            this.token.value = token
            this.userId.value = userId
        }

        override fun getAccessToken() = token
        override fun getDeviceId() = deviceId
        override fun getBaseUrl() = baseUrl
        override fun getCurrentUserId() = userId
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }

    private fun assertAuthenticatedIdentity(authorization: String?) {
        requireNotNull(authorization)
        assertTrue(authorization.startsWith("MediaBrowser Token=\"feedback-token\""))
        assertTrue(authorization.contains("DeviceId=\"feedback-device\""))
    }
}
