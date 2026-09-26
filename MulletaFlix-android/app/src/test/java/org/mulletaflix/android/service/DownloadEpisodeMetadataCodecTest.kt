package org.mulletaflix.android.service

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata

class DownloadEpisodeMetadataCodecTest {
    @Test
    fun `episode metadata survives download request data round trip`() {
        val metadata = DownloadEpisodeMetadata("series-1", 2, 7)

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
}
