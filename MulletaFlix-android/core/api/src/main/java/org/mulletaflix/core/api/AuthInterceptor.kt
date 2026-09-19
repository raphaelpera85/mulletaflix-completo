package org.mulletaflix.core.api

import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject
import javax.inject.Singleton

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
 * OkHttp interceptor that adds Authorization: Bearer <token> to every request.
 *
 * The token is retrieved synchronously from [SessionRepository] which reads
 * from the encrypted DataStore. If no token is available the request proceeds
 * without the header (public endpoints like /System/Info/Public don't need it).
 *
 * Per TODO-AUDITORIA.md P0: "Preferir Authorization: Bearer ao token HTTP
 * em query string."
 */
@Singleton
class AuthInterceptor @Inject constructor(
    private val sessionRepository: SessionRepository
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val token = runBlocking { sessionRepository.getAccessToken().first() }
        val deviceId = runBlocking { sessionRepository.getDeviceId().first() }

        val request = chain.request().newBuilder().apply {
            addHeader("Authorization", buildMediaBrowserAuthorizationHeader(token, deviceId))
        }.build()

        return chain.proceed(request)
    }
}
