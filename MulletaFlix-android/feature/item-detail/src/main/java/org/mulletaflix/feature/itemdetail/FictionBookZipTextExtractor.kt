package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayOutputStream
import java.io.File
import java.io.IOException
import java.util.zip.ZipEntry
import java.util.zip.ZipFile

/** Reads a single FictionBook XML member from common `.fb2.zip` / `.fbz` packages. */
internal object FictionBookZipTextExtractor {
    const val CONTENT_TYPE = "application/x-zip-compressed-fb2"
    const val MAX_PACKAGE_BYTES = 64L * 1024L * 1024L
    const val MAX_XML_BYTES = 16L * 1024L * 1024L
    private const val MAX_ARCHIVE_ENTRIES = 1_024

    fun supports(contentType: String?): Boolean = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.equals(CONTENT_TYPE, ignoreCase = true) == true

    /** Finds exactly one FB2 member; archive images remain ignored and are never extracted. */
    fun hasFictionBookPackage(file: File): Boolean = runCatching {
        if (!file.isFile || file.length() !in 1L..MAX_PACKAGE_BYTES) return false
        ZipFile(file).use { archive ->
            if (archive.size() > MAX_ARCHIVE_ENTRIES) return false
            fictionBookEntries(archive).size == 1
        }
    }.getOrDefault(false)

    fun extractXml(file: File): ByteArray {
        if (!file.isFile || file.length() <= 0L) throw IOException("O arquivo FB2 ZIP está vazio ou ausente.")
        if (file.length() > MAX_PACKAGE_BYTES) throw IOException("O arquivo FB2 ZIP excede o limite de leitura direta.")

        try {
            ZipFile(file).use { archive ->
                if (archive.size() > MAX_ARCHIVE_ENTRIES) {
                    throw IOException("O arquivo FB2 ZIP contém entradas demais.")
                }
                val entries = fictionBookEntries(archive)
                if (entries.size != 1) {
                    throw IOException("O arquivo FB2 ZIP deve conter exatamente um documento FictionBook.")
                }
                val entry = entries.single()
                if (entry.size > MAX_XML_BYTES) throw IOException("O XML do FB2 excede o limite de leitura.")

                val initialCapacity = entry.size.takeIf { it in 1L..MAX_XML_BYTES }
                    ?.toInt() ?: DEFAULT_BUFFER_SIZE
                val output = ByteArrayOutputStream(initialCapacity)
                archive.getInputStream(entry).use { input ->
                    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                    var totalBytes = 0L
                    while (true) {
                        val read = input.read(buffer)
                        if (read < 0) break
                        totalBytes += read
                        if (totalBytes > MAX_XML_BYTES) {
                            throw IOException("O XML do FB2 excede o limite de leitura.")
                        }
                        output.write(buffer, 0, read)
                    }
                }
                return output.toByteArray()
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("Não foi possível ler o pacote FB2 ZIP.", failure)
        }
    }

    private fun fictionBookEntries(archive: ZipFile): List<ZipEntry> {
        val entries = mutableListOf<ZipEntry>()
        val allEntries = archive.entries()
        while (allEntries.hasMoreElements()) {
            val entry = allEntries.nextElement()
            if (!entry.isDirectory && entry.name.substringAfterLast('/').endsWith(".fb2", ignoreCase = true)) {
                entries += entry
                if (entries.size > 1) return entries
            }
        }
        return entries
    }
}
