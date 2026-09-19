package org.mulletaflix.feature.player

import androidx.media3.common.MimeTypes
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PlaybackMimeTypeTest {

    @Test
    fun `maps transcode HLS sources to the HLS mime type`() {
        assertEquals(
            MimeTypes.APPLICATION_M3U8,
            playbackMimeType(null, "http://server/Videos/item/master.m3u8?api_key=token"),
        )
    }

    @Test
    fun `maps server container before relying on the URL suffix`() {
        assertEquals(MimeTypes.VIDEO_MP4, playbackMimeType("mp4", "http://server/Videos/item/stream"))
        assertEquals(MimeTypes.VIDEO_MATROSKA, playbackMimeType("Matroska", "http://server/Videos/item/stream"))
    }

    @Test
    fun `leaves unsupported containers for Media3 sniffing`() {
        assertNull(playbackMimeType("avi", "http://server/Videos/item/stream"))
    }
}
