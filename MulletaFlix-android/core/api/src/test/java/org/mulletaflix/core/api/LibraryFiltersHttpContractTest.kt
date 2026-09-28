package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

class LibraryFiltersHttpContractTest {
    private lateinit var server: MockWebServer

    @Before fun setUp() {
        server = MockWebServer()
        server.start()
    }

    @After fun tearDown() = server.shutdown()

    private fun api(): MulletaFlixApiService {
        val session = object : SessionRepository {
            override fun getAccessToken() = flowOf("test-token")
            override fun getDeviceId() = flowOf("library-filter-device")
            override fun getBaseUrl() = flowOf(server.url("/").toString().trimEnd('/'))
            override fun getCurrentUserId() = flowOf("user-1")
            override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
            override suspend fun setBaseUrl(url: String) = Unit
            override suspend fun clearSession() = Unit
        }
        val client = OkHttpClient.Builder()
            .addInterceptor(ClientIdentityInterceptor(session))
            .build()
        return Retrofit.Builder()
            .baseUrl(server.url("/"))
            .client(client)
            .addConverterFactory(MoshiConverterFactory.create(Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()))
            .build()
            .create(MulletaFlixApiService::class.java)
    }

    @Test
    fun `requests available filter values for the selected library and item types`() = runBlocking {
        server.enqueue(
            MockResponse().setResponseCode(200).setBody(
                """{"Genres":["Drama","Ação"],"Years":[2024,2023],"OfficialRatings":["PG-13"]}""",
            ),
        )

        val response = api().getLibraryFilterOptions("library-1", "Movie,Episode")
        val request = server.takeRequest()

        assertEquals("GET", request.method)
        assertEquals("/Items/Filters", request.requestUrl?.encodedPath)
        assertEquals("library-1", request.requestUrl?.queryParameter("ParentId"))
        assertEquals("Movie,Episode", request.requestUrl?.queryParameter("IncludeItemTypes"))
        assertEquals(listOf("Drama", "Ação"), response.genres)
        assertEquals(listOf(2024, 2023), response.years)
        assertEquals(listOf("PG-13"), response.officialRatings)
        assertTrue(request.getHeader("Authorization").orEmpty().contains("Token=\"test-token\""))
    }
}
