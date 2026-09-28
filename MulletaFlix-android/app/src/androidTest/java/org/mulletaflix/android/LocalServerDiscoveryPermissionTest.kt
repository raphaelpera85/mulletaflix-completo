package org.mulletaflix.android

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.filters.SdkSuppress
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.feature.auth.LocalServerDiscovery

@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = 37)
class LocalServerDiscoveryPermissionTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val instrumentation = InstrumentationRegistry.getInstrumentation()
    private var permissionGrantedBeforeTest = false

    @Before
    fun revokeLocalNetworkPermission() {
        check(context.applicationInfo.targetSdkVersion >= 37)
        permissionGrantedBeforeTest = hasLocalNetworkPermission()
        if (permissionGrantedBeforeTest) setLocalNetworkPermission(granted = false)
    }

    @After
    fun restorePermissionState() {
        if (hasLocalNetworkPermission() != permissionGrantedBeforeTest) {
            setLocalNetworkPermission(granted = permissionGrantedBeforeTest)
        }
    }

    @Test
    fun discoveryStopsBeforeUdpWhenPermissionIsDenied() {
        assertEquals(PackageManager.PERMISSION_DENIED, permissionState())

        val failure = runCatching {
            runBlocking { LocalServerDiscovery(context).discover(timeoutMs = 0) }
        }.exceptionOrNull()

        assertEquals("LocalNetworkPermissionRequiredException", failure?.javaClass?.simpleName)
    }

    @Test
    fun discoveryCanRunAfterPermissionIsGranted() {
        setLocalNetworkPermission(granted = true)
        assertEquals(PackageManager.PERMISSION_GRANTED, permissionState())

        val servers = runBlocking { LocalServerDiscovery(context).discover(timeoutMs = 0) }

        assertTrue(servers.isEmpty())
        assertTrue(Build.VERSION.SDK_INT >= 37)
    }

    private fun permissionState(): Int =
        context.checkSelfPermission(Manifest.permission.ACCESS_LOCAL_NETWORK)

    private fun hasLocalNetworkPermission(): Boolean =
        permissionState() == PackageManager.PERMISSION_GRANTED

    private fun setLocalNetworkPermission(granted: Boolean) {
        if (granted) {
            instrumentation.uiAutomation.grantRuntimePermission(
                context.packageName,
                Manifest.permission.ACCESS_LOCAL_NETWORK,
            )
        } else {
            instrumentation.uiAutomation.revokeRuntimePermission(
                context.packageName,
                Manifest.permission.ACCESS_LOCAL_NETWORK,
            )
        }
    }
}
