package org.mulletaflix.android

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class MediaDeepLinkTest {
    @Test
    fun `reads item id from the official web hash link`() {
        assertEquals(
            "movie-123",
            extractMediaItemId(
                "http://mulletaflix.duckdns.org:8096/web/#/details?id=movie-123&serverId=server-1",
            ),
        )
    }

    @Test
    fun `reads item id from the app scheme query`() {
        assertEquals(
            "episode-456",
            extractMediaItemId("mulletaflix://details?id=episode-456"),
        )
    }

    @Test
    fun `rejects unrelated hosts and malformed links`() {
        assertNull(extractMediaItemId("https://example.com/web/#/details?id=movie-123"))
        assertNull(extractMediaItemId("https://mulletaflix.duckdns.org:8096/website/#/details?id=movie-123"))
        assertNull(extractMediaItemId("mulletaflix://details"))
        assertNull(extractMediaItemId(null as String?))
    }

    @Test
    fun `accepts the official web root without requiring a trailing slash`() {
        assertEquals(
            "movie-789",
            extractMediaItemId(
                "https://mulletaflix.duckdns.org/web#/details?id=movie-789",
            ),
        )
    }
}
