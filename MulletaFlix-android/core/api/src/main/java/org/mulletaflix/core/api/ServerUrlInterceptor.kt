package org.mulletaflix.core.api

import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.mulletaflix.core.common.session.FeedbackRequestSession
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.HttpUrl
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Rewrites requests to the configured server, with explicit routes for captured
 * sessions and credential-free public server verification.
 */
@Singleton
class ServerUrlInterceptor @Inject constructor(
    private val sessionRepository: SessionRepository
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        var request = chain.request()
        val requestSession = request.tag(FeedbackRequestSession::class.java)
        val currentServerUrl = runBlocking {
            request.tag(PublicServerVerificationRequest::class.java)?.serverUrl
                ?: requestSession?.serverUrl
                ?: sessionRepository.getBaseUrl().first()
        }

        if (currentServerUrl.isNotBlank()) {
            val serverHttpUrl = currentServerUrl.toHttpUrlOrNull()
            if (request.tag(PublicServerVerificationRequest::class.java) != null && serverHttpUrl == null) {
                throw IllegalStateException("Candidate server URL is invalid")
            }
            if (serverHttpUrl == null && requestSession != null) {
                throw IllegalStateException("Captured feedback server URL is invalid")
            }
            if (serverHttpUrl != null) {
                val newUrl = rewriteServerUrl(request.url, serverHttpUrl)
                request = request.newBuilder().url(newUrl).build()
            }
        }

        return chain.proceed(request)
    }
}

/** Rewrites the endpoint while preserving an optional server installation path. */
internal fun rewriteServerUrl(requestUrl: HttpUrl, serverUrl: HttpUrl): HttpUrl {
    val serverPath = serverUrl.encodedPath.trimEnd('/')
    val requestPath = requestUrl.encodedPath
    val combinedPath = if (serverPath.isBlank()) {
        requestPath
    } else {
        "$serverPath/${requestPath.trimStart('/')}"
    }

    return requestUrl.newBuilder()
        .scheme(serverUrl.scheme)
        .host(serverUrl.host)
        .port(serverUrl.port)
        .encodedPath(combinedPath)
        .build()
}
