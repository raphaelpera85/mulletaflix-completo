package org.mulletaflix.domain.repository

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DownloadServerScopeTest {
    @Test
    fun `stable server id wins over endpoint`() {
        assertEquals("server-1", downloadServerScopeId(" server-1 ", "http://lan:8096"))
        assertEquals("server-1", downloadServerScopeId("server-1", "https://public.example"))
    }

    @Test
    fun `endpoint is fallback only when stable id is absent`() {
        assertEquals("http://lan:8096", downloadServerScopeId(null, "http://lan:8096/"))
        assertEquals("http://lan:8096", downloadServerScopeId("  ", "http://lan:8096/"))
        assertNull(downloadServerScopeId(null, "  "))
    }
}
