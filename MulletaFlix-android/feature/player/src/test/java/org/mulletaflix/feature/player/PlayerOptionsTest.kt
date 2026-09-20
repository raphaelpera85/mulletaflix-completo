package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

class PlayerOptionsTest {

    @Test
    fun `cast action communicates whether a receiver session is active`() {
        assertEquals("Transmitir", castActionLabel(isCasting = false))
        assertEquals("Transmitindo", castActionLabel(isCasting = true))
        assertEquals(
            "Transmitindo para dispositivo compatível",
            castActionContentDescription(isCasting = true),
        )
    }

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
            title = "À Beira da Extinção",
        )

        assertTrue(text.contains("Título: À Beira da Extinção"))
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
    fun `quality preference normalizes persisted aliases to canonical labels`() {
        assertEquals("4K", normalizeQualityPreference(" 2160p "))
        assertEquals("1440p", normalizeQualityPreference("1440"))
        assertEquals("1080p", normalizeQualityPreference("FULL HD"))
        assertEquals("Auto", normalizeQualityPreference("automático"))
    }

    @Test
    fun `invalid quality values fall back to automatic mode`() {
        assertEquals("Auto", normalizeQualityPreference("unsupported"))
    }

    @Test
    fun `auto quality is capped on metered networks without changing manual choices`() {
        assertEquals("720p", effectivePlaybackQuality("Auto", isMetered = true))
        assertEquals("Auto", effectivePlaybackQuality("Auto", isMetered = false))
        assertEquals("1080p", effectivePlaybackQuality("1080p", isMetered = true))
    }

    @Test
    fun `auto quality label explains the metered network cap`() {
        assertEquals("Auto (até 720p nesta rede)", qualityOptionLabel("Auto", isMetered = true))
        assertEquals("Auto", qualityOptionLabel("Auto", isMetered = false))
        assertEquals("1080p", qualityOptionLabel("1080p", isMetered = true))
    }

    @Test
    fun `quality selection falls back to auto when saved resolution is unavailable`() {
        assertEquals("Auto", effectiveQualitySelection("4K", listOf("1080p", "720p")))
        assertEquals("1440p", effectiveQualitySelection("1440", listOf("1440p", "1080p")))
        assertEquals("Auto", effectiveQualitySelection("Auto", emptyList()))
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
