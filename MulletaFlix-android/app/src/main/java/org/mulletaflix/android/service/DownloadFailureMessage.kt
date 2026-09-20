package org.mulletaflix.android.service

import androidx.media3.exoplayer.offline.Download
import androidx.media3.common.util.UnstableApi

/** Converts Media3's technical failure code into an actionable user-facing message. */
@UnstableApi
internal fun downloadFailureMessage(failureReason: Int): String? = when (failureReason) {
    Download.FAILURE_REASON_NONE -> null
    Download.FAILURE_REASON_UNKNOWN -> "Falha desconhecida. Tente baixar novamente."
    else -> "Não foi possível concluir o download. Tente novamente (código $failureReason)."
}
