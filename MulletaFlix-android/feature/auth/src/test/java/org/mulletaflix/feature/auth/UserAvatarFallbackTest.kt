package org.mulletaflix.feature.auth

import org.junit.Assert.assertEquals
import org.junit.Test

class UserAvatarFallbackTest {
    @Test
    fun `avatar fallback uses the first unicode character`() {
        assertEquals("R", userInitial(" Raphael "))
        assertEquals("É", userInitial("Érica"))
    }

    @Test
    fun `avatar fallback handles missing names`() {
        assertEquals("?", userInitial("   "))
    }
}
