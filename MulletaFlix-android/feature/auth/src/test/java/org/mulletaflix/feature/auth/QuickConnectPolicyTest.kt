package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import retrofit2.HttpException

class QuickConnectPolicyTest {
    @Test
    fun `polling has finite five minute budget`() {
        assertEquals(100, QUICK_CONNECT_MAX_POLL_ATTEMPTS)
    }

    @Test
    fun `unknown secret stops polling with expiration message`() {
        val error = HttpException(retrofit2.Response.error<Any>(404, okhttp3.ResponseBody.create(null, "")))
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
