package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * Documenta a decisão de repontar um stream já preparado quando o endereço do servidor
 * muda — o cenário "comecei a assistir em casa, saí do alcance do Wi-Fi e o vídeo
 * morreu".
 */
class StreamRetargetPolicyTest {

    @Test
    fun `a stream prepared on the lan moves to the public address`() {
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/Videos/item-1/stream?MediaSourceId=s1&api_key=NEW&Static=true",
            retargetPreparedStreamUrl(
                preparedUrl = "http://192.168.15.9:8096/Videos/item-1/stream?MediaSourceId=s1&api_key=OLD&Static=true",
                baseUrl = "http://mulletaflix.duckdns.org:8096",
                accessToken = "NEW",
            ),
        )
    }

    @Test
    fun `a stream that already points at the current address is not touched`() {
        // `getBaseUrl()` emits on every preference write; re-preparing a healthy stream
        // would only stutter it.
        assertNull(
            retargetPreparedStreamUrl(
                preparedUrl = "http://192.168.15.9:8096/Videos/item-1/stream?api_key=OLD",
                baseUrl = "http://192.168.15.9:8096/",
                accessToken = "NEW",
            ),
        )
    }

    @Test
    fun `the port participates in the comparison`() {
        assertEquals(
            "https://192.168.15.9:443/Videos/item-1/stream?api_key=NEW",
            retargetPreparedStreamUrl(
                preparedUrl = "http://192.168.15.9:8096/Videos/item-1/stream?api_key=OLD",
                baseUrl = "https://192.168.15.9:443",
                accessToken = "NEW",
            ),
        )
    }

    @Test
    fun `an offline download is never re-pointed at a server`() {
        assertNull(
            retargetPreparedStreamUrl(
                preparedUrl = "file:///data/user/0/org.mulletaflix.android/files/downloads/item-1",
                baseUrl = "http://mulletaflix.duckdns.org:8096",
                accessToken = "NEW",
            ),
        )
        assertNull(
            retargetPreparedStreamUrl(
                preparedUrl = "content://media/external/video/media/42",
                baseUrl = "http://mulletaflix.duckdns.org:8096",
                accessToken = "NEW",
            ),
        )
    }

    @Test
    fun `a session that has not been read yet leaves the stream alone`() {
        val prepared = "http://192.168.15.9:8096/Videos/item-1/stream?api_key=OLD"
        assertNull(retargetPreparedStreamUrl(prepared, baseUrl = "", accessToken = "NEW"))
        assertNull(retargetPreparedStreamUrl(prepared, baseUrl = "não é um endereço", accessToken = "NEW"))
        assertNull(retargetPreparedStreamUrl("", "http://server:8096", "NEW"))
    }

    @Test
    fun `the retargeted url is the one the download path would use`() {
        // The value handed to `setUri` has to be a URL the server accepts, not a
        // rewritten string: path, query and the replaced credential are all checked.
        val retargeted = retargetPreparedStreamUrl(
            preparedUrl = "http://192.168.15.9:8096/jellyfin/Videos/item-1/stream?api_key=OLD",
            baseUrl = "http://mulletaflix.duckdns.org:8096/jellyfin/",
            accessToken = "NEW TOKEN",
        )
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/jellyfin/Videos/item-1/stream?api_key=NEW+TOKEN",
            retargeted,
        )
    }
}
