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

    @Test
    fun `artwork carries the session token so the server does not see it as anonymous`() {
        // A cover grid that reaches the server as anonymous is throttled by the
        // server's RateLimitMiddleware (30 requests / 10 s) and loads slowly.
        val url = resolveMediaUrl(
            baseUrl = "http://192.168.15.9:8096",
            path = "Items/movie-1/Images/Primary",
            accessToken = "session-token",
        )
        assertEquals(
            "http://192.168.15.9:8096/Items/movie-1/Images/Primary?api_key=session-token",
            url,
        )
    }

    @Test
    fun `token without an image path still yields no url`() {
        assertNull(resolveMediaUrl("http://server", null, "session-token"))
        assertNull(resolveMediaUrl("http://server", "  ", "session-token"))
    }

    @Test
    fun `diagnostics never print the token`() {
        val redacted = redactToken(
            "http://server/Items/x/Images/Primary?tag=t&api_key=super-secret-token",
        )
        assertEquals(
            "http://server/Items/x/Images/Primary?tag=t&api_key=<redacted>",
            redacted,
        )
        org.junit.Assert.assertFalse(redacted.contains("super-secret-token"))
    }

    @Test
    fun `diagnostics redact every token spelling the server accepts`() {
        listOf("api_key", "ApiKey", "X-Emby-Token").forEach { parameter ->
            val redacted = redactToken("http://server/Items/x?$parameter=abc123&tag=t")
            assertEquals(
                "http://server/Items/x?$parameter=<redacted>&tag=t",
                redacted,
            )
        }
    }
}
