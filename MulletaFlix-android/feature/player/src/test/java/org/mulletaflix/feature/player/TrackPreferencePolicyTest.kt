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
    fun `uses default stream when server default is missing`() {
        assertEquals(8, preferredStreamIndex(streams, "original", serverDefaultIndex = 99))
    }

    @Test
    fun `off disables subtitle selection`() {
        assertNull(preferredStreamIndex(streams, "off", serverDefaultIndex = 5))
    }
}
