package org.mulletaflix.data.repository

import org.junit.Assert.assertEquals
import org.junit.Test

class OfficialServerUrlMigrationTest {
    @Test
    fun `migrates the previous public endpoint with optional trailing slash`() {
        assertEquals(
            "https://mulletaflix.duckdns.org",
            migrateLegacyOfficialServerUrl("http://mulletaflix.duckdns.org:8096/"),
        )
        assertEquals(
            "https://mulletaflix.duckdns.org",
            migrateLegacyOfficialServerUrl(" HTTP://MULLETAFLIX.DUCKDNS.ORG:8096 "),
        )
    }

    @Test
    fun `does not rewrite local or custom server addresses`() {
        assertEquals(
            "http://192.168.1.20:8096/",
            migrateLegacyOfficialServerUrl("http://192.168.1.20:8096/"),
        )
        assertEquals(
            "http://media.example.org:8096",
            migrateLegacyOfficialServerUrl("http://media.example.org:8096"),
        )
        assertEquals(
            "  http://media.example.org:8096/  ",
            migrateLegacyOfficialServerUrl("  http://media.example.org:8096/  "),
        )
    }
}
