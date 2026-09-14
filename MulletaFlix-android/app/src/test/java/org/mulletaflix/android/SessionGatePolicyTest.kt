package org.mulletaflix.android

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class SessionGatePolicyTest {

    @Test
    fun `opens home when persisted session is complete`() {
        assertTrue(hasUsableSession("http://mulletaflix.duckdns.org:8096", "token", "user-id"))
    }

    @Test
    fun `requires server and identity before opening home`() {
        assertFalse(hasUsableSession("", "token", "user-id"))
        assertFalse(hasUsableSession("http://server:8096", null, "user-id"))
        assertFalse(hasUsableSession("http://server:8096", "token", null))
    }
}
