package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class StreamingPolicyTest {
    @Test
    fun `remote streaming keeps a jitter buffer without delaying startup`() {
        val policy = streamingBufferPolicy()

        assertTrue(policy.minBufferMs < policy.maxBufferMs)
        assertTrue(policy.bufferForPlaybackMs < policy.minBufferMs)
        assertTrue(policy.bufferForPlaybackAfterRebufferMs < policy.maxBufferMs)
        assertEquals(20_000, policy.minBufferMs)
        assertEquals(60_000, policy.maxBufferMs)
    }

    @Test
    fun `offline playback starts with a smaller local buffer`() {
        val local = localBufferPolicy()
        val streaming = streamingBufferPolicy()

        assertTrue(local.maxBufferMs < streaming.maxBufferMs)
        assertEquals(250, local.bufferForPlaybackMs)
        assertEquals(750, local.bufferForPlaybackAfterRebufferMs)
    }

    @Test
    fun `http timeouts allow slow internet connections to recover`() {
        assertEquals(15_000, MEDIA_CONNECT_TIMEOUT_MS)
        assertEquals(30_000, MEDIA_READ_TIMEOUT_MS)
    }
}
