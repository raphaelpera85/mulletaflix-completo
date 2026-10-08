package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayInputStream
import java.io.FilterInputStream
import java.io.IOException
import java.util.concurrent.TimeUnit
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.time.Duration
import okhttp3.Call
import okhttp3.Callback
import okhttp3.Dns
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.RequestBody.Companion.asRequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import org.mulletaflix.core.common.network.enforceLocalNetworkCleartextPolicy
import org.readium.r2.shared.util.AbsoluteUrl
import org.readium.r2.shared.util.Try
import org.readium.r2.shared.util.http.HttpClient
import org.readium.r2.shared.util.http.HttpError
import org.readium.r2.shared.util.http.HttpRequest
import org.readium.r2.shared.util.http.HttpResponse
import org.readium.r2.shared.util.http.HttpStatus
import org.readium.r2.shared.util.http.HttpStreamResponse
import org.readium.r2.shared.util.mediatype.MediaType as ReadiumMediaType

internal fun createBookReaderHttpClient(dns: Dns = Dns.SYSTEM): HttpClient =
    GuardedReadiumHttpClient(
        OkHttpClient.Builder()
            .dns(dns)
            .connectTimeout(30, TimeUnit.SECONDS)
            .readTimeout(120, TimeUnit.SECONDS)
            .writeTimeout(30, TimeUnit.SECONDS)
            .enforceLocalNetworkCleartextPolicy()
            .build(),
    )

internal class GuardedReadiumHttpClient(
    private val client: OkHttpClient,
) : HttpClient {
    override suspend fun stream(
        request: HttpRequest,
    ): Try<HttpStreamResponse, HttpError> = suspendCancellableCoroutine { continuation ->
        val call = try {
            client.withReadiumRequestTimeouts(request).newCall(request.toOkHttpRequest())
        } catch (failure: Exception) {
            continuation.resume(Try.Failure(HttpError.IO(failure))) { _, _, _ -> }
            return@suspendCancellableCoroutine
        }

        continuation.invokeOnCancellation { call.cancel() }
        call.enqueue(object : Callback {
            override fun onFailure(call: Call, e: IOException) {
                continuation.resume(Try.Failure(HttpError.IO(e))) { _, _, _ -> }
            }

            override fun onResponse(call: Call, response: Response) {
                val result = try {
                    response.toReadiumResult(request)
                } catch (failure: Exception) {
                    response.close()
                    Try.Failure(HttpError.IO(failure))
                }
                continuation.resume(result) { _, value, _ -> value.getOrNull()?.body?.close() }
            }
        })
    }
}

internal fun OkHttpClient.withReadiumRequestTimeouts(request: HttpRequest): OkHttpClient =
    newBuilder().apply {
        request.connectTimeout?.let { connectTimeout(it.toOkHttpTimeoutMillis(), TimeUnit.MILLISECONDS) }
        request.readTimeout?.let { readTimeout(it.toOkHttpTimeoutMillis(), TimeUnit.MILLISECONDS) }
    }.build()

private fun Duration.toOkHttpTimeoutMillis(): Long {
    require(!isNegative()) { "Readium não pode definir timeout negativo." }
    if (this == Duration.ZERO || isInfinite()) return 0
    return inWholeMilliseconds.coerceAtLeast(1).coerceAtMost(Int.MAX_VALUE.toLong())
}

private fun HttpRequest.toOkHttpRequest(): Request {
    val contentType = headers.entries
        .firstOrNull { it.key.equals("Content-Type", ignoreCase = true) }
        ?.value
        ?.firstOrNull()
        ?.toMediaTypeOrNull()
    val requestBody = when (val requestBody = body) {
        null -> null
        is HttpRequest.Body.Bytes -> requestBody.bytes.toRequestBody(contentType)
        is HttpRequest.Body.File -> requestBody.file.asRequestBody(contentType)
    }

    return Request.Builder()
        .url(url.toString())
        .method(method.name, requestBody)
        .also { builder ->
            headers.forEach { (name, values) -> values.forEach { value -> builder.addHeader(name, value) } }
        }
        .build()
}

private fun Response.toReadiumResult(
    request: HttpRequest,
): Try<HttpStreamResponse, HttpError> {
    val responseMediaType = header("Content-Type")
        ?.let { contentType -> runCatching { ReadiumMediaType(contentType) }.getOrNull() }
    if (!isSuccessful) {
        close()
        return Try.Failure(HttpError.ErrorResponse(HttpStatus(code), responseMediaType))
    }

    val responseUrl = AbsoluteUrl(this.request.url.toString())
        ?: throw IOException("Readium recebeu uma URL de resposta inválida.")
    val readiumResponse = HttpResponse(
        request = request,
        url = responseUrl,
        statusCode = HttpStatus(code),
        headers = headers.toMultimap(),
        mediaType = responseMediaType,
    )
    val bodyStream = body?.byteStream() ?: ByteArrayInputStream(byteArrayOf())
    val response = this
    return Try.Success(
        HttpStreamResponse(
            response = readiumResponse,
            body = object : FilterInputStream(bodyStream) {
                override fun close() {
                    try {
                        super.close()
                    } finally {
                        response.close()
                    }
                }
            },
        ),
    )
}
