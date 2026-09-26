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

    @Test
    fun `does not read a route segment as an item id`() {
        // The /web root used to fall through to the literal segment "web",
        // which navigated to `detail/web` and showed "Erro ao carregar
        // detalhes" instead of doing nothing.
        assertNull(extractMediaItemId("http://mulletaflix.duckdns.org:8096/web"))
        assertNull(extractMediaItemId("http://mulletaflix.duckdns.org:8096/web/"))
        assertNull(extractMediaItemId("http://mulletaflix.duckdns.org:8096/web/details"))
        assertNull(extractMediaItemId("http://mulletaflix.duckdns.org:8096/web/item"))
    }

    @Test
    fun `reads the target server id from the link`() {
        assertEquals(
            MediaLink(itemId = "movie-123", serverId = "server-1"),
            extractMediaLink(
                "http://mulletaflix.duckdns.org:8096/web/#/details?id=movie-123&serverId=server-1",
            ),
        )
    }

    @Test
    fun `reads the target server id when it arrives before the item id`() {
        assertEquals(
            MediaLink(itemId = "movie-123", serverId = "server-1"),
            extractMediaLink(
                "mulletaflix://details?serverId=server-1&id=movie-123",
            ),
        )
    }

    @Test
    fun `a link without a server id still yields the item`() {
        assertEquals(
            MediaLink(itemId = "movie-123", serverId = null),
            extractMediaLink("mulletaflix://details?id=movie-123"),
        )
    }

    @Test
    fun `still resolves the id from the path form`() {
        assertEquals(
            MediaLink(itemId = "movie-555"),
            extractMediaLink("mulletaflix://details/movie-555"),
        )
    }

    @Test
    fun `the parsed server id reaches the pending request`() {
        // It used to be dropped here, which is why a link generated on another
        // server opened an unrelated item against this library.
        val request = mediaDeepLinkRequest(
            rawUri = "mulletaflix://details?id=movie-123&serverId=server-B",
            sequence = 7L,
        )

        assertEquals("movie-123", request?.itemId)
        assertEquals("server-B", request?.serverId)
        assertEquals(7L, request?.sequence)
    }
}
