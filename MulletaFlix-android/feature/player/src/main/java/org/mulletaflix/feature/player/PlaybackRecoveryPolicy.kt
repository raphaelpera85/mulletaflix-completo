package org.mulletaflix.feature.player

import androidx.media3.common.PlaybackException

private const val MAX_AUTOMATIC_NETWORK_RETRIES = 1

/** Retries only transient transport failures; codec/auth/content errors stay visible. */
internal fun shouldRetryPlayback(errorCode: Int, attempt: Int): Boolean =
    attempt < MAX_AUTOMATIC_NETWORK_RETRIES && errorCode in setOf(
        PlaybackException.ERROR_CODE_TIMEOUT,
        PlaybackException.ERROR_CODE_IO_UNSPECIFIED,
        PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
        PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT,
    )
