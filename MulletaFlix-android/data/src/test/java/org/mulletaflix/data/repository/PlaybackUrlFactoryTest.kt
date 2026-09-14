package org.mulletaflix.data.repository

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackUrlFactoryTest {

    @Test
    fun `encodes item source and token while preserving server installation path`() {
        val urls = buildPlaybackStreamUrls(
            baseUrl = "http://192.168.1.50:8096/jellyfin/",
            itemId = "item/with space",
            mediaSourceId = "source&1",
            accessToken = "token+with&reserved",
        )

        assertEquals(
            "http://192.168.1.50:8096/jellyfin/Videos/item%2Fwith%20space/stream?MediaSourceId=source%261&api_key=token%2Bwith%26reserved&Static=true",
            urls.directStream,
        )
        assertTrue(urls.transcode.endsWith("/Videos/item%2Fwith%20space/master.m3u8?MediaSourceId=source%261&api_key=token%2Bwith%26reserved"))
    }

    @Test
    fun `omits blank token and normalizes trailing slashes`() {
        val urls = buildPlaybackStreamUrls(
            baseUrl = "http://server:8096///",
            itemId = "movie",
            mediaSourceId = "source",
            accessToken = " ",
        )

        assertEquals(
            "http://server:8096/Videos/movie/stream?MediaSourceId=source&Static=true",
            urls.directStream,
        )
    }
}
