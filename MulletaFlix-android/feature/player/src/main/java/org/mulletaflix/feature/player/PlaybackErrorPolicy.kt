package org.mulletaflix.feature.player

import androidx.media3.common.PlaybackException
import org.mulletaflix.designsystem.media.redactToken

internal const val NETWORK_WAITING_PLAYBACK_MESSAGE =
    "Conexão perdida. A reprodução continuará quando a rede voltar."

/**
 * Converts low-level Media3 failures into actionable messages for viewers.
 *
 * The fallback is whatever Media3 put in `localizedMessage`, and some of its codes
 * embed the URL they failed on — which for a playback URL carries the session token
 * as `api_key`. Every code known to do that is mapped to a curated message above, so
 * the path is unreachable today; the redaction is here so that a code this list has
 * not learned yet cannot put a token on screen.
 */
internal fun userFacingPlaybackError(errorCode: Int, fallback: String?): String = when (errorCode) {
    PlaybackException.ERROR_CODE_TIMEOUT,
    PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
    PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT,
    -> "A conexão com o servidor foi interrompida. Verifique a rede e tente novamente."

    PlaybackException.ERROR_CODE_IO_FILE_NOT_FOUND ->
        "A fonte desta mídia não foi encontrada no servidor."

    PlaybackException.ERROR_CODE_DECODING_FAILED,
    PlaybackException.ERROR_CODE_DECODER_INIT_FAILED,
    -> "Este dispositivo não conseguiu decodificar o formato desta mídia."

    PlaybackException.ERROR_CODE_DRM_CONTENT_ERROR,
    PlaybackException.ERROR_CODE_DRM_LICENSE_ACQUISITION_FAILED,
    PlaybackException.ERROR_CODE_DRM_PROVISIONING_FAILED,
    -> "Não foi possível autorizar a reprodução protegida desta mídia."

    else -> fallback?.takeIf(String::isNotBlank)?.let(::redactToken)
        ?: "Não foi possível reproduzir esta mídia."
}
