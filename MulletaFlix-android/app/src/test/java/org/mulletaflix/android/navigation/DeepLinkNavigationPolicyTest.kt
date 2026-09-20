package org.mulletaflix.android.navigation

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

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
}
