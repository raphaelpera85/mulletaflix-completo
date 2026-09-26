package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

class PlayerOptionsTest {

    @Test
    fun `cast action communicates whether a receiver session is active`() {
        // Só o rótulo visível: a `contentDescription` fixa em pt-BR saiu porque era um
        // terceiro nome para o mesmo controle, competindo com o rótulo e com a
        // descrição localizada que o `MediaRouteButton` do Media3 já publica.
        assertEquals("Transmitir", castActionLabel(isCasting = false))
        assertEquals("Transmitindo", castActionLabel(isCasting = true))
    }

    @Test
    fun `quality control is available locally but not during cast`() {
        assertTrue(qualityControlAvailable(isCasting = false))
        assertFalse(qualityControlAvailable(isCasting = true))
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
    fun `quality labels preserve unknown server supplied names`() {
        assertEquals("Qualidade 20", qualityOptionLabel("Qualidade 20", isMetered = false))
        assertEquals("1080p", qualityOptionLabel("1080", isMetered = false))
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

    @Test
    fun `a resolution the server really offers can be selected and kept`() {
        // Tracks below 480p keep their own height instead of being bucketed onto
        // a ladder rung. `qualityOptions` named them "360p"/"240p",
        // `normalizeQualityPreference` did not recognise those names and answered
        // "Auto" — so picking 360p stored "Auto" and the radio jumped back to
        // Automatic.
        val streams = listOf(360, 288, 240, 480, 720).mapIndexed { index, height ->
            MediaStream(index = index, type = MediaStreamType.Video, height = height)
        }
        val offered = qualityOptions(streams)

        assertEquals(listOf("720p", "480p", "360p", "288p", "240p"), offered)

        offered.forEach { quality ->
            assertEquals(
                "\"$quality\" must survive being stored, or choosing it is discarded",
                quality,
                normalizeQualityPreference(quality),
            )
            assertEquals(
                "\"$quality\" must stay selected once the title offers it",
                quality,
                effectiveQualitySelection(quality, offered),
            )
        }
    }

    @Test
    fun `an unlisted resolution still caps the stream it names`() {
        // Falling through to "no cap" would serve 4K to someone who asked for 360p.
        val constraint = videoQualityConstraint("360p")
        assertEquals(360, constraint.maxHeight)
        assertTrue("an unlisted height must not be left uncapped", constraint.maxBitrate < Int.MAX_VALUE)
        assertEquals(360, videoQualityConstraint("360P").maxHeight)
    }

    @Test
    fun `implausible quality values are still rejected`() {
        assertEquals("Auto", normalizeQualityPreference("9999p"))
        assertEquals("Auto", normalizeQualityPreference("0p"))
        assertEquals("Auto", normalizeQualityPreference("360"))
        assertEquals("Auto", normalizeQualityPreference("360px"))
    }

    @Test
    fun `the applied quality is always one the menu can show`() {
        // The stored preference may name a resolution this title does not offer.
        // `applyQuality` used to write it raw, so the state claimed "4K" while the
        // menu — built from `qualityMenuOptions(availableQualities)` — had no
        // matching row and the control appeared unset.
        val offered = listOf("1080p", "720p")
        val menu = qualityMenuOptions(offered)

        listOf("4K", "1440p", "1080p", "720p", "Auto", null, "unsupported").forEach { stored ->
            val applied = appliedQualitySelection(stored, offered)

            assertTrue(
                "quality \"$stored\" applied as \"$applied\", which the menu does not offer",
                menu.contains(applied),
            )
        }
    }

    @Test
    fun `a resolution the title does offer is kept when applied`() {
        assertEquals("720p", appliedQualitySelection("720p", listOf("1080p", "720p")))
        assertEquals("Auto", appliedQualitySelection("4K", listOf("1080p", "720p")))
        assertEquals("Auto", appliedQualitySelection("720p", emptyList()))
    }
}
