package org.mulletaflix.designsystem.components

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * A top bar must look different when the remote is on one of its actions.
 *
 * The defect this covers: every action kept its fixed `tint` and nothing was drawn
 * around it, so a D-pad could move across a top bar with no visible result and the
 * user could not tell where the focus was. Measured on the TV emulator, the focused
 * action and an unfocused one captured byte-identical pixels.
 */
class TopBarActionFocusPolicyTest {

    @Test
    fun `a focused action on a remote device is lifted and ringed`() {
        assertEquals(1.15f, topBarActionFocusScale(focusFriendly = true, isFocused = true))
        assertEquals(2f, topBarActionFocusRingWidthDp(focusFriendly = true, isFocused = true))
        assertEquals(0.20f, topBarActionFocusBackgroundAlpha(focusFriendly = true, isFocused = true))
    }

    @Test
    fun `an unfocused action is left exactly as it was drawn`() {
        assertEquals(1f, topBarActionFocusScale(focusFriendly = true, isFocused = false))
        assertEquals(0f, topBarActionFocusRingWidthDp(focusFriendly = true, isFocused = false))
        assertEquals(0f, topBarActionFocusBackgroundAlpha(focusFriendly = true, isFocused = false))
    }

    @Test
    fun `touch devices never draw a focus ring`() {
        // On a phone these actions are tapped, never focused; a permanent red ring
        // around all of them would be noise rather than feedback.
        assertEquals(1f, topBarActionFocusScale(focusFriendly = false, isFocused = true))
        assertEquals(0f, topBarActionFocusRingWidthDp(focusFriendly = false, isFocused = true))
        assertEquals(0f, topBarActionFocusBackgroundAlpha(focusFriendly = false, isFocused = true))
    }

    @Test
    fun `the focused lift is large enough to notice and small enough not to collide`() {
        val scale = topBarActionFocusScale(focusFriendly = true, isFocused = true)
        assertTrue("a lift below 10% is not readable from a couch", scale >= 1.10f)
        assertTrue("top-bar actions sit close together, so the lift must stay modest", scale <= 1.25f)
    }
}
