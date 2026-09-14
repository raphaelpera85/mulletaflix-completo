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
): PlaybackStreamUrls {
    val root = baseUrl.trimEnd('/').toHttpUrl()

    fun builder(path: String) = root.newBuilder()
        .addPathSegment("Videos")
        .addPathSegment(itemId)
        .addPathSegments(path)
        .addQueryParameter("MediaSourceId", mediaSourceId)
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
