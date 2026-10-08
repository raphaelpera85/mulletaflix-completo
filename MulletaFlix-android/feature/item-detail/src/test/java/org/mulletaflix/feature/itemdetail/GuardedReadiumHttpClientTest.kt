package org.mulletaflix.feature.itemdetail

import java.util.concurrent.TimeUnit
import okhttp3.OkHttpClient
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.util.AbsoluteUrl
import org.readium.r2.shared.util.http.HttpRequest
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config
import kotlin.time.Duration
import kotlin.time.Duration.Companion.milliseconds

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class GuardedReadiumHttpClientTest {
    @Test
    fun readiumRequestTimeoutsOverrideDefaultsAndZeroMeansNoTimeout() {
        val baseClient = OkHttpClient.Builder()
            .connectTimeout(2, TimeUnit.SECONDS)
            .readTimeout(3, TimeUnit.SECONDS)
            .build()
        val request = HttpRequest(
            url = requireNotNull(AbsoluteUrl("https://book.test/book.epub")),
            connectTimeout = 17.milliseconds,
            readTimeout = Duration.ZERO,
        )

        val configuredClient = baseClient.withReadiumRequestTimeouts(request)

        assertEquals(17, configuredClient.connectTimeoutMillis)
        assertEquals(0, configuredClient.readTimeoutMillis)
        assertEquals(2_000, baseClient.connectTimeoutMillis)
        assertEquals(3_000, baseClient.readTimeoutMillis)
    }

    @Test
    fun absentReadiumTimeoutsKeepTheConfiguredClientDefaults() {
        val baseClient = OkHttpClient.Builder()
            .connectTimeout(2, TimeUnit.SECONDS)
            .readTimeout(3, TimeUnit.SECONDS)
            .build()
        val request = HttpRequest(url = requireNotNull(AbsoluteUrl("https://book.test/book.epub")))

        val configuredClient = baseClient.withReadiumRequestTimeouts(request)

        assertEquals(2_000, configuredClient.connectTimeoutMillis)
        assertEquals(3_000, configuredClient.readTimeoutMillis)
    }
}
