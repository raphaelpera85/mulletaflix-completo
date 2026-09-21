package org.mulletaflix.feature.player

import androidx.media3.common.PlaybackException
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PlaybackRecoveryPolicyTest {
    @Test
    fun `retries transient network failures with a bounded attempt count`() {
        assertTrue(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED, 0))
        assertTrue(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT, 0))
        assertTrue(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED, 2))
        assertFalse(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED, 3))
    }

    @Test
    fun `uses bounded exponential backoff for network retries`() {
        assertEquals(750L, playbackRetryDelayMs(0))
        assertEquals(1_500L, playbackRetryDelayMs(1))
        assertEquals(3_000L, playbackRetryDelayMs(2))
        assertEquals(3_000L, playbackRetryDelayMs(10))
    }

    @Test
    fun `does not retry permanent media failures`() {
        assertFalse(shouldRetryPlayback(PlaybackException.ERROR_CODE_DECODING_FAILED, 0))
        assertFalse(shouldRetryPlayback(PlaybackException.ERROR_CODE_IO_BAD_HTTP_STATUS, 0))
    }

    @Test
    fun `retries a remote stream when connectivity is restored`() {
        assertTrue(
            shouldRetryAfterNetworkRestored(
                wasOffline = true,
                isOnline = true,
                hasRemoteMedia = true,
                hasPlaybackError = true,
                errorCode = PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT,
            )
        )
    }

    @Test
    fun `does not retry local media or permanent errors on reconnect`() {
        assertFalse(
            shouldRetryAfterNetworkRestored(
                wasOffline = true,
                isOnline = true,
                hasRemoteMedia = false,
                hasPlaybackError = true,
                errorCode = PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
            )
        )
        assertFalse(
            shouldRetryAfterNetworkRestored(
                wasOffline = true,
                isOnline = true,
                hasRemoteMedia = true,
                hasPlaybackError = true,
                errorCode = PlaybackException.ERROR_CODE_DECODING_FAILED,
            )
        )
        assertFalse(
            shouldRetryAfterNetworkRestored(
                wasOffline = true,
                isOnline = true,
                hasRemoteMedia = true,
                hasPlaybackError = false,
                errorCode = PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
            )
        )
    }

    @Test
    fun `pauses remote retries while offline but leaves local playback alone`() {
        assertTrue(
            shouldPausePlaybackForOffline(
                isOnline = false,
                isOfflinePlayback = false,
                hasRemoteMedia = true,
                errorCode = PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
            )
        )
        assertFalse(
            shouldPausePlaybackForOffline(
                isOnline = false,
                isOfflinePlayback = true,
                hasRemoteMedia = false,
                errorCode = PlaybackException.ERROR_CODE_IO_NETWORK_CONNECTION_FAILED,
            )
        )
        assertFalse(
            shouldPausePlaybackForOffline(
                isOnline = false,
                isOfflinePlayback = false,
                hasRemoteMedia = true,
                errorCode = PlaybackException.ERROR_CODE_DECODING_FAILED,
            )
        )
    }

    @Test
    fun `invalidates a retry when the same title starts a newer load`() {
        assertFalse(isCurrentPlaybackLoad(4L, 5L, "movie", "movie"))
        assertFalse(isCurrentPlaybackLoad(4L, 4L, "movie", "episode"))
        assertTrue(isCurrentPlaybackLoad(4L, 4L, "movie", "movie"))
    }

    @Test
    fun `invalidates a load when the playback session changes`() {
        assertFalse(
            isCurrentPlaybackLoad(
                expectedGeneration = 4L,
                currentGeneration = 4L,
                expectedItemId = "movie",
                currentItemId = "movie",
                expectedSessionGeneration = 1L,
                currentSessionGeneration = 2L,
            ),
        )
        assertTrue(
            isCurrentPlaybackLoad(
                expectedGeneration = 4L,
                currentGeneration = 4L,
                expectedItemId = "movie",
                currentItemId = "movie",
                expectedSessionGeneration = 2L,
                currentSessionGeneration = 2L,
            ),
        )
    }
}
