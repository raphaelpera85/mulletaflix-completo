package org.mulletaflix.android

import android.security.NetworkSecurityPolicy
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.mulletaflix.core.common.network.CleartextTrafficPolicy

@RunWith(AndroidJUnit4::class)
class AndroidCleartextPolicyIntegrationTest {
    @Test
    fun packagedPlatformPolicyAndApplicationGuardPreserveTheLanContract() {
        val platformPolicy = NetworkSecurityPolicy.getInstance()

        assertFalse(
            "The public MulletaFlix host must require HTTPS in the packaged network security config",
            platformPolicy.isCleartextTrafficPermitted("mulletaflix.duckdns.org"),
        )
        assertFalse(
            "The HTTPS-only rule must include public subdomains",
            platformPolicy.isCleartextTrafficPermitted("api.mulletaflix.duckdns.org"),
        )
        assertTrue(
            "Android must allow dynamic private LAN addresses so discovery can reach self-hosted servers",
            platformPolicy.isCleartextTrafficPermitted("192.168.1.20"),
        )

        assertTrue(CleartextTrafficPolicy.isAllowed("https://mulletaflix.duckdns.org"))
        assertTrue(CleartextTrafficPolicy.isAllowed("http://192.168.1.20:8096"))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://mulletaflix.duckdns.org:8096"))
        assertFalse(CleartextTrafficPolicy.isAllowed("http://8.8.8.8:8096"))
    }
}
