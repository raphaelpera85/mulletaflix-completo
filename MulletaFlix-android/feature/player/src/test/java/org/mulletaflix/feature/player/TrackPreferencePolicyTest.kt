package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

class TrackPreferencePolicyTest {
    private val streams = listOf(
        MediaStream(index = 2, type = MediaStreamType.Audio, language = "eng"),
        MediaStream(index = 5, type = MediaStreamType.Audio, language = "pt-BR"),
        MediaStream(index = 8, type = MediaStreamType.Audio, language = "spa", isDefault = true),
    )

    @Test
    fun `prefers requested language over server default`() {
        assertEquals(5, preferredStreamIndex(streams, "por", serverDefaultIndex = 2))
    }

    @Test
    fun `matches regional language and falls back to server default`() {
        assertEquals(5, preferredStreamIndex(streams, "pt", serverDefaultIndex = 2))
        assertEquals(2, preferredStreamIndex(streams, "deu", serverDefaultIndex = 2))
    }

    @Test
    fun `matches localized display language with region suffix`() {
        val localizedStreams = listOf(
            MediaStream(
                index = 12,
                type = MediaStreamType.Audio,
                displayLanguage = "Português (Brasil)",
            ),
            MediaStream(
                index = 14,
                type = MediaStreamType.Audio,
                displayLanguage = "English (US)",
            ),
        )

        assertEquals(12, preferredStreamIndex(localizedStreams, "pt-BR", serverDefaultIndex = null))
        assertEquals(14, preferredStreamIndex(localizedStreams, "en", serverDefaultIndex = null))
    }

    @Test
    fun `uses default stream when server default is missing`() {
        assertEquals(8, preferredStreamIndex(streams, "original", serverDefaultIndex = 99))
    }

    @Test
    fun `off disables subtitle selection`() {
        assertNull(preferredStreamIndex(streams, "off", serverDefaultIndex = 5))
    }

    @Test
    fun `initial request uses only an explicit matching language`() {
        assertEquals(5, requestedPreferredStreamIndex(streams, "pt-BR"))
        assertNull(requestedPreferredStreamIndex(streams, "original"))
        assertNull(requestedPreferredStreamIndex(streams, "deu"))
    }

    @Test
    fun `off is asked for as minus one, not as no preference`() {
        // Null means "no preference" to Jellyfin: the server then computes a
        // default from the user's subtitle mode and may burn a subtitle in.
        // `MediaSourceManager.SetDefaultSubtitleStreamIndex` treats -1 as a
        // remembered "no subtitles", which is what actually disables them.
        assertEquals(
            DISABLED_SUBTITLE_STREAM_INDEX,
            requestedPreferredStreamIndex(streams, "off"),
        )
        assertEquals(
            DISABLED_SUBTITLE_STREAM_INDEX,
            requestedPreferredStreamIndex(streams, "none"),
        )
        assertEquals(
            DISABLED_SUBTITLE_STREAM_INDEX,
            requestedPreferredStreamIndex(streams, "Desativadas"),
        )
    }

    @Test
    fun `track info keeps technical language separate from display label`() {
        val track = TrackInfo(index = 5, displayName = "Português (Brasil)", language = "pt-BR")

        assertEquals("pt-BR", track.language)
        assertEquals("Português (Brasil)", track.displayName)
    }

    @Test
    fun `a track without a language is not worth remembering`() {
        // The server may describe a track with neither Language nor
        // DisplayLanguage. Persisting that null deleted the stored preference
        // (`SettingsRepositoryImpl` drops blank values and then answers with its
        // default, "por"), so the next item silently used the default track
        // instead of the one the user had chosen.
        assertNull(persistableTrackLanguage(null))
        assertNull(persistableTrackLanguage(""))
        assertNull(persistableTrackLanguage("   "))
    }

    @Test
    fun `a described language is remembered without surrounding space`() {
        assertEquals("spa", persistableTrackLanguage("spa"))
        assertEquals("pt-BR", persistableTrackLanguage("  pt-BR  "))
    }
}
