package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ExternalSubtitlePolicyTest {
    @Test
    fun `maps supported subtitle codecs and file extensions`() {
        assertEquals("application/x-subrip", externalSubtitleMimeType("SubRip", null))
        assertEquals("application/x-subrip", externalSubtitleMimeType(null, "/captions/movie.srt"))
        assertEquals("text/vtt", externalSubtitleMimeType("webvtt", null))
        assertEquals("text/x-ssa", externalSubtitleMimeType(null, "https://cdn.example/captions.ass"))
        assertEquals("application/ttml+xml", externalSubtitleMimeType("dfxp", null))
        assertNull(externalSubtitleMimeType(null, "/captions/unknown.xml"))
        assertNull(externalSubtitleMimeType("pgs", "/captions/movie.sup"))
    }

    @Test
    fun `uses server converted WebVTT delivery format for an SRT source`() {
        assertEquals(
            "text/vtt",
            externalSubtitleMimeType(
                codec = "srt",
                deliveryUrl = "/Videos/item/source/Subtitles/3/0/Stream.vtt?api_key=secret",
            ),
        )
    }

    @Test
    fun `builds fallback route with encoded item and source ids`() {
        assertEquals(
            "Items/item%20one/Subtitles/4/Stream?MediaSourceId=source%2Ftwo",
            externalSubtitleStreamPath("item one", 4, "source/two"),
        )
        assertNull(externalSubtitleStreamPath("", 4, "source"))
        assertNull(externalSubtitleStreamPath("item", -1, "source"))
        assertEquals("Items/item/Subtitles/4/Stream", externalSubtitleStreamPath("item", 4, null))
    }

    @Test
    fun `authenticates relative and same-origin URLs without duplicating token`() {
        assertEquals(
            "http://server:8096/Items/movie/Subtitles/2/Stream?api_key=NEW+TOKEN",
            resolveExternalSubtitleUrl(
                "http://server:8096",
                "/Items/movie/Subtitles/2/Stream?api_key=OLD",
                "NEW TOKEN",
            ),
        )
        assertEquals(
            "http://server:8096/Items/movie/Subtitles/2/Stream?MediaSourceId=source&api_key=NEW+TOKEN",
            resolveExternalSubtitleUrl(
                "http://server:8096",
                "Items/movie/Subtitles/2/Stream?MediaSourceId=source",
                "NEW TOKEN",
            ),
        )
    }

    @Test
    fun `does not send server token to another origin`() {
        val resolved = resolveExternalSubtitleUrl(
            "http://server:8096",
            "https://cdn.example/subtitles/movie.srt?sig=public",
            "session-token",
        )

        assertEquals("https://cdn.example/subtitles/movie.srt?sig=public", resolved)
        assertFalse(resolved.orEmpty().contains("session-token"))
        assertNull(resolveExternalSubtitleUrl(
            "http://server:8096",
            "https://cdn.example/subtitles/movie.srt?api_key=embedded-secret",
            "session-token",
        ))
        assertNull(resolveExternalSubtitleUrl(
            "http://server:8096",
            "https://cdn.example/subtitles/movie.vtt?ApiKey=embedded-secret",
            "session-token",
        ))
        assertNull(resolveExternalSubtitleUrl(
            "http://server:8096",
            "https://cdn.example/subtitles/movie.vtt?X-MediaBrowser-Token=embedded-secret",
            "session-token",
        ))
        assertNull(resolveExternalSubtitleUrl(
            "http://server:8096",
            "https://cdn.example/subtitles/movie.vtt?%61uth%6Frization=embedded-secret",
            "session-token",
        ))
        assertEquals(
            "https://cdn.example/subtitles/movie.vtt?signature=public&expires=1000",
            resolveExternalSubtitleUrl(
                "http://server:8096",
                "https://cdn.example/subtitles/movie.vtt?signature=public&expires=1000",
                "session-token",
            ),
        )
        assertEquals(
            "http://server:8096/subtitles/movie.srt",
            resolveExternalSubtitleUrl("http://server:8096", "//server:8096/subtitles/movie.srt", null),
        )
    }

    @Test
    fun `external track ids map only to valid server indices`() {
        assertEquals(12, externalSubtitleServerIndex("mullet-external:12"))
        assertNull(externalSubtitleServerIndex("1:12"))
        assertNull(externalSubtitleServerIndex("12"))
        assertNull(externalSubtitleServerIndex("container-track"))
        assertNull(externalSubtitleServerIndex("-1"))
        assertTrue(externalSubtitleMimeType("srt", null) != null)
    }

    @Test
    fun `Cast exposes only receiver supported sidecar formats`() {
        assertEquals("text/vtt", castSubtitleContentType("text/vtt"))
        assertEquals("application/ttml+xml", castSubtitleContentType("application/ttml+xml"))
        assertNull(castSubtitleContentType("application/x-subrip"))
        assertNull(castSubtitleContentType("text/x-ssa"))
        assertEquals(0x4D554C4C0000000CL, castSubtitleTrackId(12))
        assertNull(castSubtitleTrackId(-1))
    }

    @Test
    fun `maps Cast track content URL back to its server stream index`() {
        val url = "http://server:8096/subtitles/12.vtt?api_key=secret"

        assertEquals(12, externalSubtitleServerIndex(url, mapOf(url to 12)))
        assertEquals(12, externalSubtitleServerIndex("mullet-external:12", emptyMap()))
        assertNull(externalSubtitleServerIndex("https://other.invalid/track.vtt", emptyMap()))
    }
}
