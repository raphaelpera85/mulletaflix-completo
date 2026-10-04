package org.mulletaflix.android.service

import kotlinx.coroutines.CancellationException
import okhttp3.Call
import java.io.InputStream

internal sealed interface OfflineSubtitleTransferResult {
    data class Stored(val uri: String) : OfflineSubtitleTransferResult
    data class HttpFailure(val statusCode: Int) : OfflineSubtitleTransferResult
    data object SessionChanged : OfflineSubtitleTransferResult
    data object MissingBody : OfflineSubtitleTransferResult
    data object NotStored : OfflineSubtitleTransferResult
}

/** Executes one authenticated subtitle request and persists only a successful, current-session body. */
internal suspend fun transferOfflineSubtitle(
    call: Call,
    isSessionCurrent: suspend () -> Boolean,
    persistBody: (InputStream) -> String?,
): OfflineSubtitleTransferResult = call.execute().use { response ->
    if (!response.isSuccessful) return@use OfflineSubtitleTransferResult.HttpFailure(response.code)
    val body = response.body ?: return@use OfflineSubtitleTransferResult.MissingBody
    if (!isSessionCurrent()) return@use OfflineSubtitleTransferResult.SessionChanged

    val uri = try {
        persistBody(body.byteStream())
    } catch (cancelled: CancellationException) {
        throw cancelled
    } catch (_: Exception) {
        null
    }
    uri?.let(OfflineSubtitleTransferResult::Stored) ?: OfflineSubtitleTransferResult.NotStored
}
