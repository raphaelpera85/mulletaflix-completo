package org.mulletaflix.feature.itemdetail

import java.io.IOException
import java.io.InputStream
import java.io.File
import java.io.ByteArrayOutputStream
import java.io.OutputStream
import java.io.RandomAccessFile
import java.util.zip.ZipFile
import kotlinx.coroutines.Job

internal const val MAX_BOOK_PAYLOAD_BYTES = 512L * 1024L * 1024L

internal fun bookReaderPayloadLimit(contentType: String?): Long = when {
    PlainTextBookDocument.isHtml(contentType) -> PlainTextBookDocument.MAX_HTML_BYTES
    OdtBookTextExtractor.supports(contentType) -> OdtBookTextExtractor.MAX_PACKAGE_BYTES
    DocxBookTextExtractor.supports(contentType) -> DocxBookTextExtractor.MAX_PACKAGE_BYTES
    FictionBookZipTextExtractor.supports(contentType) -> FictionBookZipTextExtractor.MAX_PACKAGE_BYTES
    PlainTextBookDocument.supports(contentType) -> PlainTextBookDocument.MAX_TEXT_BYTES
    else -> MAX_BOOK_PAYLOAD_BYTES
}

internal fun copyBookReaderPayload(
    input: InputStream,
    output: OutputStream,
    maxBytes: Long = MAX_BOOK_PAYLOAD_BYTES,
    contentType: String? = null,
): Long {
    require(maxBytes > 0L)
    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
    val signaturePrefix = ByteArrayOutputStream(OdtBookTextExtractor.ZIP_MIMETYPE_PREFIX_BYTES)
    val retainGeneralLimit = hasLargeBookContentType(contentType)
    var effectiveMaxBytes = if (retainGeneralLimit) maxBytes else minOf(
        maxBytes,
        PlainTextBookDocument.MAX_TEXT_BYTES,
    )
    var signatureResolved = retainGeneralLimit || maxBytes <= PlainTextBookDocument.MAX_TEXT_BYTES
    var copiedBytes = 0L
    while (true) {
        val readBytes = input.read(buffer)
        if (readBytes < 0) return copiedBytes
        if (!signatureResolved) {
            val prefixBytes = minOf(readBytes, OdtBookTextExtractor.ZIP_MIMETYPE_PREFIX_BYTES - signaturePrefix.size())
            signaturePrefix.write(buffer, 0, prefixBytes)
            if (signaturePrefix.size() == OdtBookTextExtractor.ZIP_MIMETYPE_PREFIX_BYTES) {
                val prefix = signaturePrefix.toByteArray()
                effectiveMaxBytes = when {
                    OdtBookTextExtractor.hasOpenDocumentTextZipPrefix(prefix) ->
                        minOf(maxBytes, OdtBookTextExtractor.MAX_PACKAGE_BYTES)
                    hasLargeBookPayloadSignature(prefix) -> maxBytes
                    else -> effectiveMaxBytes
                }
                signatureResolved = true
            }
        }
        if (readBytes.toLong() > effectiveMaxBytes - copiedBytes) {
            throw IOException("Book file exceeds the supported size limit.")
        }
        output.write(buffer, 0, readBytes)
        copiedBytes += readBytes
    }
}

private fun hasLargeBookContentType(contentType: String?): Boolean {
    val mimeType = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.lowercase()
        .orEmpty()
    return PdfBookDocument.supports(mimeType) ||
        ComicBookArchive.supports(mimeType) ||
        CbrBookArchive.supports(mimeType) ||
        OdtBookTextExtractor.supports(mimeType) ||
        FictionBookZipTextExtractor.supports(mimeType) ||
        mimeType == "application/epub+zip"
}

private fun hasLargeBookPayloadSignature(prefix: ByteArray): Boolean {
    if (prefix.size >= PDF_SIGNATURE.size && prefix.copyOfRange(0, PDF_SIGNATURE.size).contentEquals(PDF_SIGNATURE)) {
        return true
    }
    if (CbrBookArchive.hasRarSignaturePrefix(prefix)) return true
    if (prefix.size < 4 || prefix[0] != 'P'.code.toByte() || prefix[1] != 'K'.code.toByte()) return false
    return (prefix[2] == 0x03.toByte() && prefix[3] == 0x04.toByte()) ||
        (prefix[2] == 0x05.toByte() && prefix[3] == 0x06.toByte()) ||
        (prefix[2] == 0x07.toByte() && prefix[3] == 0x08.toByte())
}

/**
 * Reject non-book responses while allowing direct PDF/CBZ and server-converted EPUB payloads.
 * Some servers omit Content-Type or return a generic binary type, so the selected reader remains
 * the authority for validating the payload itself.
 */
internal fun isClearlyNotSupportedBookContentType(contentType: String?): Boolean {
    val mimeType = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.lowercase()
        .orEmpty()

    if (mimeType.isEmpty()) return false

    return (mimeType.startsWith("text/") && !PlainTextBookDocument.supports(mimeType)) ||
        mimeType.startsWith("image/") ||
        mimeType.startsWith("audio/") ||
        mimeType.startsWith("video/") ||
        mimeType in setOf(
            "application/json",
            "application/x-mobipocket-ebook",
            "application/x-cb7",
            "application/x-cbt",
        )
}

internal fun bookReaderHttpFailureMessage(statusCode: Int): String =
    if (statusCode == 415) {
        "Este formato não pode ser lido pelo aplicativo."
    } else {
        "Não foi possível carregar o livro (HTTP $statusCode)."
    }

/** Owns per-instance files and recovers abandoned reader payloads without deleting active files. */
internal class BookReaderCacheFiles(
    private val directory: File,
    private val deleteFile: (File) -> Boolean = File::delete,
) {
    private val directoryKey = directory.canonicalPath
    private val ownedFiles = linkedSetOf<File>()

    init {
        synchronized(registryLock) {
            activeFilesByDirectory.getOrPut(directoryKey) { linkedSetOf() }
            deleteOrphanedFiles()
        }
    }

    fun create(
        comicArchive: Boolean = false,
        pdfDocument: Boolean = false,
        plainText: Boolean = false,
    ): File {
        synchronized(registryLock) {
            require(listOf(comicArchive, pdfDocument, plainText).count { it } <= 1)
            if (!directory.isDirectory && !directory.mkdirs()) {
                throw IllegalStateException("Não foi possível preparar o cache do leitor.")
            }
            val extension = when {
                comicArchive -> ".cbz"
                pdfDocument -> ".pdf"
                plainText -> ".txt"
                else -> ".epub"
            }
            return File.createTempFile(FILE_PREFIX, extension, directory).also { file ->
                ownedFiles.add(file)
                activeFilesByDirectory.getValue(directoryKey).add(file)
            }
        }
    }

    fun delete(file: File) {
        synchronized(registryLock) {
            if (file !in ownedFiles) return
            try {
                if (file.exists()) attemptDelete(file)
            } catch (_: SecurityException) {
                // Retain the file as an orphan so a later initialization can retry cleanup.
            } finally {
                // Relinquish ownership even when deletion fails; a later reader initialization retries it.
                ownedFiles.remove(file)
                activeFilesByDirectory[directoryKey]?.remove(file)
            }
        }
    }

    fun withExtension(file: File, extension: String): File = synchronized(registryLock) {
        require(file in ownedFiles)
        require(extension in setOf(".epub", ".cbz", ".cbr", ".pdf", ".txt"))
        val renamed = File(file.parentFile, file.nameWithoutExtension + extension)
        if (renamed == file) return@synchronized file
        if (!file.renameTo(renamed)) throw IOException("Não foi possível identificar o formato do livro.")
        ownedFiles.remove(file)
        ownedFiles.add(renamed)
        activeFilesByDirectory.getValue(directoryKey).remove(file)
        activeFilesByDirectory.getValue(directoryKey).add(renamed)
        renamed
    }

    fun deleteAllOwned() {
        synchronized(registryLock) {
            ownedFiles.toList().forEach(::delete)
        }
    }

    fun deleteAllOwnedExcept(file: File) {
        synchronized(registryLock) {
            ownedFiles.filter { it != file }.forEach(::delete)
        }
    }

    fun deleteAfter(job: Job?) {
        if (job == null || job.isCompleted) {
            deleteAllOwned()
        } else {
            job.invokeOnCompletion { deleteAllOwned() }
        }
    }

    private fun deleteOrphanedFiles() {
        val activeFiles = activeFilesByDirectory.getValue(directoryKey)
        val files = try {
            directory.listFiles()
        } catch (_: SecurityException) {
            return
        }
        files?.forEach { file ->
            try {
                if (file.isFile && file.isOwnedBookCacheFile() && file !in activeFiles) {
                    attemptDelete(file)
                }
            } catch (_: SecurityException) {
                // Ignore inaccessible entries; retry on the next reader initialization.
            }
        }
    }

    private fun attemptDelete(file: File) {
        try {
            deleteFile(file)
        } catch (_: SecurityException) {
            // Cleanup is best-effort; ownership is released and a later initialization retries.
        }
    }

    private fun File.isOwnedBookCacheFile(): Boolean =
        name.startsWith(FILE_PREFIX) &&
            (name.endsWith(".epub") || name.endsWith(".cbz") || name.endsWith(".cbr") ||
                name.endsWith(".pdf") || name.endsWith(".txt"))

    private companion object {
        const val FILE_PREFIX = "book-reader-"
        val registryLock = Any()
        val activeFilesByDirectory = mutableMapOf<String, MutableSet<File>>()
    }
}

internal enum class BookPayloadFormat { EPUB, PDF, CBZ, CBR, PLAIN_TEXT }

/** MIME is authoritative when specific; inspect signatures/ZIP structure for generic responses. */
internal fun detectBookPayloadFormat(file: File, contentType: String?): BookPayloadFormat {
    if (OdtBookTextExtractor.supports(contentType) || OdtBookTextExtractor.hasOpenDocumentTextPackage(file)) {
        return BookPayloadFormat.PLAIN_TEXT
    }
    if (DocxBookTextExtractor.supports(contentType) || DocxBookTextExtractor.isDocxPackage(file)) {
        return BookPayloadFormat.PLAIN_TEXT
    }
    if (FictionBookZipTextExtractor.supports(contentType) ||
        FictionBookZipTextExtractor.hasFictionBookPackage(file)
    ) return BookPayloadFormat.PLAIN_TEXT
    if (PlainTextBookDocument.isFictionBookContentType(contentType) ||
        PlainTextBookDocument.hasFictionBookRoot(file)
    ) return BookPayloadFormat.PLAIN_TEXT
    if (PlainTextBookDocument.isRtfContentType(contentType) || PlainTextBookDocument.hasRtfHeader(file)) {
        return BookPayloadFormat.PLAIN_TEXT
    }
    if (PlainTextBookDocument.supports(contentType)) return BookPayloadFormat.PLAIN_TEXT
    if (PdfBookDocument.supports(contentType)) return BookPayloadFormat.PDF
    if (ComicBookArchive.supports(contentType)) return BookPayloadFormat.CBZ
    if (CbrBookArchive.supports(contentType) || CbrBookArchive.hasRarSignature(file)) return BookPayloadFormat.CBR
    if (file.isFile && file.length() >= PDF_SIGNATURE.size) {
        val signature = ByteArray(PDF_SIGNATURE.size)
        RandomAccessFile(file, "r").use { it.readFully(signature) }
        if (signature.contentEquals(PDF_SIGNATURE)) return BookPayloadFormat.PDF
    }
    if (file.isFile && file.length() >= 4L) {
        val isComicArchive = runCatching {
            ZipFile(file).use { archive ->
                archive.getEntry("META-INF/container.xml") == null &&
                    archive.entries().asSequence().any { entry ->
                        !entry.isDirectory && entry.name.substringAfterLast('.', "").lowercase() in
                            setOf("jpg", "jpeg", "png", "webp", "gif", "bmp")
                    }
            }
        }.getOrDefault(false)
        if (isComicArchive) return BookPayloadFormat.CBZ
    }
    return BookPayloadFormat.EPUB
}

private val PDF_SIGNATURE = "%PDF-".toByteArray(Charsets.US_ASCII)
