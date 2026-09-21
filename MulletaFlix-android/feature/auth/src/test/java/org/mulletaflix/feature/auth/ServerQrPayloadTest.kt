package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class ServerQrPayloadTest {
    @Test
    fun `accepts a direct HTTP server URL`() {
        assertEquals("http://192.168.1.20:8096", serverUrlFromQrPayload(" http://192.168.1.20:8096/ "))
    }

    @Test
    fun `extracts an encoded URL from MulletaFlix QR scheme`() {
        assertEquals(
            "https://media.example.com/mulletaflix",
            serverUrlFromQrPayload("mulletaflix://server?url=https%3A%2F%2Fmedia.example.com%2Fmulletaflix%2F"),
        )
    }

    @Test
    fun `rejects unsupported or malformed QR payloads`() {
        assertNull(serverUrlFromQrPayload("javascript:alert(1)"))
        assertNull(serverUrlFromQrPayload("mulletaflix://server?url=not-a-url"))
        assertNull(serverUrlFromQrPayload(null))
    }
}
