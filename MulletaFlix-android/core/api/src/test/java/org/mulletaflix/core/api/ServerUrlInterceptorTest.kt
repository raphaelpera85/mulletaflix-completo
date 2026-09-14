package org.mulletaflix.core.api

import okhttp3.HttpUrl.Companion.toHttpUrl
import org.junit.Assert.assertEquals
import org.junit.Test

class ServerUrlInterceptorTest {
    @Test
    fun `rewrites host and preserves api path for root server`() {
        val request = "http://localhost:8096/Users/user-1/Items?Limit=20".toHttpUrl()
        val server = "https://media.example.com".toHttpUrl()

        assertEquals(
            "https://media.example.com/Users/user-1/Items?Limit=20",
            rewriteServerUrl(request, server).toString(),
        )
    }

    @Test
    fun `prefixes api path with installation path`() {
        val request = "http://localhost:8096/Items/item-1/Images/Primary?tag=abc".toHttpUrl()
        val server = "https://media.example.com/jellyfin/".toHttpUrl()

        assertEquals(
            "https://media.example.com/jellyfin/Items/item-1/Images/Primary?tag=abc",
            rewriteServerUrl(request, server).toString(),
        )
    }
}
