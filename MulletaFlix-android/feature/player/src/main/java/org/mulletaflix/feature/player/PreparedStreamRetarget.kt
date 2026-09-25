package org.mulletaflix.feature.player

/**
 * The slice of the player that re-pointing a prepared stream needs.
 *
 * `PlayerViewModel` builds its own `ExoPlayer` internally and cannot be constructed
 * outside a running app with a Hilt graph, so the decision and the effect on the
 * player live behind this five-member interface. A test can then fake it and observe
 * exactly what the player was asked to do; the adapter that maps `ExoPlayer` onto it
 * stays a handful of lines inside the ViewModel.
 */
internal interface RetargetableStream {
    /** Address of the source that is prepared, or null when nothing is loaded. */
    fun preparedUrl(): String?

    /** Whether the loaded source is a local download rather than a server stream. */
    fun isOffline(): Boolean

    /** Position to preserve, in milliseconds. */
    fun positionMs(): Long

    /** Whether playback is meant to be running right now. */
    fun isPlaying(): Boolean

    /**
     * Replaces the source with [url], resuming at [positionMs] and playing again when
     * [resumePlayback] is true.
     *
     * The implementation is also expected to mark the track selection for restore: a
     * new media source drops the chosen audio and subtitle tracks.
     */
    fun replaceSource(url: String, positionMs: Long, resumePlayback: Boolean)
}

/**
 * Moves the stream that is already prepared to the server address in use now.
 *
 * The player is handed an absolute URL once and only fetches another when the title is
 * reopened, while the app moves between the LAN address and the public one on its own
 * when the network disappears. Without this, an episode dies the moment the viewer
 * walks out of Wi-Fi range and only returns by reopening it.
 *
 * The decision of *where* to move to is [retargetPreparedStreamUrl]; this holds the
 * order of the steps and what has to be true for them to run at all:
 *
 *  - offline playback is never re-pointed at a server;
 *  - a stream whose address has not changed is left alone, so the address flow can
 *    emit as often as it likes without stuttering a healthy stream;
 *  - the credential is only read when a move is actually going to happen, which is why
 *    it is a provider rather than a value.
 */
internal class PreparedStreamRetarget(
    private val stream: RetargetableStream,
    private val accessToken: suspend () -> String?,
    private val transcodeFallbackUrl: () -> String? = { null },
    private val updateTranscodeFallbackUrl: (String) -> Unit = {},
) {
    /** @return whether the stream was moved. */
    suspend fun onBaseUrlChanged(baseUrl: String): Boolean {
        if (stream.isOffline()) return false
        val preparedUrl = stream.preparedUrl()
        val fallbackUrl = transcodeFallbackUrl()
        val shouldMoveStream = preparedUrl?.let { shouldRetargetPreparedStream(it, baseUrl) } == true
        val shouldMoveFallback = fallbackUrl?.let { shouldRetargetPreparedStream(it, baseUrl) } == true
        // The decision is asked first so the credential — a storage read — is not read
        // when neither address actually changed, which is most emissions.
        if (!shouldMoveStream && !shouldMoveFallback) return false
        val token = accessToken()

        val retargetedStream = preparedUrl
            ?.takeIf { shouldMoveStream }
            ?.let { retargetPreparedStreamUrl(it, baseUrl, token) }
        val retargetedFallback = fallbackUrl
            ?.takeIf { shouldMoveFallback }
            ?.let { retargetPreparedStreamUrl(it, baseUrl, token) }

        // Keep future retries pointed at the new host even if replacing the active
        // source fails; the fallback belongs to the same server session.
        retargetedFallback?.let(updateTranscodeFallbackUrl)
        if (retargetedStream != null) {
            // The position goes with the replacement rather than a `seekTo` afterwards:
            // the player would otherwise start loading from zero and immediately jump.
            val position = stream.positionMs().coerceAtLeast(0L)
            stream.replaceSource(retargetedStream, position, stream.isPlaying())
        }

        return retargetedStream != null
    }
}
