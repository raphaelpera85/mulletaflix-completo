package org.mulletaflix.designsystem.media

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
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
        listOf("api_key", "ApiKey", "api-key", "X-Emby-Token", "X-MediaBrowser-Token").forEach { parameter ->
            val redacted = redactToken("http://server/Items/x?$parameter=abc123&tag=t")
            assertEquals(
                "http://server/Items/x?$parameter=<redacted>&tag=t",
                redacted,
            )
        }
    }

    /**
     * The spellings the old deny-list did **not** know.
     *
     * This is the test that fails against `redactToken` as a list of three literals:
     * every parameter here carried its value into the diagnostic line verbatim. The
     * rule is now "a parameter whose name contains `key` or `token` is a secret", so a
     * spelling added on the server side tomorrow is redacted without anyone
     * remembering this file.
     */
    @Test
    fun `diagnostics redact a token spelling the deny-list never listed`() {
        listOf(
            "api_token",
            "apikey",
            "apiToken",
            "token",
            "access_token",
            "authToken",
            "apikey_v2",
        ).forEach { parameter ->
            val redacted = redactToken("http://server/Items/x?$parameter=super-secret&tag=t")
            assertEquals(
                "http://server/Items/x?$parameter=<redacted>&tag=t",
                redacted,
            )
            org.junit.Assert.assertFalse(
                "$parameter leaked its value",
                redacted.contains("super-secret"),
            )
        }
    }

    @Test
    fun `diagnostics redact a password embedded in the server address`() {
        val redacted = redactToken("http://raphael:s3cr3t@192.168.15.9:8096/Items/x?tag=t")
        assertEquals("http://raphael:<redacted>@192.168.15.9:8096/Items/x?tag=t", redacted)
        org.junit.Assert.assertFalse(redacted.contains("s3cr3t"))
    }

    @Test
    fun `diagnostics keep a harmless parameter readable and redact only its own value`() {
        val redacted = redactToken(
            "http://server/Items/x?tag=abc&api_key=super-secret&width=300",
        )
        assertEquals(
            "http://server/Items/x?tag=abc&api_key=<redacted>&width=300",
            redacted,
        )
    }

    // ── retargetMediaUrl ─────────────────────────────────────────────────────
    //
    // A download URL is written into the Media3 index and outlives the address it was
    // built with; a prepared player holds one too. "Tentar novamente" used to re-add
    // exactly what was stored, so a download queued at home kept being retried against
    // the LAN address after the app had switched to the public one.

    @Test
    fun `retargets a stored download to the address in use now`() {
        val retargeted = retargetMediaUrl(
            storedUrl = "http://192.168.1.10:8096/Videos/item-1/stream?MediaSourceId=s1&api_key=OLD&Static=true",
            baseUrl = "http://mulletaflix.duckdns.org:8096",
            accessToken = "OLD",
        )
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/Videos/item-1/stream?MediaSourceId=s1&api_key=OLD&Static=true",
            retargeted,
        )
    }

    @Test
    fun `retargeting replaces the stored credential instead of appending a second one`() {
        val retargeted = retargetMediaUrl(
            storedUrl = "http://192.168.1.10:8096/Videos/item-1/stream?api_key=OLD",
            baseUrl = "http://mulletaflix.duckdns.org:8096",
            accessToken = "NEW",
        )
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/Videos/item-1/stream?api_key=NEW",
            retargeted,
        )
        assertFalse("the old session token survived", retargeted.contains("OLD"))
    }

    @Test
    fun `retargeting adds the credential when the stored url has none`() {
        val retargeted = retargetMediaUrl(
            storedUrl = "http://192.168.1.10:8096/Videos/item-1/stream?Static=true",
            baseUrl = "https://public.example:443",
            accessToken = "NEW",
        )
        assertEquals(
            "https://public.example:443/Videos/item-1/stream?Static=true&api_key=NEW",
            retargeted,
        )
    }

    @Test
    fun `retargeting keeps the absolute path so an installation prefix is not doubled`() {
        // Both addresses are the same installation: `/jellyfin` belongs to the path
        // already stored, and prefixing the base path again would ask for
        // `/jellyfin/jellyfin/...`.
        val retargeted = retargetMediaUrl(
            storedUrl = "http://192.168.1.10:8096/jellyfin/Videos/item-1/stream?api_key=OLD",
            baseUrl = "http://mulletaflix.duckdns.org:8096/jellyfin/",
            accessToken = "NEW",
        )
        assertEquals(
            "http://mulletaflix.duckdns.org:8096/jellyfin/Videos/item-1/stream?api_key=NEW",
            retargeted,
        )
    }

    @Test
    fun `retargeting drops the old port when the new address has none`() {
        val retargeted = retargetMediaUrl(
            storedUrl = "http://192.168.1.10:8096/Videos/item-1/stream",
            baseUrl = "https://public.example",
            accessToken = null,
        )
        assertEquals("https://public.example/Videos/item-1/stream", retargeted)
    }

    @Test
    fun `retargeting is a no-op without an address or a parseable url`() {
        val stored = "http://192.168.1.10:8096/Videos/item-1/stream?api_key=OLD"
        assertEquals(
            "a session that has not been read yet must not rewrite the address",
            stored,
            retargetMediaUrl(stored, baseUrl = "", accessToken = "NEW"),
        )
        assertEquals(
            "a relative url has no address to rewrite",
            "Videos/item-1/stream",
            retargetMediaUrl("Videos/item-1/stream", "http://server:8096", "NEW"),
        )
        assertEquals(
            "a base that cannot be parsed must not cost the download its url",
            stored,
            retargetMediaUrl(stored, baseUrl = "não é um endereço", accessToken = "NEW"),
        )
    }

    @Test
    fun `an absolute url keeps one credential whatever the server called it`() {
        // The append branch used to look only for `api_key`, so an absolute URL the
        // server had already authenticated as `ApiKey` came back with a second
        // credential — with the same single-definition rule as the redaction.
        listOf("api_key", "ApiKey", "X-Emby-Token").forEach { parameter ->
            val url = resolveMediaUrl(
                baseUrl = "http://server:8096",
                path = "http://server:8096/image.jpg?$parameter=existing",
                accessToken = "session-token",
            )
            assertEquals(
                "http://server:8096/image.jpg?$parameter=existing",
                url,
            )
        }
    }

    // ── canonicalImageCacheKey ───────────────────────────────────────────────
    //
    // Coil keys its caches on the request URL, which carries both the address and the
    // session token. Without a canonical key, entering and leaving home threw the whole
    // cover grid away and kept two copies of every poster on disk.

    @Test
    fun `the same artwork has one key across the lan and the public address`() {
        val lan = canonicalImageCacheKey(
            "http://192.168.15.9:8096/Items/movie-1/Images/Primary?tag=t&api_key=OLD",
            serverId = "server-abc",
        )
        val public = canonicalImageCacheKey(
            "http://mulletaflix.duckdns.org:8096/Items/movie-1/Images/Primary?tag=t&api_key=NEW",
            serverId = "server-abc",
        )
        assertEquals(lan, public)
        assertEquals("mulletaflix|server-abc|/Items/movie-1/Images/Primary|tag=t", lan)
        assertFalse("the session token must not be part of the key", lan!!.contains("OLD"))
    }

    @Test
    fun `the key survives a new session token on the same address`() {
        // Isolates the credential stripping from the address normalisation: same host,
        // same path, same tag, only the token differs — as it does after a re-login.
        assertEquals(
            canonicalImageCacheKey("http://server:8096/Items/x/Images/Primary?tag=t&api_key=OLD", "s"),
            canonicalImageCacheKey("http://server:8096/Items/x/Images/Primary?tag=t&api_key=NEW", "s"),
        )
    }

    @Test
    fun `a different tag or size is a different picture`() {
        val base = "http://server:8096/Items/movie-1/Images/Primary"
        val tagOne = canonicalImageCacheKey("$base?tag=one&api_key=t", "server-abc")
        val tagTwo = canonicalImageCacheKey("$base?tag=two&api_key=t", "server-abc")
        val small = canonicalImageCacheKey("$base?tag=one&width=300&api_key=t", "server-abc")
        assertFalse("a new image tag is a new picture", tagOne == tagTwo)
        assertFalse("a thumbnail must not answer a full-size request", tagOne == small)
    }

    @Test
    fun `two servers never share an entry`() {
        val url = "http://server-a:8096/Items/movie-1/Images/Primary?tag=t"
        assertFalse(
            canonicalImageCacheKey(url, "server-a") == canonicalImageCacheKey(url, "server-b"),
        )
        assertEquals(
            canonicalImageCacheKey(url, null),
            canonicalImageCacheKey(url, "  "),
        )
    }

    @Test
    fun `a local resource keeps its default key`() {
        // The login background and the app logo are not served by a server; giving them
        // a normalised key would put two different drawables in one entry.
        assertNull(canonicalImageCacheKey("2131689472", "server-abc"))
        assertNull(canonicalImageCacheKey("file:///data/user/0/app/files/cover.jpg", "server-abc"))
        assertNull(canonicalImageCacheKey("", "server-abc"))
    }
}
