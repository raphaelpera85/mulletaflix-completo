package org.mulletaflix.android.service

import okhttp3.Interceptor
import org.junit.Assert.assertFalse
import org.junit.Test

class OfflineSubtitleNetworkPolicyTest {
    @Test
    fun `authenticated subtitle client refuses HTTP and HTTPS redirects`() {
        val client = offlineSubtitleHttpClient(Interceptor { chain -> chain.proceed(chain.request()) })

        assertFalse(client.followRedirects)
        assertFalse(client.followSslRedirects)
    }
}
