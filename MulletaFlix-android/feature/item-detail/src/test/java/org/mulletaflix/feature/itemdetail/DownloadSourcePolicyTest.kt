package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.model.MediaSource

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
}
