package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class LocalServerDiscoveryTest {
    @Test
    fun `parses a valid discovery response and normalizes endpoint`() {
        val server = parseDiscoveryResponse(
            """{"Address":"HTTP://192.168.1.20:8096/","Name":"Sala","Version":"10.9.0","Id":"server-1"}"""
        )

        assertEquals(ServerInfo("Sala", "http://192.168.1.20:8096", version = "10.9.0", serverId = "server-1"), server)
    }

    @Test
    fun `uses a safe default name when discovery omits name`() {
        val server = parseDiscoveryResponse("""{"Address":"http://192.168.1.20:8096"}""")

        assertEquals("MulletaFlix Server", server?.name)
    }

    @Test
    fun `ignores malformed or unsafe discovery payloads`() {
        assertNull(parseDiscoveryResponse("not-json"))
        assertNull(parseDiscoveryResponse("""{"Address":"ftp://192.168.1.20:8096"}"""))
        assertNull(parseDiscoveryResponse("""{"Name":"Sem endereço"}"""))
    }
}
