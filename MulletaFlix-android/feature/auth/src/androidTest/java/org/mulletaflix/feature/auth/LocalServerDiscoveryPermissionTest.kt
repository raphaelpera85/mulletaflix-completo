package org.mulletaflix.feature.auth

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

@RunWith(AndroidJUnit4::class)
@SdkSuppress(minSdkVersion = 37)
class LocalServerDiscoveryPermissionTest {
    private val context: Context = ApplicationProvider.getApplicationContext()
    private val instrumentation = InstrumentationRegistry.getInstrumentation()

    @Before
    fun revokeLocalNetworkPermission() {
        instrumentation.uiAutomation.revokeRuntimePermission(
            context.packageName,
            Manifest.permission.ACCESS_LOCAL_NETWORK,
        )
    }

    @After
    fun restorePermissionState() {
        instrumentation.uiAutomation.revokeRuntimePermission(
            context.packageName,
            Manifest.permission.ACCESS_LOCAL_NETWORK,
        )
    }

    @Test
    fun discoveryStopsBeforeUdpWhenPermissionIsDenied() {
        assertEquals(
            PackageManager.PERMISSION_DENIED,
            context.checkSelfPermission(Manifest.permission.ACCESS_LOCAL_NETWORK),
        )

        val failure = runCatching {
            runBlocking { LocalServerDiscovery(context).discover(timeoutMs = 0) }
        }.exceptionOrNull()

        assertTrue(failure is LocalNetworkPermissionRequiredException)
    }

    @Test
    fun discoveryCanRunAfterPermissionIsGranted() {
        instrumentation.uiAutomation.grantRuntimePermission(
            context.packageName,
            Manifest.permission.ACCESS_LOCAL_NETWORK,
        )
        assertEquals(
            PackageManager.PERMISSION_GRANTED,
            context.checkSelfPermission(Manifest.permission.ACCESS_LOCAL_NETWORK),
        )

        val servers = runBlocking { LocalServerDiscovery(context).discover(timeoutMs = 0) }

        assertTrue(servers.isEmpty())
        assertTrue(Build.VERSION.SDK_INT >= 37)
    }
}
