package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

class PlayerOptionsTest {

    @Test
    fun `formats playback stats for support sharing`() {
        val text = formatPlaybackStats(
            PlaybackStats(
                videoCodec = "H.265",
                audioCodec = "AAC",
                resolution = "1920x1080",
                bitrate = "8 Mbps",
                playMethod = "Transcode",
            ),
        )

        assertTrue(text.contains("Método de Reprodução: Transcode"))
        assertTrue(text.contains("Codec de Vídeo: H.265"))
        assertTrue(text.contains("Taxa de Bits: 8 Mbps"))
    }
    @Test
    fun `quality options are derived from video heights and deduplicated`() {
        val streams = listOf(
            MediaStream(index = 0, type = MediaStreamType.Video, height = 2160),
            MediaStream(index = 1, type = MediaStreamType.Video, height = 1080),
            MediaStream(index = 2, type = MediaStreamType.Video, height = 1080),
            MediaStream(index = 3, type = MediaStreamType.Audio, height = 480),
        )

        assertEquals(listOf("4K", "1080p"), qualityOptions(streams))
    }

    @Test
    fun `quality options are empty when server has no video metadata`() {
        assertEquals(emptyList<String>(), qualityOptions(emptyList()))
    }

    @Test
    fun `quality choices map to actual resolution constraints`() {
        assertEquals(VideoQualityConstraint(2560, 1440, 16_000_000), videoQualityConstraint("1440p"))
        assertEquals(VideoQualityConstraint(1920, 1080, 10_000_000), videoQualityConstraint("1080p"))
        assertEquals(VideoQualityConstraint(Int.MAX_VALUE, Int.MAX_VALUE, Int.MAX_VALUE), videoQualityConstraint("Auto"))
    }

    @Test
    fun `quality menu has one auto option and removes duplicates`() {
        assertEquals(
            listOf("Auto", "1080p", "720p"),
            qualityMenuOptions(listOf("Auto", "1080p", "1080p", "", "720p")),
        )
    }

    @Test
    fun `quality preference normalizes blank values to auto`() {
        assertEquals("Auto", normalizeQualityPreference(null))
        assertEquals("Auto", normalizeQualityPreference("   "))
        assertEquals("1080p", normalizeQualityPreference(" 1080p "))
    }

    @Test
    fun `video aspect ratio modes provide valid titles and resize modes`() {
        assertEquals("Ajustar (Original)", VideoAspectRatio.FIT.title)
        assertEquals("Preencher / Zoom", VideoAspectRatio.ZOOM.title)
        assertEquals("Esticar", VideoAspectRatio.FILL.title)
    }

    @Test
    fun `aspect ratio preference accepts known values and safely falls back to fit`() {
        assertEquals(VideoAspectRatio.ZOOM, normalizeAspectRatioPreference(" zoom "))
        assertEquals(VideoAspectRatio.FILL, normalizeAspectRatioPreference("FILL"))
        assertEquals(VideoAspectRatio.FIT, normalizeAspectRatioPreference(null))
        assertEquals(VideoAspectRatio.FIT, normalizeAspectRatioPreference("unknown"))
    }
}
