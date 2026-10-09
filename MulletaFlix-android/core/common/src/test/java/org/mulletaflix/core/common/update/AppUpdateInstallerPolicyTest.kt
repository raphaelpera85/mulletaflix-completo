package org.mulletaflix.core.common.update

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class AppUpdateInstallerPolicyTest {
    @Test
    fun `recognizes only the Google Play installer`() {
        assertTrue(AppUpdateInstaller.isGooglePlayInstallerPackageName("com.android.vending"))
        assertFalse(AppUpdateInstaller.isGooglePlayInstallerPackageName("com.aptoide.pt"))
        assertFalse(AppUpdateInstaller.isGooglePlayInstallerPackageName(null))
    }
}
