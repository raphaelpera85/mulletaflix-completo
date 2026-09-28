package org.mulletaflix.android.service

import android.content.SharedPreferences
import android.system.ErrnoException
import android.system.OsConstants
import androidx.media3.common.util.UnstableApi
import androidx.media3.exoplayer.offline.Download
import java.util.Collections
import java.util.IdentityHashMap

/** Converts Media3's technical failure code into an actionable user-facing message. */
@UnstableApi
internal fun downloadFailureMessage(failureReason: Int, insufficientStorage: Boolean = false): String? {
    if (failureReason == Download.FAILURE_REASON_NONE) return null
    if (insufficientStorage) {
        return "Armazenamento insuficiente. Libere espaço no dispositivo e tente baixar novamente. " +
            "(código Media3 $failureReason)"
    }
    return when (failureReason) {
        Download.FAILURE_REASON_UNKNOWN -> "Falha desconhecida. Tente baixar novamente."
        else -> "Não foi possível concluir o download. Tente novamente (código Media3 $failureReason)."
    }
}

/** Uses the OS error code, not localized exception text, to identify exhausted storage. */
internal fun isInsufficientStorageFailure(failure: Throwable?): Boolean {
    val seen = Collections.newSetFromMap(IdentityHashMap<Throwable, Boolean>())
    var cause = failure
    while (cause != null && seen.add(cause)) {
        if (cause is ErrnoException && cause.errno == OsConstants.ENOSPC) return true
        cause = cause.cause
    }
    return false
}

@UnstableApi
internal fun recordDownloadFailure(
    preferences: SharedPreferences,
    requestId: String,
    state: Int,
    finalException: Throwable?,
) {
    val key = downloadFailureMetadataKey(requestId)
    when {
        state == Download.STATE_FAILED && finalException != null -> preferences.edit()
            .putBoolean(key, isInsufficientStorageFailure(finalException))
            .apply()
        state != Download.STATE_FAILED -> preferences.edit().remove(key).apply()
    }
}

internal fun clearDownloadFailure(preferences: SharedPreferences, requestId: String) {
    preferences.edit().remove(downloadFailureMetadataKey(requestId)).apply()
}

internal fun isDownloadFailureDueToInsufficientStorage(
    preferences: SharedPreferences,
    requestId: String,
): Boolean = preferences.getBoolean(downloadFailureMetadataKey(requestId), false)

internal fun downloadFailureMetadataKey(requestId: String): String =
    "failure_insufficient_storage:$requestId"
