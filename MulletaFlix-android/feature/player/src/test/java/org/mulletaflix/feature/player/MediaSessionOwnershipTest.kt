package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The bridge lends the screen's player to the playback service; it does not own it.
 *
 * It used to release whatever player it was holding. Every caller already releases its
 * own player, so in the ordinary path that was redundant, and in the path that matters
 * — `attach` with a new player while the previous owner is still alive — it destroyed
 * a player another component still held and would still hand to Media3.
 *
 * `MediaSession` is final and the bridge is a singleton, so the behaviour cannot be
 * reached from a JVM test. The decision is therefore a function, and this asserts the
 * decision rather than describing it in a comment.
 */
class MediaSessionOwnershipTest {

    @Test
    fun `the session is released only when neither the ui nor the service wants it`() {
        assertFalse(shouldReleaseSession(uiOwner = true, serviceOwner = true))
        assertFalse(shouldReleaseSession(uiOwner = true, serviceOwner = false))
        assertFalse(shouldReleaseSession(uiOwner = false, serviceOwner = true))
        assertTrue(shouldReleaseSession(uiOwner = false, serviceOwner = false))
    }

    @Test
    fun `the bridge never releases the player it borrowed`() {
        assertFalse(
            "the bridge does not create the player, so releasing it destroys an object " +
                "another component still owns and still hands to Media3",
            shouldReleasePlayer(),
        )
    }
}
