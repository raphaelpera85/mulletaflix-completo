package org.mulletaflix.domain.usecase

import kotlinx.coroutines.flow.Flow
import org.mulletaflix.domain.repository.DownloadEntry
import org.mulletaflix.domain.repository.DownloadRepository
import javax.inject.Inject

/**
 * UseCase encapsulating download inspection, enqueueing, retrying, and queue controls.
 */
class ManageDownloadsUseCase @Inject constructor(
    private val downloadRepository: DownloadRepository,
) {
    fun observeDownloads(): Flow<List<DownloadEntry>> =
        downloadRepository.observeDownloads()

    fun enqueue(id: String, title: String, uri: String): Result<Unit> {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        require(uri.startsWith("http://") || uri.startsWith("https://")) { "A URL da mídia é inválida." }
        return downloadRepository.enqueue(id, title, uri)
    }

    fun retry(entry: DownloadEntry): Result<Unit> {
        require(entry.id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        return downloadRepository.retry(entry.id, entry.title, entry.uri)
    }

    fun remove(id: String): Result<Unit> {
        require(id.isNotBlank()) { "O identificador da mídia é obrigatório." }
        return downloadRepository.remove(id)
    }

    fun pauseAll(): Result<Unit> = downloadRepository.pauseAll()

    fun resumeAll(): Result<Unit> = downloadRepository.resumeAll()
}
