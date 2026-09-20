package org.mulletaflix.core.api

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ApiRetryPolicyTest {
    @Test
    fun `retries transient responses only for read-only methods`() {
        assertTrue(shouldRetryApiResponse("GET", 503, attempt = 0))
        assertTrue(shouldRetryApiResponse("HEAD", 429, attempt = 1))
        assertTrue(shouldRetryApiResponse("OPTIONS", 408, attempt = 0))
        assertFalse(shouldRetryApiResponse("POST", 503, attempt = 0))
        assertFalse(shouldRetryApiResponse("GET", 404, attempt = 0))
    }

    @Test
    fun `stops after the bounded retry budget`() {
        assertTrue(shouldRetryApiResponse("GET", 500, attempt = 0))
        assertTrue(shouldRetryApiResponse("GET", 500, attempt = 1))
        assertFalse(shouldRetryApiResponse("GET", 500, attempt = 2))
        assertTrue(shouldRetryApiFailure("GET", attempt = 0))
        assertFalse(shouldRetryApiFailure("GET", attempt = 2))
    }

    @Test
    fun `honors retry after without allowing a long blocking delay`() {
        assertEquals(250L, apiRetryDelayMs(attempt = 0, retryAfterHeader = null))
        assertEquals(750L, apiRetryDelayMs(attempt = 1, retryAfterHeader = null))
        assertEquals(1_500L, apiRetryDelayMs(attempt = 0, retryAfterHeader = "30"))
        assertEquals(750L, apiRetryDelayMs(attempt = 1, retryAfterHeader = "invalid"))
    }
}
