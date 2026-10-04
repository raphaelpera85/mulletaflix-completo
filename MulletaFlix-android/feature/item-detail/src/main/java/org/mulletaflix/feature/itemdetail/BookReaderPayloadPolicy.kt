package org.mulletaflix.feature.itemdetail

import java.io.IOException
import java.io.InputStream
import java.io.File
import java.io.OutputStream
import kotlinx.coroutines.Job

internal const val MAX_BOOK_PAYLOAD_BYTES = 512L * 1024L * 1024L

internal fun copyBookReaderPayload(
    input: InputStream,
    output: OutputStream,
    maxBytes: Long = MAX_BOOK_PAYLOAD_BYTES,
): Long {
    require(maxBytes > 0L)
    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
    var copiedBytes = 0L
    while (true) {
        val readBytes = input.read(buffer)
        if (readBytes < 0) return copiedBytes
        if (readBytes.toLong() > maxBytes - copiedBytes) {
            throw IOException("Book file exceeds the supported size limit.")
        }
        output.write(buffer, 0, readBytes)
        copiedBytes += readBytes
    }
}

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
        mimeType in setOf("application/pdf", "application/json", "application/xml") ||
        ComicBookArchive.supports(mimeType)
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
    fun create(comicArchive: Boolean = false): File {
        if (!directory.isDirectory && !directory.mkdirs()) {
            throw IllegalStateException("Não foi possível preparar o cache do leitor.")
        }
        val extension = if (comicArchive) ".cbz" else ".epub"
        return File.createTempFile("book-reader-", extension, directory).also(ownedFiles::add)
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

    @Synchronized
    fun deleteAllOwnedExcept(file: File) {
        ownedFiles.filter { it != file }.forEach(::delete)
    }

    fun deleteAfter(job: Job?) {
        if (job == null || job.isCompleted) {
            deleteAllOwned()
        } else {
            job.invokeOnCompletion { deleteAllOwned() }
        }
    }
}
