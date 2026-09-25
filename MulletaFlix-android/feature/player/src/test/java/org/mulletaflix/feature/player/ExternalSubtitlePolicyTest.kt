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
}
