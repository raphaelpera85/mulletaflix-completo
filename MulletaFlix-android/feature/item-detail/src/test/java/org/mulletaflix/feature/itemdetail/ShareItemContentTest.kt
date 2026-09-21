package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ShareItemContentTest {

    @Test
    fun `builds encoded detail link and removes trailing slash`() {
        assertEquals(
            "http://server:8096/web/#/details?id=movie%2F1+edition",
            buildItemShareUrl(" http://server:8096/// ", " movie/1 edition ")
        )
    }

    @Test
    fun `share text includes title and server link`() {
        val text = buildItemShareText(" O Filme ", "movie-1", "http://server:8096")

        assertTrue(text.contains("\"O Filme\""))
        assertTrue(text.contains("http://server:8096/web/#/details?id=movie-1"))
    }

    @Test
    fun `loopback server uses the public MulletaFlix link when sharing`() {
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/web/#/details?id=movie-1",
            buildItemShareUrl("http://localhost:8096", "movie-1"),
        )
        assertEquals(
            "http://mulletaflix.duckdns.org:8096",
            canonicalShareBaseUrl("http://127.0.0.1:8096/"),
        )
    }

    @Test
    fun `private lan server uses the public link when sharing`() {
        // At home the session is switched to the LAN endpoint, so this is the
        // normal state. A private address is useless to the recipient.
        listOf(
            "http://192.168.1.20:8096",
            "http://10.0.0.5:8096",
            "http://172.16.4.9:8096",
            "http://169.254.10.10:8096",
        ).forEach { lanUrl ->
            assertEquals(
                "$lanUrl must not leak into a shared link",
                "http://mulletaflix.duckdns.org:8096",
                canonicalShareBaseUrl(lanUrl),
            )
        }
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/web/#/details?id=movie-1",
            buildItemShareUrl("http://192.168.1.20:8096", "movie-1"),
        )
    }

    @Test
    fun `a genuinely public server url is preserved`() {
        assertEquals(
            "https://midia.exemplo.com:8920",
            canonicalShareBaseUrl("https://midia.exemplo.com:8920/"),
        )
    }

    @Test
    fun `share link carries the target server id`() {
        assertEquals(
            "http://server:8096/web/#/details?id=movie-1&serverId=srv-1",
            buildItemShareUrl("http://server:8096", "movie-1", "srv-1"),
        )
    }

    @Test
    fun `share link omits a blank server id`() {
        assertEquals(
            "http://server:8096/web/#/details?id=movie-1",
            buildItemShareUrl("http://server:8096", "movie-1", "   "),
        )
        assertEquals(
            "http://server:8096/web/#/details?id=movie-1",
            buildItemShareUrl("http://server:8096", "movie-1", null),
        )
    }

    @Test
    fun `each media type shares its own id`() {
        val seasonId = "season-2"
        val episodeId = "episode-202"
        val seriesId = "series-9"

        listOf(seriesId, seasonId, episodeId).forEach { id ->
            val url = buildItemShareUrl("http://server:8096", id, "srv-1")
            assertTrue("expected the link to carry $id", url!!.contains("id=$id"))
        }
        val episodeUrl = buildItemShareUrl("http://server:8096", episodeId, "srv-1")!!
        assertTrue(
            "an episode link must never carry the series id",
            !episodeUrl.contains("id=$seriesId"),
        )
    }

    @Test
    fun `blank server does not create a broken link`() {
        assertNull(buildItemShareUrl("  ", "movie-1"))
        assertEquals("Confira \"O Filme\" no MulletaFlix.", buildItemShareText("O Filme", "movie-1", ""))
    }
}
