package org.mulletaflix.core.api

import com.squareup.moshi.Moshi
import com.squareup.moshi.kotlin.reflect.KotlinJsonAdapterFactory
import java.util.concurrent.atomic.AtomicInteger
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.flowOf
import okhttp3.OkHttpClient
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class SyncPlayCleartextPolicyTest {
    @Test
    fun publicHttpServerIsRejectedBeforeSyncPlayReadsCredentials() {
        val sessions = CountingSessionRepository("http://203.0.113.25:8096")
        val moshi = Moshi.Builder().addLast(KotlinJsonAdapterFactory()).build()
        val client = SyncPlayRealtimeClient(
            httpClient = OkHttpClient(),
            sessionRepository = sessions,
            applicationScope = CoroutineScope(SupervisorJob() + Dispatchers.Unconfined),
            moshi = moshi,
        )

        client.start("group-1")

        assertEquals(0, sessions.accessTokenReads.get())
        assertEquals(0, sessions.deviceIdReads.get())
        assertFalse(client.connectionState.value.connected)
    }

    private class CountingSessionRepository(
        private val serverUrl: String,
    ) : SessionRepository {
        val accessTokenReads = AtomicInteger()
        val deviceIdReads = AtomicInteger()

        override fun getAccessToken() = flowOf("secret-token").also { accessTokenReads.incrementAndGet() }
        override fun getDeviceId() = flowOf("device-1").also { deviceIdReads.incrementAndGet() }
        override fun getBaseUrl() = flowOf(serverUrl)
        override fun getCurrentUserId() = flowOf("user-1")
        override suspend fun saveSession(serverUrl: String, token: String, userId: String, deviceId: String) = Unit
        override suspend fun setBaseUrl(url: String) = Unit
        override suspend fun clearSession() = Unit
    }
}
