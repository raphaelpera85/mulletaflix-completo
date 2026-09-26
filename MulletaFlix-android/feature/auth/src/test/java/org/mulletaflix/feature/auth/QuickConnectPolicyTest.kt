package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import okhttp3.ResponseBody.Companion.toResponseBody
import retrofit2.HttpException

class QuickConnectPolicyTest {
    @Test
    fun `polling has finite five minute budget`() {
        assertEquals(100, QUICK_CONNECT_MAX_POLL_ATTEMPTS)
        assertEquals(300, quickConnectDurationSeconds())
    }

    @Test
    fun `remaining time rounds up from a monotonic deadline`() {
        assertEquals(300, quickConnectRemainingSeconds(300_000))
        assertEquals(298, quickConnectRemainingSeconds(297_500))
        assertEquals(1, quickConnectRemainingSeconds(1))
        assertEquals(0, quickConnectRemainingSeconds(0))
        assertEquals(0, quickConnectRemainingSeconds(-1))
    }

    @Test
    fun `unknown secret stops polling with expiration message`() {
        val error = HttpException(retrofit2.Response.error<Any>(404, "".toResponseBody()))
        assertEquals(
            "O código Quick Connect expirou. Gere um novo código.",
            quickConnectTerminalErrorMessage(error),
        )
    }

    @Test
    fun `non-terminal network errors remain retryable`() {
        assertNull(quickConnectTerminalErrorMessage(java.io.IOException("offline")))
    }
}
