package org.mulletaflix.feature.itemdetail

import java.io.File
import kotlinx.coroutines.Job

/**
 * Reject only response types that cannot contain the EPUB produced by the book-reader endpoint.
 * Some servers omit Content-Type or return a generic binary type for valid EPUB streams, so the
 * EPUB parser remains the authority for validating the payload itself.
 */
internal fun isClearlyNotEpubContentType(contentType: String?): Boolean {
    val mimeType = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.lowercase()
        .orEmpty()

    if (mimeType.isEmpty()) return false

    return mimeType.startsWith("text/") ||
        mimeType.startsWith("image/") ||
        mimeType.startsWith("audio/") ||
        mimeType.startsWith("video/") ||
        mimeType in setOf("application/pdf", "application/json", "application/xml")
}

internal fun bookReaderHttpFailureMessage(statusCode: Int): String =
    if (statusCode == 415) {
        "Este formato não pode ser lido pelo aplicativo."
    } else {
        "Não foi possível carregar o livro (HTTP $statusCode)."
    }

/** Owns only files created by one reader instance, never files from another open book. */
internal class BookReaderCacheFiles(private val directory: File) {
    private val ownedFiles = linkedSetOf<File>()

    @Synchronized
    fun create(): File {
        if (!directory.isDirectory && !directory.mkdirs()) {
            throw IllegalStateException("Não foi possível preparar o cache do leitor.")
        }
        return File.createTempFile("book-reader-", ".epub", directory).also(ownedFiles::add)
    }

    @Synchronized
    fun delete(file: File) {
        if (file !in ownedFiles) return
        if (!file.exists() || file.delete()) ownedFiles.remove(file)
    }

    @Synchronized
    fun deleteAllOwned() {
        ownedFiles.toList().forEach(::delete)
    }

    fun deleteAfter(job: Job?) {
        if (job == null || job.isCompleted) {
            deleteAllOwned()
        } else {
            job.invokeOnCompletion { deleteAllOwned() }
        }
    }
}
