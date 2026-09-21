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
    fun `track info keeps technical language separate from display label`() {
        val track = TrackInfo(index = 5, displayName = "Português (Brasil)", language = "pt-BR")

        assertEquals("pt-BR", track.language)
        assertEquals("Português (Brasil)", track.displayName)
    }
}
