package org.mulletaflix.android.navigation

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.android.MediaDeepLinkRequest

class DeepLinkNavigationPolicyTest {

    @Test
    fun `navigates from authenticated home to incoming media`() {
        assertTrue(
            shouldNavigateToMediaDeepLink(
                currentRoute = MulletaFlixRoute.HOME,
                currentItemId = null,
                targetItemId = "movie-123",
            ),
        )
    }

    @Test
    fun `does not navigate when the requested item is already visible`() {
        assertFalse(
            shouldNavigateToMediaDeepLink(
                currentRoute = MulletaFlixRoute.ITEM_DETAIL,
                currentItemId = "movie-123",
                targetItemId = "movie-123",
            ),
        )
    }

    @Test
    fun `does not bypass authentication`() {
        assertFalse(
            shouldNavigateToMediaDeepLink(
                currentRoute = MulletaFlixRoute.LOGIN,
                currentItemId = null,
                targetItemId = "movie-123",
            ),
        )
    }

    @Test
    fun `rejects empty target`() {
        assertFalse(
            shouldNavigateToMediaDeepLink(
                currentRoute = MulletaFlixRoute.HOME,
                currentItemId = null,
                targetItemId = " ",
            ),
        )
    }

    @Test
    fun `marks a link handled when its destination is already visible`() {
        assertTrue(shouldMarkMediaDeepLinkHandled("movie-123", "movie-123"))
    }

    @Test
    fun `does not mark another destination handled`() {
        assertFalse(shouldMarkMediaDeepLinkHandled("movie-123", "episode-456"))
    }

    @Test
    fun `delivers a request that was never handled`() {
        assertTrue(
            shouldDeliverMediaDeepLink(
                requestSequence = 1L,
                handledSequence = null,
                itemId = "movie-123",
            ),
        )
    }

    @Test
    fun `does not deliver the same request twice`() {
        assertFalse(
            shouldDeliverMediaDeepLink(
                requestSequence = 7L,
                handledSequence = 7L,
                itemId = "movie-123",
            ),
        )
    }

    @Test
    fun `delivers the same link again when it is opened a second time`() {
        // This is the defect the sequence fixes: keyed by item id alone, the
        // second tap on an identical link was silently ignored.
        assertTrue(
            shouldDeliverMediaDeepLink(
                requestSequence = 8L,
                handledSequence = 7L,
                itemId = "movie-123",
            ),
        )
    }

    @Test
    fun `does not deliver a missing or empty request`() {
        assertFalse(shouldDeliverMediaDeepLink(null, null, "movie-123"))
        assertFalse(shouldDeliverMediaDeepLink(3L, null, "  "))
        assertFalse(shouldDeliverMediaDeepLink(3L, null, null))
    }

    @Test
    fun `a pending request routes to its own detail destination`() {
        val request = MediaDeepLinkRequest(itemId = "movie-123", sequence = 1L)
        assertEquals(MulletaFlixRoute.itemDetail("movie-123"), request.detailRoute)
    }

    @Test
    fun `two deliveries of the same link are distinct requests`() {
        val first = MediaDeepLinkRequest(itemId = "movie-123", sequence = 1L)
        val second = MediaDeepLinkRequest(itemId = "movie-123", sequence = 2L)
        assertNotEquals(first, second)
        assertTrue(shouldDeliverMediaDeepLink(second.sequence, first.sequence, second.itemId))
    }

    @Test
    fun `a link from another server is not opened against this library`() {
        // `ShareItemContent` writes `&serverId=` so the recipient does not
        // resolve the id against their own library; the value used to be parsed
        // and dropped, so the wrong item opened.
        assertFalse(shouldOpenLinkOnCurrentServer("server-B", "server-A"))
    }

    @Test
    fun `a link from this server opens normally`() {
        assertTrue(shouldOpenLinkOnCurrentServer("server-A", "server-A"))
        assertTrue("a server id is case insensitive", shouldOpenLinkOnCurrentServer("SERVER-A", "server-a"))
        assertTrue(shouldOpenLinkOnCurrentServer(" server-A ", "server-A"))
    }

    @Test
    fun `an unknown server id on either side never blocks a link`() {
        // Servers that do not report a ServerId must not lose link support.
        assertTrue(shouldOpenLinkOnCurrentServer(null, "server-A"))
        assertTrue(shouldOpenLinkOnCurrentServer("server-A", null))
        assertTrue(shouldOpenLinkOnCurrentServer("  ", "server-A"))
    }
}
