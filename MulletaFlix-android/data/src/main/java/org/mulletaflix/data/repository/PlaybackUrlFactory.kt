package org.mulletaflix.data.repository

import okhttp3.HttpUrl.Companion.toHttpUrl

internal data class PlaybackStreamUrls(
    val directStream: String,
    val transcode: String,
)

/** Builds authenticated playback URLs without hand-concatenating query strings. */
internal fun buildPlaybackStreamUrls(
    baseUrl: String,
    itemId: String,
    mediaSourceId: String,
    accessToken: String?,
    liveStreamId: String? = null,
): PlaybackStreamUrls {
    val root = baseUrl.trimEnd('/').toHttpUrl()

    fun builder(path: String) = root.newBuilder()
        .addPathSegment("Videos")
        .addPathSegment(itemId)
        .addPathSegments(path)
        .addQueryParameter("MediaSourceId", mediaSourceId)
        .apply {
            // A live channel from a tuner has to be *opened* first (`LiveStreams/Open`),
            // and the id that call returns is what the stream route uses to find the
            // already-open feed. Without it the route answers 404 for a protocol that
            // cannot be read as a file, and "Assistir" never played.
            liveStreamId?.takeIf(String::isNotBlank)?.let { addQueryParameter("LiveStreamId", it) }
        }
        .apply {
            accessToken?.takeIf(String::isNotBlank)?.let { addQueryParameter("api_key", it) }
        }

    return PlaybackStreamUrls(
        directStream = builder("stream")
            .addQueryParameter("Static", "true")
            .build()
            .toString(),
        transcode = builder("master.m3u8")
            .build()
            .toString(),
    )
}

/**
 * Whether a media source has to be opened through `LiveStreams/Open` before playing.
 *
 * The server marks tuner sources with `RequiresOpening` and leaves `LiveStreamId` empty
 * until something opens them; the official web client checks exactly this pair
 * (`playbackmanager.ts`, "if (mediaSource.RequiresOpening && !mediaSource.LiveStreamId)").
 * A source that already carries an id was opened by another session and is reused.
 */
internal fun shouldOpenLiveStream(requiresOpening: Boolean, liveStreamId: String?): Boolean =
    requiresOpening && liveStreamId.isNullOrBlank()
