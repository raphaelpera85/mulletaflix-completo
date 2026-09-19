package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import org.junit.Test

class AuthHeaderTest {

    @Test
    fun `anonymous header advertises the current client version`() {
        assertEquals(
            "MediaBrowser Client=\"MulletaFlix Android\", Device=\"Android\", DeviceId=\"device-1\", Version=\"${BuildConfig.CLIENT_VERSION}\"",
            buildMediaBrowserAuthorizationHeader(null, "device-1"),
        )
    }

    @Test
    fun `authenticated header includes token and current client version`() {
        assertEquals(
            "MediaBrowser Token=\"token-1\", Client=\"MulletaFlix Android\", Device=\"Android\", DeviceId=\"device-1\", Version=\"${BuildConfig.CLIENT_VERSION}\"",
            buildMediaBrowserAuthorizationHeader("token-1", "device-1"),
        )
    }
}
