package org.mulletaflix.feature.player

internal data class BufferPolicy(
    val minBufferMs: Int,
    val maxBufferMs: Int,
    val bufferForPlaybackMs: Int,
    val bufferForPlaybackAfterRebufferMs: Int,
)

/** Shared by playback and offline downloads so both Media3 transports evolve together. */
const val MEDIA_CONNECT_TIMEOUT_MS = 15_000
const val MEDIA_READ_TIMEOUT_MS = 30_000

/** Balanced for remote playback: start quickly, then keep enough headroom for network jitter. */
internal fun streamingBufferPolicy(): BufferPolicy = BufferPolicy(
    minBufferMs = 20_000,
    maxBufferMs = 60_000,
    bufferForPlaybackMs = 1_500,
    bufferForPlaybackAfterRebufferMs = 4_000,
)

/** Local/offline playback needs less buffering because the source is on-device. */
internal fun localBufferPolicy(): BufferPolicy = BufferPolicy(
    minBufferMs = 5_000,
    maxBufferMs = 30_000,
    bufferForPlaybackMs = 250,
    bufferForPlaybackAfterRebufferMs = 750,
)
