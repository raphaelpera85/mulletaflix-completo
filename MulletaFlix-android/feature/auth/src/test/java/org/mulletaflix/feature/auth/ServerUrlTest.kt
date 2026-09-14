package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class ServerUrlTest {
    @Test
    fun `normalizes scheme host port and trailing slash`() {
        assertEquals(
            "http://mulletaflix.duckdns.org:8096",
            normalizeServerUrl(" HTTP://MULLETAFLIX.DUCKDNS.ORG:8096/ ")
        )
    }

    @Test
    fun `preserves a server installation path`() {
        assertEquals("https://media.example.com/jellyfin", normalizeServerUrl("https://MEDIA.EXAMPLE.COM/jellyfin/"))
    }

    @Test
    fun `rejects unsupported or ambiguous URLs`() {
        assertNull(normalizeServerUrl("ftp://media.example.com"))
        assertNull(normalizeServerUrl("https://user:password@media.example.com"))
        assertNull(normalizeServerUrl("https://media.example.com?token=secret"))
        assertNull(normalizeServerUrl("not-a-url"))
    }
}
