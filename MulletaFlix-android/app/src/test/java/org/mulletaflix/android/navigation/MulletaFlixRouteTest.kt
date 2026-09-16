package org.mulletaflix.android.navigation

import org.junit.Assert.assertEquals
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
            title = "Movie & Show"
        )
        assertTrue(route.startsWith("player/offline/downloaded+1?uri="))
        assertTrue(route.contains("content%3A%2F%2Fmedia%2Fexternal%2Fvideo%2F42"))
        assertTrue(route.contains("Movie+%26+Show"))
    }
}
