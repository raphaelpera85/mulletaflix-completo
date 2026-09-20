package org.mulletaflix.core.api

import java.io.IOException
import okhttp3.Interceptor
import okhttp3.Response
import javax.inject.Inject

/** Retries only transient failures of read-only API calls before surfacing them. */
class ApiRetryInterceptor @Inject constructor() : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        if (!isRetryableApiMethod(request.method)) {
            return chain.proceed(request)
        }

        var attempt = 0
        while (true) {
            try {
                val response = chain.proceed(request)
                if (!shouldRetryApiResponse(request.method, response.code, attempt)) {
                    return response
                }
                val delayMs = apiRetryDelayMs(attempt, response.header("Retry-After"))
                response.close()
                waitBeforeRetry(chain, delayMs)
            } catch (error: IOException) {
                if (!shouldRetryApiFailure(request.method, attempt) || chain.call().isCanceled()) {
                    throw error
                }
                waitBeforeRetry(chain, apiRetryDelayMs(attempt, null))
            }
            attempt += 1
        }
    }

    private fun waitBeforeRetry(chain: Interceptor.Chain, delayMs: Long) {
        if (chain.call().isCanceled()) {
            throw IOException("API request cancelled")
        }
        try {
            Thread.sleep(delayMs)
        } catch (interrupted: InterruptedException) {
            Thread.currentThread().interrupt()
            throw IOException("API retry interrupted", interrupted)
        }
    }
}
