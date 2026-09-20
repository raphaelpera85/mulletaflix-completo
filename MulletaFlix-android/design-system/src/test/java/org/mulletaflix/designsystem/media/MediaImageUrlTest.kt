package org.mulletaflix.designsystem.media

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class MediaImageUrlTest {
    @Test
    fun `resolves relative image path against selected server`() {
        assertEquals(
            "http://lan-server:8096/Items/item-1/Images/Primary?tag=abc&api_key=token",
            resolveMediaUrl("http://lan-server:8096/", "/Items/item-1/Images/Primary?tag=abc", "token"),
        )
    }

    @Test
    fun `encodes authentication token in image query`() {
        assertEquals(
            "http://server/Users/user/Images/Primary?api_key=token%2Bwith%2Fslash",
            resolveMediaUrl("http://server", "Users/user/Images/Primary", "token+with/slash"),
        )
    }

    @Test
    fun `does not modify absolute image URLs or blank paths`() {
        assertEquals("https://cdn.example/image.jpg", resolveMediaUrl("http://server", "https://cdn.example/image.jpg", "token"))
        assertNull(resolveMediaUrl("http://server", "", "token"))
    }

    @Test
    fun `authenticates absolute image URLs from the selected server`() {
        assertEquals(
            "http://server:8096/Items/item-1/Images/Primary?tag=abc&api_key=token",
            resolveMediaUrl(
                "http://server:8096",
                "http://server:8096/Items/item-1/Images/Primary?tag=abc",
                "token",
            ),
        )
    }

    @Test
    fun `does not leak token to external image hosts or duplicate an existing token`() {
        assertEquals(
            "https://cdn.example/image.jpg",
            resolveMediaUrl("http://server:8096", "https://cdn.example/image.jpg", "token"),
        )
        assertEquals(
            "http://server:8096/image.jpg?api_key=existing",
            resolveMediaUrl(
                "http://server:8096",
                "http://server:8096/image.jpg?api_key=existing",
                "token",
            ),
        )
    }

    @Test
    fun `builds user avatar path from the server primary image tag`() {
        assertEquals(
            "Users/user-1/Images/Primary?tag=tag-1",
            userAvatarPath("user-1", "tag-1"),
        )
        assertEquals(
            "http://server/Users/user-1/Images/Primary?tag=tag-1&api_key=token",
            resolveMediaUrl("http://server", userAvatarPath("user-1", "tag-1"), "token"),
        )
    }

    @Test
    fun `has no avatar path without a user id or image tag`() {
        assertNull(userAvatarPath(null, "tag-1"))
        assertNull(userAvatarPath("user-1", null))
        assertNull(userAvatarPath("user-1", "  "))
    }
}
