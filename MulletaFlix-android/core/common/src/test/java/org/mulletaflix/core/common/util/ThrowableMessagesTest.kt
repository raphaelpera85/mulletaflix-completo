package org.mulletaflix.core.common.util

import java.io.IOException
import org.junit.Assert.assertEquals
import org.junit.Test

class ThrowableMessagesTest {
    @Test
    fun `uses fallback when localized message is absent`() {
        assertEquals("Try again", IllegalStateException().userFacingMessageOr("Try again"))
    }

    @Test
    fun `uses fallback when localized message is blank`() {
        assertEquals("Try again", IllegalStateException("  \t ").userFacingMessageOr("Try again"))
    }

    @Test
    fun `preserves meaningful localized message`() {
        assertEquals("Connection refused", IOException("Connection refused").userFacingMessageOr("Try again"))
    }
}
