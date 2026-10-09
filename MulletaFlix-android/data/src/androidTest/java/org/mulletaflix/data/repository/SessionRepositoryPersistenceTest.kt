package org.mulletaflix.data.repository

import android.content.Context
import android.content.ContextWrapper
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import java.io.File
import java.util.UUID
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.api.SavedServerSession

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
