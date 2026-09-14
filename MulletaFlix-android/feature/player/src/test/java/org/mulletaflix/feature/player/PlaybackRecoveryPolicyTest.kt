package org.mulletaflix.feature.player

import androidx.media3.common.PlaybackException
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackRecoveryPolicyTest {
    @Test
    fun `retries transient network failures once`() {
        assertTrue(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED, 0))
        assertTrue(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT, 0))
        assertFalse(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED, 1))
    }

    @Test
    fun `does not retry permanent media failures`() {
        assertFalse(shouldRetryPlayback(PlaybackException.ERROR_CODE_DECODING_FAILED, 0))
        assertFalse(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_BAD_HTTP_STATUS, 0))
    }
}
