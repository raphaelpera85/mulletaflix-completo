package org.mulletaflix.core.api

private const val MAX_API_RETRIES = 2
private const val FIRST_RETRY_DELAY_MS = 250L
private const val SECOND_RETRY_DELAY_MS = 750L
private const val MAX_RETRY_AFTER_DELAY_MS = 1_500L

/** Only safe, read-only API calls may be repeated automatically. */
internal fun isRetryableApiMethod(method: String): Boolean =
    method.uppercase() in setOf("GET", "HEAD", "OPTIONS")

/** Keeps retries bounded and limited to transient HTTP responses. */
internal fun shouldRetryApiResponse(
    method: String,
    statusCode: Int,
    attempt: Int,
): Boolean =
    isRetryableApiMethod(method) &&
        attempt < MAX_API_RETRIES &&
        (statusCode == 408 || statusCode == 425 || statusCode == 429 || statusCode in 500..599)

/** Keeps connection failures on safe requests recoverable without an infinite loop. */
internal fun shouldRetryApiFailure(method: String, attempt: Int): Boolean =
    isRetryableApiMethod(method) && attempt < MAX_API_RETRIES

/** Uses the server hint when safe, otherwise a short exponential backoff. */
internal fun apiRetryDelayMs(attempt: Int, retryAfterHeader: String?): Long {
    val serverDelay = retryAfterHeader
        ?.trim()
        ?.toLongOrNull()
        ?.takeIf { it >= 0L }
        ?.times(1_000L)
        ?.coerceAtMost(MAX_RETRY_AFTER_DELAY_MS)
    return serverDelay ?: when (attempt) {
        0 -> FIRST_RETRY_DELAY_MS
        else -> SECOND_RETRY_DELAY_MS
    }
}
