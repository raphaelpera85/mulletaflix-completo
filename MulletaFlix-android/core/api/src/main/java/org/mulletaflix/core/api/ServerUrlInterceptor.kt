package org.mulletaflix.core.api

import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Dynamically rewrites the host, scheme, and port of requests
 * to point to the user's currently configured MulletaFlix server.
 */
@Singleton
class ServerUrlInterceptor @Inject constructor(
    private val sessionRepository: SessionRepository
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        var request = chain.request()
        val currentServerUrl = runBlocking {
            sessionRepository.getBaseUrl().first()
        }

        if (currentServerUrl.isNotBlank()) {
            val serverHttpUrl = currentServerUrl.toHttpUrlOrNull()
            if (serverHttpUrl != null) {
                val newUrl = request.url.newBuilder()
                    .scheme(serverHttpUrl.scheme)
                    .host(serverHttpUrl.host)
                    .port(serverHttpUrl.port)
                    .build()
                request = request.newBuilder().url(newUrl).build()
            }
        }

        return chain.proceed(request)
    }
}
