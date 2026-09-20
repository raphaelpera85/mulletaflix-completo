package org.mulletaflix.feature.player

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class NotificationPermissionPolicyTest {
    @Test
    fun `does not request notification permission below android 13`() {
        assertFalse(needsNotificationPermission(sdkInt = 32, permissionGranted = false))
    }

    @Test
    fun `requests permission on android 13 when it is missing`() {
        assertTrue(needsNotificationPermission(sdkInt = 33, permissionGranted = false))
    }

    @Test
    fun `does not request permission when already granted`() {
        assertFalse(needsNotificationPermission(sdkInt = 35, permissionGranted = true))
    }

    @Test
    fun `does not show prompt after the user dismissed it`() {
        assertFalse(
            shouldShowNotificationPermissionPrompt(
                sdkInt = 35,
                permissionGranted = false,
                promptDismissed = true,
            ),
        )
    }

    @Test
    fun `shows prompt when permission is missing and it was not dismissed`() {
        assertTrue(
            shouldShowNotificationPermissionPrompt(
                sdkInt = 35,
                permissionGranted = false,
                promptDismissed = false,
            ),
        )
    }
}
