package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayOutputStream
import java.io.File
import java.io.IOException
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.charset.Charset
import java.nio.charset.CodingErrorAction
import java.nio.charset.StandardCharsets

/** Reads non-DRM PalmDOC-compressed and uncompressed MOBI text; HUFF/CDIC and AZW3 are unsupported. */
internal object MobiBookTextExtractor {
    const val CONTENT_TYPE = "application/x-mobipocket-ebook"
    const val MAX_PACKAGE_BYTES = 64L * 1024L * 1024L
    private const val MAX_TEXT_BYTES = 8 * 1024 * 1024
    private const val PDB_HEADER_BYTES = 78
    private const val PDB_RECORD_ENTRY_BYTES = 8
    private const val MAX_RECORDS = 4_096
    private const val COMPRESSION_NONE = 1
    private const val COMPRESSION_PALMDOC = 2
    private const val COMPRESSION_HUFF_CDIC = 17_480

    fun supports(contentType: String?): Boolean = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.equals(CONTENT_TYPE, ignoreCase = true) == true

    fun hasMobiDatabase(file: File): Boolean = runCatching {
        if (!file.isFile || file.length() < PDB_HEADER_BYTES) return@runCatching false
        file.inputStream().buffered().use { input ->
            val header = ByteArray(68)
            var copied = 0
            while (copied < header.size) {
                val count = input.read(header, copied, header.size - copied)
                if (count < 0) break
                copied += count
            }
            copied == header.size && hasMobiDatabaseHeader(header)
        }
    }.getOrDefault(false)

    fun hasMobiDatabaseHeader(header: ByteArray): Boolean =
        header.size >= 68 && ascii(header, 60, 4) == "BOOK" && ascii(header, 64, 4) == "MOBI"

    fun extractHtml(file: File): String {
        if (!file.isFile || file.length() !in PDB_HEADER_BYTES.toLong()..MAX_PACKAGE_BYTES) {
            throw IOException("O arquivo MOBI está vazio ou excede o limite de leitura.")
        }
        return RandomAccessFile(file, "r").use { input ->
            val databaseHeader = readAt(input, 0L, PDB_HEADER_BYTES)
            if (ascii(databaseHeader, 60, 4) != "BOOK" || ascii(databaseHeader, 64, 4) != "MOBI") {
                throw IOException("O arquivo não contém um banco MOBI válido.")
            }
            val recordCount = unsignedShort(databaseHeader, 76)
            if (recordCount !in 2..MAX_RECORDS) throw IOException("O índice do arquivo MOBI é inválido.")
            val tableEnd = PDB_HEADER_BYTES.toLong() + recordCount.toLong() * PDB_RECORD_ENTRY_BYTES
            if (tableEnd > input.length()) throw IOException("O índice do arquivo MOBI está incompleto.")
            val table = readAt(input, PDB_HEADER_BYTES.toLong(), recordCount * PDB_RECORD_ENTRY_BYTES)
            val records = List(recordCount) { index ->
                unsignedInt(table, index * PDB_RECORD_ENTRY_BYTES).also { offset ->
                    if (offset < tableEnd || offset >= input.length()) {
                        throw IOException("O índice do arquivo MOBI aponta para dados inválidos.")
                    }
                }.toInt()
            }
            if (records.zipWithNext().any { (left, right) -> right <= left }) {
                throw IOException("A ordem dos registros MOBI é inválida.")
            }
            if (records[1] - records[0] > 1_048_576) {
                throw IOException("O cabeçalho do livro MOBI excede o limite de leitura.")
            }

            val firstRecord = readAt(input, records.first().toLong(), records[1] - records[0])
            if (firstRecord.size < 16 || ascii(firstRecord, 16, 4) != "MOBI") {
                throw IOException("O cabeçalho do livro MOBI está incompleto.")
            }
            val compression = unsignedShort(firstRecord, 0)
            val declaredLength = unsignedInt(firstRecord, 4)
            val textRecordCount = unsignedShort(firstRecord, 8)
            val recordSize = unsignedShort(firstRecord, 10)
            val encryption = unsignedShort(firstRecord, 12)
            val mobiHeaderLength = unsignedInt(firstRecord, 20)
            if (mobiHeaderLength < 24 || 16L + mobiHeaderLength > firstRecord.size) {
                throw IOException("O cabeçalho do livro MOBI é inválido.")
            }
            if (encryption != 0) throw IOException("Livros MOBI protegidos por DRM não são compatíveis.")
            if (compression == COMPRESSION_HUFF_CDIC) {
                throw IOException("Este MOBI usa compressão HUFF/CDIC, ainda não compatível.")
            }
            if (compression !in setOf(COMPRESSION_NONE, COMPRESSION_PALMDOC)) {
                throw IOException("O tipo de compressão deste MOBI não é compatível.")
            }
            if (declaredLength !in 1..MAX_TEXT_BYTES.toLong() ||
                textRecordCount !in 1 until recordCount || recordSize !in 1..8_192
            ) {
                throw IOException("O tamanho ou a paginação do livro MOBI é inválida.")
            }
            val extraDataFlags = if (mobiHeaderLength >= 228 && firstRecord.size >= 244) {
                unsignedShort(firstRecord, 242)
            } else {
                0
            }

            val output = BoundedByteOutputStream(declaredLength.toInt())
            repeat(textRecordCount) { index ->
                val recordIndex = index + 1
                val start = records[recordIndex]
                val end = records.getOrElse(recordIndex + 1) { input.length().toInt() }
                if (end <= start || end - start > recordSize + 4_096) {
                    throw IOException("Um bloco de texto MOBI está vazio ou excede o limite.")
                }
                val textRecord = removeTrailingEntries(
                    readAt(input, start.toLong(), end - start),
                    extraDataFlags,
                )
                if (compression == COMPRESSION_NONE) output.writeBounded(textRecord)
                else decompressPalmDoc(textRecord, output)
            }
            if (output.size() != declaredLength.toInt()) {
                throw IOException("O conteúdo MOBI não corresponde ao tamanho declarado.")
            }

            val charset = mobiCharset(firstRecord)
            val html = charset.newDecoder()
                .onMalformedInput(CodingErrorAction.REPLACE)
                .onUnmappableCharacter(CodingErrorAction.REPLACE)
                .decode(ByteBuffer.wrap(output.toByteArray()))
                .toString()
                .replace("\u0000", "\uFFFD")
            if (html.isBlank()) throw IOException("O livro MOBI não contém texto para leitura.")
            html
        }
    }

    private fun decompressPalmDoc(input: ByteArray, output: BoundedByteOutputStream) {
        var index = 0
        while (index < input.size) {
            val value = input[index++].toInt() and 0xFF
            when {
                value == 0 -> output.writeBounded(byteArrayOf(0))
                value in 1..8 -> {
                    if (value > input.size - index) throw IOException("Um bloco PalmDOC está truncado.")
                    output.writeBounded(input.copyOfRange(index, index + value))
                    index += value
                }
                value <= 0x7F -> output.writeBounded(byteArrayOf(value.toByte()))
                value >= 0xC0 -> output.writeBounded(byteArrayOf(' '.code.toByte(), (value xor 0x80).toByte()))
                else -> {
                    if (index >= input.size) throw IOException("Uma referência PalmDOC está truncada.")
                    val pair = (value shl 8) or (input[index++].toInt() and 0xFF)
                    val distance = (pair shr 3) and 0x7FF
                    val length = (pair and 0x7) + 3
                    if (distance == 0 || distance > output.size()) {
                        throw IOException("Uma referência PalmDOC aponta para fora do texto.")
                    }
                    output.copyBackReference(distance, length)
                }
            }
        }
    }

    /** Removes MOBI per-record trailers before interpreting bytes as compressed or plain text. */
    private fun removeTrailingEntries(record: ByteArray, extraDataFlags: Int): ByteArray {
        var end = record.size
        for (bit in 15 downTo 1) {
            if (extraDataFlags and (1 shl bit) == 0) continue
            val (entrySize, encodedSizeBytes) = decodeBackwardVariableInteger(record, end)
            if (entrySize < encodedSizeBytes || entrySize > end) {
                throw IOException("Os dados auxiliares do registro MOBI são inválidos.")
            }
            end -= entrySize
        }
        if (extraDataFlags and 1 != 0) {
            if (end == 0) throw IOException("O registro MOBI não contém o marcador de sobreposição.")
            val overlapEntrySize = (record[end - 1].toInt() and 0x03) + 1
            if (overlapEntrySize > end) {
                throw IOException("Os dados de sobreposição do registro MOBI são inválidos.")
            }
            end -= overlapEntrySize
        }
        return record.copyOfRange(0, end)
    }

    /** Decodes a backwards-encoded Mobipocket variable-width integer at the record end. */
    private fun decodeBackwardVariableInteger(record: ByteArray, end: Int): Pair<Int, Int> {
        var cursor = end - 1
        var value = 0L
        var byteCount = 0
        while (cursor >= 0 && byteCount < 4) {
            val current = record[cursor--].toInt() and 0xFF
            value = value or ((current and 0x7F).toLong() shl (byteCount * 7))
            byteCount++
            if (current and 0x80 != 0) {
                if (value > Int.MAX_VALUE) break
                return value.toInt() to byteCount
            }
        }
        throw IOException("O tamanho dos dados auxiliares MOBI está truncado ou inválido.")
    }

    private fun readAt(input: RandomAccessFile, offset: Long, length: Int): ByteArray {
        if (offset < 0 || length < 0 || offset > input.length() - length) {
            throw IOException("O índice do arquivo MOBI aponta para dados inválidos.")
        }
        return ByteArray(length).also { bytes ->
            input.seek(offset)
            input.readFully(bytes)
        }
    }

    private fun mobiCharset(firstRecord: ByteArray): Charset {
        val headerLength = unsignedInt(firstRecord, 20)
        if (headerLength < 16 || firstRecord.size < 48) return WINDOWS_1252
        return when (unsignedInt(firstRecord, 28)) {
            65_001L -> StandardCharsets.UTF_8
            1_252L -> WINDOWS_1252
            else -> throw IOException("A codificação deste livro MOBI não é compatível.")
        }
    }

    private fun unsignedShort(bytes: ByteArray, offset: Int): Int {
        if (offset < 0 || offset > bytes.size - 2) throw IOException("O cabeçalho MOBI está incompleto.")
        return ((bytes[offset].toInt() and 0xFF) shl 8) or (bytes[offset + 1].toInt() and 0xFF)
    }

    private fun unsignedInt(bytes: ByteArray, offset: Int): Long {
        if (offset < 0 || offset > bytes.size - 4) throw IOException("O cabeçalho MOBI está incompleto.")
        return ((bytes[offset].toLong() and 0xFF) shl 24) or
            ((bytes[offset + 1].toLong() and 0xFF) shl 16) or
            ((bytes[offset + 2].toLong() and 0xFF) shl 8) or
            (bytes[offset + 3].toLong() and 0xFF)
    }

    private fun ascii(bytes: ByteArray, offset: Int, length: Int): String =
        if (offset < 0 || offset > bytes.size - length) "" else
            bytes.copyOfRange(offset, offset + length).toString(StandardCharsets.US_ASCII)

    private class BoundedByteOutputStream(initialCapacity: Int) : ByteArrayOutputStream(initialCapacity) {
        fun writeBounded(bytes: ByteArray) {
            if (bytes.size > MAX_TEXT_BYTES - count) throw IOException("O texto MOBI excede o limite de leitura.")
            write(bytes)
        }

        fun copyBackReference(distance: Int, length: Int) {
            if (distance <= 0 || distance > count || length > MAX_TEXT_BYTES - count) {
                throw IOException("Uma referência PalmDOC aponta para fora do texto.")
            }
            val required = count + length
            if (required > buf.size) buf = buf.copyOf(maxOf(required, buf.size * 2))
            repeat(length) {
                buf[count] = buf[count - distance]
                count++
            }
        }
    }

    private const val WINDOWS_1252_CHARSET = "windows-1252"
    private val WINDOWS_1252: Charset = Charset.forName(WINDOWS_1252_CHARSET)
}
