package org.mulletaflix.data.repository

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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

    @Test
    fun `an opened live channel carries its live stream id`() {
        // A rota de stream só encontra o feed já aberto pelo `LiveStreamId`; sem ele um
        // canal de tuner não reproduz.
        val urls = buildPlaybackStreamUrls(
            baseUrl = "http://server:8096",
            itemId = "channel-1",
            mediaSourceId = "source-1",
            accessToken = "token",
            liveStreamId = "live-1",
        )

        assertEquals(
            "http://server:8096/Videos/channel-1/stream?MediaSourceId=source-1&LiveStreamId=live-1&api_key=token&Static=true",
            urls.directStream,
        )
        assertEquals(
            "http://server:8096/Videos/channel-1/master.m3u8?MediaSourceId=source-1&LiveStreamId=live-1&api_key=token",
            urls.transcode,
        )
    }

    @Test
    fun `a blank live stream id is left out of the url`() {
        val urls = buildPlaybackStreamUrls(
            baseUrl = "http://server:8096",
            itemId = "movie",
            mediaSourceId = "source",
            accessToken = null,
            liveStreamId = "  ",
        )

        assertEquals(
            "http://server:8096/Videos/movie/stream?MediaSourceId=source&Static=true",
            urls.directStream,
        )
    }

    @Test
    fun `only a source the server marked as requiring opening is opened`() {
        assertTrue(shouldOpenLiveStream(requiresOpening = true, liveStreamId = null))
        assertTrue(shouldOpenLiveStream(requiresOpening = true, liveStreamId = "  "))
        assertFalse(
            "uma fonte já aberta por outra sessão é reaproveitada",
            shouldOpenLiveStream(requiresOpening = true, liveStreamId = "live-1"),
        )
        assertFalse(
            "um filme comum não passa por LiveStreams/Open",
            shouldOpenLiveStream(requiresOpening = false, liveStreamId = null),
        )
    }
}
