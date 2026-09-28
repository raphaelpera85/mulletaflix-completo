package org.mulletaflix.android.navigation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class MulletaFlixRouteTest {

    @Test
    fun library_formatsRouteCorrectly() {
        val route = MulletaFlixRoute.library("lib-123")
        assertEquals("main/library/lib-123", route)
    }

    @Test
    fun itemDetail_formatsRouteCorrectly() {
        val route = MulletaFlixRoute.itemDetail("item-456")
        assertEquals("detail/item-456", route)
    }

    @Test
    fun videoPlayer_formatsRouteCorrectly() {
        val route = MulletaFlixRoute.videoPlayer("video-789")
        assertEquals("player/video/video-789", route)
    }

    @Test
    fun offlinePlayer_urlEncodesParameters() {
        val route = MulletaFlixRoute.offlinePlayer(
            itemId = "downloaded 1",
            uri = "content://media/external/video/42",
            title = "Movie & Show",
            downloadId = "queue:v2/1",
        )
        // A space must become %20, never `+`. Navigation decodes query
        // arguments with `Uri.getQueryParameters`, which follows RFC 3986 and
        // does not turn `+` back into a space, so `+` used to reach the player
        // OSD verbatim as `Movie+&+Show`.
        assertTrue(route.startsWith("player/offline/downloaded%201?uri="))
        assertTrue(route.contains("content%3A%2F%2Fmedia%2Fexternal%2Fvideo%2F42"))
        assertTrue(route.contains("title=Movie%20%26%20Show"))
        assertTrue(route.contains("downloadId=queue%3Av2%2F1"))
        assertFalse(
            "a `+` in the route means the value will not decode back",
            route.contains('+'),
        )
    }

    @Test
    fun routeArgumentEncoderEscapesEveryByteOutsideTheUnreservedSet() {
        // The encoder is the contract: what it escapes decides whether the
        // value survives Navigation's RFC 3986 decoding.
        assertEquals("O%20Retorno%20de%20Jedi", encodeRouteQueryArgument("O Retorno de Jedi"))
        assertEquals("Movie%20%26%20Show", encodeRouteQueryArgument("Movie & Show"))
        assertEquals("50%25%20de%20desconto", encodeRouteQueryArgument("50% de desconto"))
        assertEquals("Pergunta%3F%20Sim%21", encodeRouteQueryArgument("Pergunta? Sim!"))
        assertEquals("A%2FB%3A%20o%20retorno", encodeRouteQueryArgument("A/B: o retorno"))
        assertEquals(
            "content%3A%2F%2Fmedia%2Fexternal%2Fvideo%2F42",
            encodeRouteQueryArgument("content://media/external/video/42"),
        )
    }

    @Test
    fun routeArgumentEncoderKeepsUnreservedCharactersReadable() {
        assertEquals("movie-1_2.3~4", encodeRouteQueryArgument("movie-1_2.3~4"))
    }

    @Test
    fun routeArgumentEncoderNeverEmitsAPlusForSpace() {
        assertFalse(encodeRouteQueryArgument("a b").contains('+'))
        assertEquals("%20", encodeRouteQueryArgument(" "))
    }

    @Test
    fun everyOfflinePlayerArgumentSurvivesTheEncoder() {
        val itemId = "downloaded 1"
        val uri = "content://media/external/video/42?token=a b"
        val title = "O Retorno de Jedi"
        val downloadId = "queue:v2/server 1/media 1"
        val route = MulletaFlixRoute.offlinePlayer(itemId, uri, title, downloadId)

        val query = route.substringAfter('?')
        val values = query.split('&').associate {
            it.substringBefore('=') to decodeRouteArgument(it.substringAfter('='))
        }
        assertEquals(uri, values["uri"])
        assertEquals(title, values["title"])
        assertEquals(downloadId, values["downloadId"])
        assertEquals(
            itemId,
            decodeRouteArgument(route.substringAfter("player/offline/").substringBefore('?')),
        )
    }

    @Test
    fun offlinePlayer_keepsQueueIdentitySeparateForDuplicateMediaIds() {
        val first = MulletaFlixRoute.offlinePlayer(
            itemId = "same-media-id",
            uri = "content://downloads/one",
            title = "Same title",
            downloadId = "server-a-entry",
        )
        val second = MulletaFlixRoute.offlinePlayer(
            itemId = "same-media-id",
            uri = "content://downloads/two",
            title = "Same title",
            downloadId = "server-b-entry",
        )

        assertTrue(first != second)
        assertTrue(first.startsWith("player/offline/same-media-id?"))
        assertTrue(second.startsWith("player/offline/same-media-id?"))
        assertTrue(first.endsWith("downloadId=server-a-entry"))
        assertTrue(second.endsWith("downloadId=server-b-entry"))
    }

    /** Inverse of the encoder, mirroring what an RFC 3986 decoder does. */
    private fun decodeRouteArgument(value: String): String {
        val bytes = mutableListOf<Byte>()
        var index = 0
        while (index < value.length) {
            val char = value[index]
            if (char == '%' && index + 2 < value.length) {
                bytes += value.substring(index + 1, index + 3).toInt(16).toByte()
                index += 3
            } else {
                bytes += char.toString().toByteArray(Charsets.UTF_8).toList()
                index += 1
            }
        }
        return String(bytes.toByteArray(), Charsets.UTF_8)
    }
}
