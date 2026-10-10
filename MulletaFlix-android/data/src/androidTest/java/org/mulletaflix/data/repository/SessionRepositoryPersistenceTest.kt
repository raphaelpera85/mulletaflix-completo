package org.mulletaflix.data.repository

import android.content.Context
import android.content.ContextWrapper
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.io.File
import java.util.UUID
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.tls.HandshakeCertificates
import okhttp3.tls.HeldCertificate
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.MulletaFlixApiService
import org.mulletaflix.core.api.SavedServerSession
import retrofit2.Retrofit
import retrofit2.converter.moshi.MoshiConverterFactory

@RunWith(AndroidJUnit4::class)
class SessionRepositoryPersistenceTest {
    private val targetContext = InstrumentationRegistry.getInstrumentation().targetContext
    private val isolatedFilesDir = File(targetContext.cacheDir, "session-repository-${UUID.randomUUID()}")
    private val context = object : ContextWrapper(targetContext) {
        override fun getApplicationContext(): Context = this
        override fun getFilesDir(): File = isolatedFilesDir.apply { mkdirs() }
    }
    private val repository = SessionRepositoryImpl(context)

    @After
    fun removeIsolatedSessionData() = runBlocking {
        repository.clearSession()
        isolatedFilesDir.deleteRecursively()
        Unit
    }

    @Test
    fun defaultOfficialServerDoesNotClaimAnUnverifiedHistoricalVersion() = runBlocking {
        val officialServer = repository.getSavedServers().first().single {
            it.url == SessionRepositoryImpl.DEFAULT_MULLETAFLIX_SERVER_URL
        }

        assertNull("versão só deve aparecer após verificação real do servidor", officialServer.version)
    }

    @Test
    fun sessionSurvivesRepositoryRecreationAndLogoutKeepsServerChoice() = runBlocking {
        repository.saveSession(
            serverUrl = "https://media.example.test/",
            token = "fake-token",
            userId = " user-7 ",
            userName = "Pessoa de teste",
            serverId = "server-9",
            deviceId = "device-2",
        )
        val restored = SessionRepositoryImpl(context)

        assertEquals("https://media.example.test", restored.getBaseUrl().first())
        assertEquals("fake-token", restored.getAccessToken().first())
        assertEquals("user-7", restored.getCurrentUserId().first())
        assertEquals("Pessoa de teste", restored.getCurrentUserName().first())
        assertEquals("server-9", restored.getServerId().first())
        assertEquals("device-2", restored.getDeviceId().first())
        assertEquals(
            org.mulletaflix.core.api.SessionState(
                serverUrl = "https://media.example.test",
                accessToken = "fake-token",
                userId = "user-7",
                serverId = "server-9",
            ),
            restored.getSessionState().first(),
        )
        assertNotNull(restored.getFeedbackRequestSession().first())

        restored.clearSession()

        assertNull(restored.getAccessToken().first())
        assertNull(restored.getCurrentUserId().first())
        assertNull(restored.getCurrentUserName().first())
        assertNull(restored.getServerId().first())
        assertNull(restored.getFeedbackRequestSession().first())
        assertEquals("https://media.example.test", restored.getBaseUrl().first())
        assertEquals("device-2", restored.getDeviceId().first())
    }

    @Test
    fun loginForAnotherAccountReplacesPersistedIdentityAndSurvivesRepositoryRecreation() = runBlocking {
        repository.saveSession(
            serverUrl = "https://media.example.test/",
            token = "old-token",
            userId = "old-user",
            userName = "Conta antiga",
            serverId = "server-9",
            deviceId = "device-2",
        )
        val server = MockWebServer()
        try {
            val certificate = HeldCertificate.Builder()
                .addSubjectAlternativeName("localhost")
                .addSubjectAlternativeName("127.0.0.1")
                .build()
            val serverTls = HandshakeCertificates.Builder()
                .heldCertificate(certificate)
                .build()
            val clientTls = HandshakeCertificates.Builder()
                .addTrustedCertificate(certificate.certificate)
                .build()
            server.useHttps(serverTls.sslSocketFactory(), false)
            server.start(java.net.InetAddress.getByName("127.0.0.1"), 0)
            server.enqueue(
                MockResponse()
                    .setHeader("Content-Type", "application/json")
                    .setBody(
                        """{"AccessToken":"new-token","ServerId":"server-9","User":{"Id":"new-user","Name":"Nova conta"}}""",
                    ),
            )
            val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
            val api = Retrofit.Builder()
                .baseUrl(server.url("/"))
                .client(
                    OkHttpClient.Builder()
                        .sslSocketFactory(clientTls.sslSocketFactory(), clientTls.trustManager)
                        .build(),
                )
                .addConverterFactory(MoshiConverterFactory.create(moshi))
                .build()
                .create(MulletaFlixApiService::class.java)

            val login = AuthRepositoryImpl(api, repository).login("Nova conta", "senha-de-teste").getOrThrow()
            val request = server.takeRequest()
            val restored = SessionRepositoryImpl(context)

            assertEquals("POST", request.method)
            assertEquals("/Users/AuthenticateByName", request.path)
            val requestBody = request.body.readUtf8()
            assertTrue(requestBody.contains("\"Username\":\"Nova conta\""))
            assertTrue(requestBody.contains("\"Pw\":\"senha-de-teste\""))
            assertEquals(1, server.requestCount)
            assertEquals("new-user", login.userId)
            assertEquals("new-token", login.token)
            assertEquals("server-9", login.serverId)
            assertEquals("https://media.example.test", restored.getBaseUrl().first())
            assertEquals("new-token", restored.getAccessToken().first())
            assertEquals("new-user", restored.getCurrentUserId().first())
            assertEquals("Nova conta", restored.getCurrentUserName().first())
            assertEquals("server-9", restored.getServerId().first())
            assertEquals("device-2", restored.getDeviceId().first())
        } finally {
            server.shutdown()
        }
    }

    @Test
    fun savedServersRoundTripUpdateInPlaceAndProtectTheOfficialEntry() = runBlocking {
        val server = SavedServerSession(
            name = "Servidor de teste",
            url = "http://192.0.2.10:8096/",
            latencyMs = 18L,
            version = "12.3.4",
            serverId = "test-server-id",
            lastConnected = 1L,
        )
        repository.addSavedServer(server)

        val reopened = SessionRepositoryImpl(context)
        val restoredServer = reopened.getSavedServers().first().single { it.url == server.url.trimEnd('/') }
        assertEquals(server.copy(url = server.url.trimEnd('/'), lastConnected = restoredServer.lastConnected), restoredServer)
        assertTrue(restoredServer.lastConnected > 0L)

        reopened.addSavedServer(server.copy(name = "Renomeado", version = "12.3.5"))
        val serversAfterUpdate = reopened.getSavedServers().first()
        assertEquals(1, serversAfterUpdate.count { it.url == server.url.trimEnd('/') })
        assertEquals("Renomeado", serversAfterUpdate.first { it.url == server.url.trimEnd('/') }.name)

        reopened.removeSavedServer(SessionRepositoryImpl.DEFAULT_MULLETAFLIX_SERVER_URL)
        assertTrue(
            reopened.getSavedServers().first().any {
                it.url == SessionRepositoryImpl.DEFAULT_MULLETAFLIX_SERVER_URL
            },
        )
    }
}
