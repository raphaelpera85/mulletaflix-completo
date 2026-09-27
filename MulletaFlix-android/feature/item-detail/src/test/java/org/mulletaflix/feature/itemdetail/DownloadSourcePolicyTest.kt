package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaSource
import org.mulletaflix.domain.model.MediaStream
import org.mulletaflix.domain.model.MediaStreamType

class DownloadSourcePolicyTest {

    @Test
    fun `direct stream wins even when another source has transcoding`() {
        assertEquals(
            "https://server/direct",
            preferredDownloadUrl(
                listOf(
                    MediaSource("transcode", transcodeUrl = "https://server/transcode"),
                    MediaSource("direct", directStreamUrl = " https://server/direct "),
                )
            )
        )
    }

    @Test
    fun `transcoding is used when direct stream is unavailable`() {
        assertEquals(
            "https://server/transcode",
            preferredDownloadUrl(listOf(MediaSource("source", transcodeUrl = " https://server/transcode ")))
        )
    }

    @Test
    fun `blank sources return no download URL`() {
        assertNull(
            preferredDownloadUrl(
                listOf(MediaSource("empty", directStreamUrl = " ", transcodeUrl = ""))
            )
        )
    }

    @Test
    fun `preferred download retains the source that owns subtitle streams`() {
        val transcode = MediaSource("transcode", transcodeUrl = "https://server/transcode")
        val direct = MediaSource("direct", directStreamUrl = "https://server/direct")

        assertEquals(direct, preferredDownloadSource(listOf(transcode, direct))?.source)
        assertEquals("https://server/direct", preferredDownloadSource(listOf(transcode, direct))?.url)
    }

    @Test
    fun `only supported external subtitle formats become download metadata`() {
        val source = MediaSource(
            id = "source/1",
            mediaStreams = listOf(
                MediaStream(3, MediaStreamType.Subtitle, codec = "SubRip", language = "por", isExternal = true,
                    deliveryUrl = "https://cdn.example/subtitle.srt?token=must-not-persist", isDefault = true),
                MediaStream(4, MediaStreamType.Subtitle, codec = "webvtt", isExternal = true),
                MediaStream(5, MediaStreamType.Subtitle, codec = "pgs", isExternal = true),
                MediaStream(6, MediaStreamType.Subtitle, codec = "srt", isExternal = false),
                MediaStream(7, MediaStreamType.Audio, codec = "srt", isExternal = true),
            ),
        )

        val metadata = downloadableExternalSubtitles(source)
        assertEquals(listOf(3, 4), metadata.map { it.streamIndex })
        assertEquals(listOf("application/x-subrip", "text/vtt"), metadata.map { it.mimeType })
        assertEquals("por", metadata.first().language)
        assertTrue(metadata.first().isDefault)
        assertFalse(metadata.toString().contains("must-not-persist"))
    }
}
