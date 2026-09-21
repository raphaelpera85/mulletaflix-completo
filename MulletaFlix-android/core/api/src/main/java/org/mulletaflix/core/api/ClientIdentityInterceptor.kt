package org.mulletaflix.core.api

import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject
import javax.inject.Singleton

/** Product name used to build the User-Agent of every app request. */
const val MULLETAFLIX_USER_AGENT_PRODUCT = "MulletaFlix-Android"

/**
 * Builds the `Authorization` header the server parses into a client identity.
 *
 * The server's `AuthorizationContext` reads `Client`, `Device`, `DeviceId` and
 * `Version` from this header to name the device in its logs and device list,
 * and `Token` to authenticate the request.
 */
internal fun buildMediaBrowserAuthorizationHeader(
    accessToken: String?,
    deviceId: String,
): String {
    val clientVersion = BuildConfig.CLIENT_VERSION
    return if (!accessToken.isNullOrBlank()) {
        "MediaBrowser Token=\"$accessToken\", Client=\"MulletaFlix Android\", Device=\"Android\", DeviceId=\"$deviceId\", Version=\"$clientVersion\""
    } else {
        "MediaBrowser Client=\"MulletaFlix Android\", Device=\"Android\", DeviceId=\"$deviceId\", Version=\"$clientVersion\""
    }
}

/**
 * Adds the MulletaFlix client identity and the session token to a request.
 *
 * Two things depend on this:
 *
 * 1. **Device identification.** The server resolves `Client`, `Device`,
 *    `DeviceId` and `Version` from the `Authorization` header and lists them in
 *    its device/activity views. Without the header a request is only an
 *    anonymous IP address in the log.
 * 2. **Not being throttled as anonymous.** The server's `RateLimitMiddleware`
 *    throttles any request whose identity is not authenticated to 30 requests
 *    per 10 s per IP. A poster grid fires far more than that, so unauthenticated
 *    artwork receives HTTP 429 and the covers crawl in.
 *
 * The token is sent in `Authorization`. `resolveMediaUrl` additionally appends
 * it as `api_key` for callers that only carry a URL. The server
 * (`AuthorizationContext`) reads `Authorization`, `X-Emby-Token`,
 * `X-MediaBrowser-Token`, `ApiKey` and `api_key`, so both routes authenticate.
 */
@Singleton
class ClientIdentityInterceptor @Inject constructor(
    private val sessionRepository: SessionRepository,
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val token = runBlocking { sessionRepository.getAccessToken().first() }
        val deviceId = runBlocking { sessionRepository.getDeviceId().first() }

        val request = chain.request().newBuilder()
            // `header` (not `addHeader`) keeps a single value even after a retry
            // re-enters this interceptor.
            .header("User-Agent", "$MULLETAFLIX_USER_AGENT_PRODUCT/${BuildConfig.CLIENT_VERSION}")
            .header("Authorization", buildMediaBrowserAuthorizationHeader(token, deviceId))
            .build()

        return chain.proceed(request)
    }
}
