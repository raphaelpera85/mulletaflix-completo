package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * An offline playback session must keep honouring the user's settings.
 *
 * The offline path built `PlayerState(...)` from scratch, so every
 * settings-derived field fell back to its data-class default: a download entered
 * Picture-in-Picture even with the setting switched off, lost the subtitle font
 * size, and forgot that the network was metered.
 */
class OfflinePlaybackStateTest {

    private fun state(
        pictureInPictureEnabled: Boolean = true,
        subtitleFontSize: Int = 100,
        isNetworkMetered: Boolean = false,
    ) = offlinePlaybackState(
        title = "Filme baixado",
        isBuffering = true,
        error = null,
        aspectRatio = VideoAspectRatio.ZOOM,
        subtitleColor = "YELLOW",
        subtitleFontSize = subtitleFontSize,
        pictureInPictureEnabled = pictureInPictureEnabled,
        isNetworkMetered = isNetworkMetered,
    )

    @Test
    fun `the picture in picture setting is not reset to its default`() {
        assertFalse(
            "a download must not enter PiP when the user turned the setting off",
            state(pictureInPictureEnabled = false).pictureInPictureEnabled,
        )
        assertTrue(state(pictureInPictureEnabled = true).pictureInPictureEnabled)
    }

    @Test
    fun `the subtitle preferences are carried over`() {
        val built = state(subtitleFontSize = 150)

        assertEquals(150, built.subtitleFontSize)
        assertEquals("YELLOW", built.subtitleColor)
        assertEquals(VideoAspectRatio.ZOOM, built.aspectRatio)
    }

    @Test
    fun `a metered network stays metered for a download`() {
        assertTrue(state(isNetworkMetered = true).isNetworkMetered)
    }

    @Test
    fun `the offline session never claims to be online-only or busy forever`() {
        val built = state()

        assertEquals("Filme baixado", built.title)
        assertTrue(built.isBuffering)
        assertFalse(built.isNetworkOffline)
        assertEquals(null, built.error)
    }
}
