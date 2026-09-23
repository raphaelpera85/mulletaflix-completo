package org.mulletaflix.feature.auth

/**
 * Whether the auth screens may skip themselves because a session already exists.
 *
 * Both screens exist for two different situations:
 *
 *  - the app launches without a usable session and the user has to walk through
 *    them, where auto-advancing past a screen that is already satisfied is
 *    right;
 *  - a signed-in user deliberately asks to change servers from the profile
 *    screen, where it is wrong.
 *
 * That second case was broken: the server list composed, its own `AuthViewModel`
 * read the still-valid saved session, and it sent the user to the login screen,
 * which did the same and pushed a second Home. "Trocar de Servidor" looked like a
 * dead button and the only way to change servers was to sign out first.
 */
internal fun shouldAutoAdvanceAuthScreen(
    isAuthenticated: Boolean,
    switchingServer: Boolean,
): Boolean = isAuthenticated && !switchingServer
