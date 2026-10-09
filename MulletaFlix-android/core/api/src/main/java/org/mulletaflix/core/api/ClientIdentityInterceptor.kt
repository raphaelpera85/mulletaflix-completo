package org.mulletaflix.core.api

import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.mulletaflix.core.common.session.FeedbackRequestSession
import org.mulletaflix.core.common.network.CleartextTrafficPolicy
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy
import okhttp3.HttpUrl
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject
import javax.inject.Singleton

/** Product name used to build the User-Agent of every app request. */
const val MULLETAFLIX_USER_AGENT_PRODUCT = "MulletaFlix-Android"

internal data class DeferredClientIdentity(
    val accessToken: String?,
    val deviceId: String,
)

internal class DeferredClientIdentityOrigin(
    val scheme: String,
    val host: String,
    val port: Int,
) {
    @Volatile
    private var cachedIdentity: DeferredClientIdentity? = null

    fun matches(url: HttpUrl): Boolean = scheme == url.scheme && host == url.host && port == url.port

    fun identityOrCache(identityProvider: () -> DeferredClientIdentity): DeferredClientIdentity {
        cachedIdentity?.let { return it }
        return synchronized(this) {
            cachedIdentity ?: identityProvider().also { cachedIdentity = it }
        }
    }
}

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
 * Adds the MulletaFlix client identity and, except during public server
 * verification, the active session token to a request.
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

    /**
     * Returns the network interceptor for clients that defer HTTP identity until
     * the physical route has passed [CleartextTrafficPolicy]. Register it after
     * [enforceLocalNetworkCleartextPolicy] on every client using this interceptor.
     */
    fun identityAfterConnectedRoute(): Interceptor = Interceptor { chain ->
        val request = chain.request()
        val deferredOrigin = request.tag(DeferredClientIdentityOrigin::class.java)
        if (deferredOrigin == null || !deferredOrigin.matches(request.url)) {
            return@Interceptor chain.proceed(request)
        }

        // This network interceptor is registered after the connected-route guard.
        // Reuse the captured identity on same-origin retries. Cross-origin redirects
        // never match this tag and cannot receive the cached credentials.
        val alreadyAuthenticated = request.header("Authorization")
            ?.startsWith("MediaBrowser ") == true
        if (alreadyAuthenticated) return@Interceptor chain.proceed(request)

        val identity = deferredOrigin.identityOrCache {
            request.tag(FeedbackRequestSession::class.java).let { requestSession ->
                val isPublicServerVerification =
                    request.tag(PublicServerVerificationRequest::class.java) != null
                DeferredClientIdentity(
                    accessToken = if (isPublicServerVerification) {
                        null
                    } else {
                        requestSession?.accessToken
                            ?: runBlocking { sessionRepository.getAccessToken().first() }
                    },
                    deviceId = if (isPublicServerVerification) {
                        ""
                    } else {
                        requestSession?.deviceId
                            ?: runBlocking { sessionRepository.getDeviceId().first() }
                    },
                )
            }
        }

        chain.proceed(
            request.newBuilder()
                .header("User-Agent", "$MULLETAFLIX_USER_AGENT_PRODUCT/${BuildConfig.CLIENT_VERSION}")
                .header(
                    "Authorization",
                    buildMediaBrowserAuthorizationHeader(identity.accessToken, identity.deviceId),
                )
                .build(),
        )
    }

    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        // This check must precede all session reads and credential construction.
        CleartextTrafficPolicy.requireAllowed(request.url)
        if (request.url.scheme == "http" && !CleartextTrafficPolicy.isLoopbackHost(request.url.host)) {
            // Hostnames such as .local are only hints. Defer session reads until
            // the network interceptor validates the actual connected LAN route.
            val deferredRequest = request.newBuilder()
                .tag(
                    DeferredClientIdentityOrigin::class.java,
                    DeferredClientIdentityOrigin(request.url.scheme, request.url.host, request.url.port),
                )
                .removeHeader("Authorization")
                .header("User-Agent", "$MULLETAFLIX_USER_AGENT_PRODUCT/${BuildConfig.CLIENT_VERSION}")
                .build()
            return chain.proceed(deferredRequest)
        }

        val requestSession = request.tag(FeedbackRequestSession::class.java)
        val isPublicServerVerification =
            request.tag(PublicServerVerificationRequest::class.java) != null
        val token = if (isPublicServerVerification) {
            null
        } else {
            requestSession?.accessToken
                ?: runBlocking { sessionRepository.getAccessToken().first() }
        }
        val deviceId = if (isPublicServerVerification) {
            ""
        } else {
            requestSession?.deviceId
                ?: runBlocking { sessionRepository.getDeviceId().first() }
        }

        val authenticatedRequest = request.newBuilder()
            // `header` (not `addHeader`) keeps a single value even after a retry
            // re-enters this interceptor.
            .header("User-Agent", "$MULLETAFLIX_USER_AGENT_PRODUCT/${BuildConfig.CLIENT_VERSION}")
            .header("Authorization", buildMediaBrowserAuthorizationHeader(token, deviceId))
            .build()

        return chain.proceed(authenticatedRequest)
    }
}

/**
 * Builds the OkHttp client used to fetch artwork.
 *
 * Kept next to the interceptors instead of inline in the Application so its
 * contract is unit-testable: every artwork request must reach the server with
 * the same `Authorization` and `User-Agent` as an API call, otherwise the server
 * sees the cover grid as anonymous traffic and throttles it to 30 requests per
 * 10 s per IP.
 */
fun buildAuthenticatedImageClient(
    serverUrlInterceptor: Interceptor,
    clientIdentityInterceptor: ClientIdentityInterceptor,
): okhttp3.OkHttpClient = okhttp3.OkHttpClient.Builder()
    .addInterceptor(serverUrlInterceptor)
    .addInterceptor(clientIdentityInterceptor)
    .enforceLocalNetworkCleartextPolicy()
    .addNetworkInterceptor(clientIdentityInterceptor.identityAfterConnectedRoute())
    .connectTimeout(30, java.util.concurrent.TimeUnit.SECONDS)
    .readTimeout(30, java.util.concurrent.TimeUnit.SECONDS)
    .build()
