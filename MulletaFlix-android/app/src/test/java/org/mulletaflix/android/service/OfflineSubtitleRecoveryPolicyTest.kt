package org.mulletaflix.android.service

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import java.io.IOException
import java.net.SocketException
import java.net.SocketTimeoutException
import javax.net.ssl.SSLHandshakeException

class OfflineSubtitleRecoveryPolicyTest {
    private val portuguese = DownloadSubtitleMetadata(2, "application/x-subrip", "pt-BR", "Português")
    private val english = DownloadSubtitleMetadata(3, "text/vtt", "en", "English")

    @Test
    fun `selects only missing subtitles for the active account and server`() {
        val matching = candidate("queue-a", "user-a", "server-a", listOf(portuguese, english))
        val anotherAccount = candidate("queue-b", "user-b", "server-a", listOf(portuguese))
        val anotherServer = candidate("queue-c", "user-a", "server-b", listOf(portuguese))
        val completedSidecars = candidate("queue-d", "user-a", "server-a", listOf(portuguese))
        val candidates = listOf(matching, anotherAccount, anotherServer, completedSidecars)

        val pending = selectOfflineSubtitleRecoveries(candidates, "user-a", "server-a") { item, subtitle ->
            item.requestId == "queue-d" || subtitle.streamIndex == english.streamIndex
        }

        assertEquals(listOf(matching.copy(media = matching.media.copy(subtitles = listOf(portuguese)))), pending)
    }

    @Test
    fun `skips legacy entries without owner and downloads without external subtitles`() {
        val legacy = candidate("legacy", null, "server-a", listOf(portuguese))
        val noSubtitles = candidate("no-subtitles", "user-a", "server-a", emptyList())

        val pending = selectOfflineSubtitleRecoveries(
            listOf(legacy, noSubtitles),
            activeUserId = "user-a",
            activeServerScope = "server-a",
            hasStoredSubtitle = { _, _ -> false },
        )

        assertTrue(pending.isEmpty())
    }

    @Test
    fun `blank active scope cannot recover any sidecar`() {
        val item = candidate("queue-a", "user-a", "server-a", listOf(portuguese))

        assertTrue(
            selectOfflineSubtitleRecoveries(listOf(item), "user-a", " ") { _, _ -> false }.isEmpty(),
        )
    }

    @Test
    fun `retries only transient HTTP failures`() {
        assertTrue(shouldRetryOfflineSubtitleHttpStatus(408))
        assertTrue(shouldRetryOfflineSubtitleHttpStatus(429))
        assertTrue(shouldRetryOfflineSubtitleHttpStatus(503))
        assertEquals(false, shouldRetryOfflineSubtitleHttpStatus(401))
        assertEquals(false, shouldRetryOfflineSubtitleHttpStatus(404))
        assertEquals(false, shouldRetryOfflineSubtitleHttpStatus(200))
    }

    @Test
    fun `uses bounded exponential delay between attempts`() {
        assertEquals(1_000L, offlineSubtitleRetryDelayMillis(1))
        assertEquals(2_000L, offlineSubtitleRetryDelayMillis(2))
        assertEquals(3, MAX_OFFLINE_SUBTITLE_ATTEMPTS)
    }

    @Test
    fun `retries transient network failures but not TLS or protocol errors`() {
        assertTrue(isTransientOfflineSubtitleNetworkFailure(SocketTimeoutException("timeout")))
        assertTrue(isTransientOfflineSubtitleNetworkFailure(SocketException("connection reset")))
        assertEquals(false, isTransientOfflineSubtitleNetworkFailure(IOException("generic I/O failure")))
        assertEquals(false, isTransientOfflineSubtitleNetworkFailure(SSLHandshakeException("certificate")))
        assertEquals(false, isTransientOfflineSubtitleNetworkFailure(java.net.ProtocolException("protocol")))
    }

    private fun candidate(
        requestId: String,
        ownerUserId: String?,
        serverId: String,
        subtitles: List<DownloadSubtitleMetadata>,
    ) = OfflineSubtitleRecoveryCandidate(
        requestId = requestId,
        itemId = "media-$requestId",
        ownerUserId = ownerUserId,
        media = DownloadMediaMetadata(serverId, "source-$requestId", subtitles),
    )
}
