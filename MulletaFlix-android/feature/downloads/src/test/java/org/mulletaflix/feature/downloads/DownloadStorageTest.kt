package org.mulletaflix.feature.downloads

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadEpisodeMetadata
import org.mulletaflix.domain.repository.DownloadState

class DownloadStorageTest {

    private fun entry(
        id: String,
        title: String,
        bytes: Long,
        state: DownloadState = DownloadState.Completed,
        contentLength: Long = 0L,
    ) = DownloadEntry(
        id = id,
        title = title,
        uri = "file:///$id",
        state = state,
        percent = 100,
        bytesDownloaded = bytes,
        contentLength = contentLength,
    )

    private val downloads = listOf(
        entry("big", "Maior", 3_000_000),
        entry("tie-b", "B empate", 2_000_000),
        entry("tie-a", "A empate", 2_000_000),
        entry("small", "Menor", 1_000_000),
        entry("zero-known", "Vazio medido", 0, DownloadState.Downloading, 500),
        entry("unknown-z", "Sem tamanho Z", 0, DownloadState.Queued),
        entry("unknown-a", "Sem tamanho A", -5, DownloadState.Failed),
    )

    @Test
    fun `largest first keeps unknown and invalid sizes last with stable title ties`() {
        assertEquals(
            listOf("big", "tie-a", "tie-b", "small", "zero-known", "unknown-a", "unknown-z"),
            sortDownloadsByStorage(downloads, DownloadStorageOrder.LargestFirst).map { it.id },
        )
    }

    @Test
    fun `smallest first keeps unknown and invalid sizes last with stable title ties`() {
        assertEquals(
            listOf("zero-known", "small", "tie-a", "tie-b", "big", "unknown-a", "unknown-z"),
            sortDownloadsByStorage(downloads, DownloadStorageOrder.SmallestFirst).map { it.id },
        )
    }

    @Test
    fun `per title storage label clamps invalid values and includes total when known`() {
        val download = entry("movie", "Filme", -5).copy(contentLength = 2_097_152)

        assertEquals("No dispositivo: 0 B de 2,0 MB", downloadStorageLabel(download))
    }

    @Test
    fun `unknown queued size is labeled as unavailable instead of zero`() {
        assertEquals(
            "Tamanho ainda não informado",
            downloadStorageLabel(entry("queued", "Na fila", 0, DownloadState.Queued)),
        )
    }

    @Test
    fun `episode downloads expose season and episode label`() {
        val episode = entry("episode", "Minha Série", 0).copy(
            episodeMetadata = DownloadEpisodeMetadata("series", seasonNumber = 2, episodeNumber = 7, seriesName = "Minha Série"),
        )

        assertEquals("Minha Série · Temporada 2 · Episódio 7", downloadEpisodeLabel(episode))
    }

    @Test
    fun `season zero is labeled as a special and invalid metadata is hidden`() {
        val special = entry("special", "Minha Série", 0).copy(
            episodeMetadata = DownloadEpisodeMetadata("series", seasonNumber = 0, episodeNumber = 2, seriesName = "Minha Série"),
        )
        val invalid = special.copy(episodeMetadata = special.episodeMetadata?.copy(episodeNumber = 0))

        assertEquals("Minha Série · Especial · Episódio 2", downloadEpisodeLabel(special))
        assertEquals(null, downloadEpisodeLabel(invalid))
        assertEquals(null, downloadEpisodeLabel(entry("movie", "Filme", 0)))
    }

    @Test
    fun `episode label remains useful when the series name is unavailable`() {
        val episode = entry("episode", "Episódio Piloto", 0).copy(
            episodeMetadata = DownloadEpisodeMetadata("series", seasonNumber = 1, episodeNumber = 1),
        )

        assertEquals("Temporada 1 · Episódio 1", downloadEpisodeLabel(episode))
    }
}
