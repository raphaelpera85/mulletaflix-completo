package org.mulletaflix.android.service

import java.io.IOException
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.core.common.network.CleartextTrafficPolicy

class CleartextSafeDownloadUrlTest {
    @Test
    fun `public HTTP download is rejected before session token is added`() {
        val failure = runCatching {
            cleartextSafeDownloadUrl(
                storedUri = "http://203.0.113.9:8096/Videos/movie/stream",
                baseUrl = "http://203.0.113.9:8096",
                accessToken = "private-session-token",
            )
        }.exceptionOrNull()

        assertTrue(failure is IOException)
        assertEquals(CleartextTrafficPolicy.BLOCKED_MESSAGE, failure?.message)
    }

    @Test
    fun `local HTTP and public HTTPS downloads keep working`() {
        val lanUrl = cleartextSafeDownloadUrl(
            storedUri = "http://127.0.0.1:8096/Videos/movie/stream",
            baseUrl = "http://127.0.0.1:8096",
            accessToken = "private-session-token",
        )
        val publicUrl = cleartextSafeDownloadUrl(
            storedUri = "https://media.example.org/Videos/movie/stream",
            baseUrl = "https://media.example.org",
            accessToken = "private-session-token",
        )

        assertTrue(lanUrl.contains("api_key=private-session-token"))
        assertTrue(publicUrl.contains("api_key=private-session-token"))
    }
}
