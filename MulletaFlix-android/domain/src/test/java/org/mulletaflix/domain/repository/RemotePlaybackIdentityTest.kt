package org.mulletaflix.domain.repository

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RemotePlaybackIdentityTest {
    @Test
    fun `stable server id allows lan and public urls only for same account`() {
        val public = RemotePlaybackIdentity("server-1", "https://mulletaflix.example", "user-1")
        val lan = RemotePlaybackIdentity("SERVER-1", "http://192.168.1.20:8096", "user-1")

        assertTrue(public.matches(lan))
        assertFalse(public.matches(lan.copy(userId = "user-2")))
        assertFalse(public.matches(lan.copy(serverId = "server-2")))
    }

    @Test
    fun `without server id requires the same normalized endpoint and account`() {
        val endpoint = RemotePlaybackIdentity(null, "HTTPS://MULLETAFLIX.example:443/", "user-1")

        assertTrue(endpoint.matches(endpoint.copy(serverUrl = "https://mulletaflix.example")))
        assertFalse(endpoint.matches(endpoint.copy(serverUrl = "https://other.example")))
        assertFalse(endpoint.matches(endpoint.copy(userId = "user-2")))
        assertFalse(endpoint.matches(endpoint.copy(serverId = "server-1")))
    }
}
