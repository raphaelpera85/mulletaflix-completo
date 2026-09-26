package org.mulletaflix.feature.auth

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The auth screens must not skip themselves when the user asked to change servers.
 *
 * They auto-advance while a session exists, which is what makes the launch flow
 * work. But "Trocar de Servidor" navigates to the very same screens with the
 * session still valid, so both of them advanced and pushed a second Home:
 * the button looked dead and signing out was the only way to change servers.
 */
class AuthAutoAdvancePolicyTest {

    @Test
    fun `a saved session skips the auth screens on the normal launch path`() {
        assertTrue(shouldAutoAdvanceAuthScreen(isAuthenticated = true, switchingServer = false))
    }

    @Test
    fun `a deliberate server change keeps the auth screens visible`() {
        assertFalse(
            "the server list must stay on screen so the user can pick another server",
            shouldAutoAdvanceAuthScreen(isAuthenticated = true, switchingServer = true),
        )
        assertFalse(
            "the login form must stay on screen after the new server is chosen",
            shouldAutoAdvanceAuthScreen(isAuthenticated = true, switchingServer = true),
        )
    }

    @Test
    fun `without a session nothing advances regardless of the intent`() {
        assertFalse(shouldAutoAdvanceAuthScreen(isAuthenticated = false, switchingServer = false))
        assertFalse(shouldAutoAdvanceAuthScreen(isAuthenticated = false, switchingServer = true))
    }
}
