package org.mulletaflix.android.service

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadMediaMetadata
import org.mulletaflix.domain.repository.DownloadSubtitleMetadata
import java.io.ByteArrayOutputStream
import java.io.DataOutputStream

class DownloadEpisodeMetadataCodecTest {
    @Test
    fun `episode metadata survives download request data round trip`() {
        val metadata = DownloadEpisodeMetadata("series-1", 2, 7, "Minha Série")

        assertEquals(metadata, decodeDownloadEpisodeMetadata(encodeDownloadEpisodeMetadata(metadata)))
    }

    @Test
    fun `non episode downloads keep empty request data`() {
        assertArrayEquals(byteArrayOf(), encodeDownloadEpisodeMetadata(null))
        assertNull(decodeDownloadEpisodeMetadata(byteArrayOf()))
    }

    @Test
    fun `unsupported or corrupt request data is ignored`() {
        assertNull(decodeDownloadEpisodeMetadata(byteArrayOf(1, 2, 3)))
        assertArrayEquals(
            byteArrayOf(),
            encodeDownloadEpisodeMetadata(DownloadEpisodeMetadata("", 1, 1)),
        )
    }

    @Test
    fun `new request metadata round trips episode and subtitle tracks`() {
        val expectedEpisode = DownloadEpisodeMetadata("series-1", 2, 7, "Minha Série")
        val expectedMedia = DownloadMediaMetadata(
            serverId = "server-1",
            mediaSourceId = "source-1",
            subtitles = listOf(
                DownloadSubtitleMetadata(4, "application/x-subrip", "pt-BR", "Português", true, false),
                DownloadSubtitleMetadata(8, "text/vtt", "en", "English", false, true),
            ),
        )

        val decoded = decodeDownloadRequestMetadata(encodeDownloadRequestMetadata(expectedEpisode, expectedMedia))

        assertEquals(expectedEpisode, decoded?.episode)
        assertEquals(expectedMedia, decoded?.media)
    }

    @Test
    fun `legacy episode bytes remain readable and new metadata stores no delivery url or token`() {
        val legacyEpisode = legacyEpisodeBytes()
        val legacyRequest = legacyRequestBytes()
        val media = DownloadMediaMetadata(
            "server-2", null,
            listOf(DownloadSubtitleMetadata(0, "text/vtt", label = "https://cdn.example/caption.vtt?token=secret")),
        )
        val encoded = encodeDownloadRequestMetadata(null, media)
        val storedText = encoded.toString(Charsets.ISO_8859_1)

        assertEquals(DownloadEpisodeMetadata("series-2", 1, 3), decodeDownloadRequestMetadata(legacyEpisode)?.episode)
        assertEquals(
            DownloadEpisodeMetadata("series-3", 2, 4),
            decodeDownloadRequestMetadata(legacyRequest)?.episode,
        )
        assertEquals(null, decodeDownloadRequestMetadata(encoded)?.media?.subtitles?.single()?.label)
        assertFalse(storedText.contains("https://cdn.example"))
        assertFalse(storedText.contains("secret"))
    }

    @Test
    fun `truncated request metadata is rejected`() {
        val encoded = encodeDownloadRequestMetadata(
            null,
            DownloadMediaMetadata("server-1", "source-1", listOf(DownloadSubtitleMetadata(1, "text/vtt"))),
        )

        assertNull(decodeDownloadRequestMetadata(encoded.copyOf(encoded.size - 1)))
    }

    @Test
    fun `subtitle fetch path encodes item and source IDs and rejects invalid indices`() {
        assertEquals(
            "Items/item%2Fwith%20space/Subtitles/12/Stream?MediaSourceId=source%2F1",
            offlineSubtitleStreamPath("item/with space", 12, "source/1"),
        )
        assertEquals(null, offlineSubtitleStreamPath("", 0, "source"))
        assertEquals(null, offlineSubtitleStreamPath("item", 100_001, "source"))
    }

    private fun legacyEpisodeBytes() = ByteArrayOutputStream().use { bytes ->
        DataOutputStream(bytes).use { output ->
            output.writeInt(0x4D464550) // MFEP
            output.writeInt(1)
            output.writeUTF("series-2")
            output.writeInt(1)
            output.writeInt(3)
        }
        bytes.toByteArray()
    }

    private fun legacyRequestBytes() = ByteArrayOutputStream().use { bytes ->
        DataOutputStream(bytes).use { output ->
            output.writeInt(0x4D46444D) // MFDM
            output.writeInt(1)
            output.writeBoolean(true)
            output.writeUTF("series-3")
            output.writeInt(2)
            output.writeInt(4)
            output.writeUTF("server-1")
            output.writeBoolean(false)
            output.writeInt(1)
            output.writeInt(5)
            output.writeUTF("text/vtt")
            output.writeBoolean(false)
            output.writeBoolean(false)
            output.writeBoolean(false)
            output.writeBoolean(false)
        }
        bytes.toByteArray()
    }
}
