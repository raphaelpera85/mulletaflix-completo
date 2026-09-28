package org.mulletaflix.designsystem.components

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DeviceFeaturePolicyTest {
    @Test
    fun downloadsAreUnavailableOnTelevisionAndAvailableOnHandheldDevices() {
        assertFalse(downloadsAvailableOnDevice(isTelevision = true))
        assertTrue(downloadsAvailableOnDevice(isTelevision = false))
    }
}
