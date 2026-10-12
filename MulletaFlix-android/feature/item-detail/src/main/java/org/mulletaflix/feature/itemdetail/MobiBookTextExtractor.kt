package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayOutputStream
import java.io.File
import java.io.IOException
import java.io.RandomAccessFile
import java.nio.ByteBuffer
import java.nio.charset.Charset
import java.nio.charset.CodingErrorAction
import java.nio.charset.StandardCharsets

/** Reads non-DRM MOBI text and reconstructs AZW3/KF8 skeletons and fragments. */
internal object MobiBookTextExtractor {
    const val CONTENT_TYPE = "application/x-mobipocket-ebook"
    const val KF8_CONTENT_TYPE = "application/vnd.amazon.mobi8-ebook"
    const val MAX_PACKAGE_BYTES = 64L * 1024L * 1024L
    private const val MAX_TEXT_BYTES = 16 * 1024 * 1024
    private const val PDB_HEADER_BYTES = 78
    private const val PDB_RECORD_ENTRY_BYTES = 8
    private const val MAX_RECORDS = 4_096
    private const val MAX_HUFF_DICTIONARY_BYTES = 8 * 1024 * 1024
    private const val COMPRESSION_NONE = 1
    private const val COMPRESSION_PALMDOC = 2
    private const val COMPRESSION_HUFF_CDIC = 17_480

    fun supports(contentType: String?): Boolean = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.equals(CONTENT_TYPE, ignoreCase = true) == true

    fun supportsKf8(contentType: String?): Boolean = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.equals(KF8_CONTENT_TYPE, ignoreCase = true) == true

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

    fun hasKf8Database(file: File): Boolean = runCatching {
        if (!file.isFile || file.length() !in PDB_HEADER_BYTES.toLong()..MAX_PACKAGE_BYTES) return@runCatching false
        RandomAccessFile(file, "r").use { input ->
            val databaseHeader = readAt(input, 0L, PDB_HEADER_BYTES)
            if (ascii(databaseHeader, 60, 4) != "BOOK" || ascii(databaseHeader, 64, 4) != "MOBI") {
                return@use false
            }
            val recordCount = unsignedShort(databaseHeader, 76)
            if (recordCount !in 2..MAX_RECORDS) return@use false
            val tableEnd = PDB_HEADER_BYTES.toLong() + recordCount.toLong() * PDB_RECORD_ENTRY_BYTES
            if (tableEnd > input.length()) return@use false
            val table = readAt(input, PDB_HEADER_BYTES.toLong(), recordCount * PDB_RECORD_ENTRY_BYTES)
            val records = List(recordCount) { index ->
                unsignedInt(table, index * PDB_RECORD_ENTRY_BYTES).also { offset ->
                    if (offset < tableEnd || offset >= input.length()) return@use false
                }.toInt()
            }
            if (records.zipWithNext().any { (left, right) -> right <= left }) return@use false
            if (records[1] - records[0] > 1_048_576) return@use false
            val firstRecord = readAt(input, records[0].toLong(), records[1] - records[0])
            if (ascii(firstRecord, 16, 4) != "MOBI") return@use false
            val headerLength = unsignedInt(firstRecord, 20)
            if (headerLength < 24 || 16L + headerLength > firstRecord.size.toLong()) return@use false

            val mobiVersion = unsignedInt(firstRecord, 104)
            if (mobiVersion == 8L && firstRecord.size >= 264 && headerLength >= 248) {
                val skeletonIndex = unsignedInt(firstRecord, 252)
                if (skeletonIndex in 1 until recordCount.toLong()) {
                    val indexStart = records[skeletonIndex.toInt()]
                    val indexEnd = records.getOrElse(skeletonIndex.toInt() + 1) { input.length().toInt() }
                    if (indexEnd - indexStart >= 200) {
                        val skeletonIndexRecord = readAt(input, indexStart.toLong(), minOf(indexEnd - indexStart, 1_048_576))
                        val indexHeaderLength = unsignedInt(skeletonIndexRecord, 4)
                        val tagxOffset = unsignedInt(skeletonIndexRecord, 180)
                        if (ascii(skeletonIndexRecord, 0, 4) == "INDX" &&
                            indexHeaderLength >= 184 && tagxOffset >= indexHeaderLength &&
                            tagxOffset <= skeletonIndexRecord.size - 16L
                        ) {
                            val tagxStart = tagxOffset.toInt()
                            val tagxLength = unsignedInt(skeletonIndexRecord, tagxStart + 4)
                            val controlByteCount = unsignedInt(skeletonIndexRecord, tagxStart + 8)
                            val validTagxLength = tagxLength in 16..(skeletonIndexRecord.size - tagxStart).toLong() &&
                                tagxLength % 4L == 0L
                            val endOfTagTable = if (validTagxLength) tagxStart + tagxLength.toInt() else tagxStart
                            val hasEndMarker = validTagxLength &&
                                skeletonIndexRecord[endOfTagTable - 4] == 0.toByte() &&
                                skeletonIndexRecord[endOfTagTable - 3] == 0.toByte() &&
                                skeletonIndexRecord[endOfTagTable - 2] == 0.toByte() &&
                                skeletonIndexRecord[endOfTagTable - 1] == 1.toByte()
                            if (ascii(skeletonIndexRecord, tagxStart, 4) == "TAGX" &&
                                validTagxLength &&
                                controlByteCount in 1..255 && hasEndMarker
                            ) return@use true
                        }
                    }
                }
            }

            findHybridKf8HeaderIndex(input, records, firstRecord) != null
        }
    }.getOrDefault(false)

    fun hasMobiDatabaseHeader(header: ByteArray): Boolean =
        header.size >= 68 && ascii(header, 60, 4) == "BOOK" && ascii(header, 64, 4) == "MOBI"

    private fun findHybridKf8HeaderIndex(
        input: RandomAccessFile,
        records: List<Int>,
        mobi7Header: ByteArray,
    ): Int? {
        if (unsignedInt(mobi7Header, 104) == 8L || mobi7Header.size < 132 ||
            unsignedInt(mobi7Header, 128) and 0x40L == 0L
        ) return null
        val headerLength = unsignedInt(mobi7Header, 20)
        val exthOffset = 16L + headerLength
        if (headerLength < 24 || exthOffset > mobi7Header.size - 12L) return null
        val exthStart = exthOffset.toInt()
        if (ascii(mobi7Header, exthStart, 4) != "EXTH") return null
        val exthLength = unsignedInt(mobi7Header, exthStart + 4)
        val exthCount = unsignedInt(mobi7Header, exthStart + 8)
        if (exthLength !in 12..(mobi7Header.size - exthStart).toLong() ||
            exthCount > (exthLength - 12) / 8
        ) return null
        val exthEnd = exthStart + exthLength.toInt()
        var entryOffset = exthStart + 12
        repeat(exthCount.toInt()) {
            if (entryOffset > exthEnd - 8) return null
            val tag = unsignedInt(mobi7Header, entryOffset)
            val entryLength = unsignedInt(mobi7Header, entryOffset + 4)
            if (entryLength < 8 || entryLength > exthEnd - entryOffset.toLong()) return null
            if (tag == 121L && entryLength >= 12) {
                val kf8HeaderIndex = unsignedInt(mobi7Header, entryOffset + 8)
                if (kf8HeaderIndex !in 2 until records.size.toLong()) return null
                val headerIndex = kf8HeaderIndex.toInt()
                val boundaryStart = records[headerIndex - 1]
                val boundaryEnd = records[headerIndex]
                if (boundaryEnd - boundaryStart != 8 ||
                    ascii(readAt(input, boundaryStart.toLong(), 8), 0, 8) != "BOUNDARY"
                ) return null
                val headerStart = records[headerIndex]
                val headerEnd = records.getOrElse(headerIndex + 1) { input.length().toInt() }
                if (headerEnd - headerStart < 120) return null
                val kf8Header = readAt(input, headerStart.toLong(), minOf(headerEnd - headerStart, 1_048_576))
                val kf8Length = unsignedInt(kf8Header, 20)
                val flowIndex = unsignedInt(kf8Header, 192)
                val divisionIndex = unsignedInt(kf8Header, 248)
                val skeletonIndex = unsignedInt(kf8Header, 252)
                fun isIndexRecord(index: Long): Boolean {
                    if (index !in (headerIndex + 1).toLong() until records.size.toLong()) return false
                    val start = records[index.toInt()]
                    val end = records.getOrElse(index.toInt() + 1) { input.length().toInt() }
                    if (end - start < 200) return false
                    val record = readAt(input, start.toLong(), minOf(end - start, 1_048_576))
                    val indexLength = unsignedInt(record, 4)
                    val tagxOffset = unsignedInt(record, 180)
                    if (ascii(record, 0, 4) != "INDX" || indexLength < 184 ||
                        tagxOffset < indexLength || tagxOffset > record.size - 16L
                    ) return false
                    val tagxStart = tagxOffset.toInt()
                    val tagxLength = unsignedInt(record, tagxStart + 4)
                    val controls = unsignedInt(record, tagxStart + 8)
                    if (ascii(record, tagxStart, 4) != "TAGX" || tagxLength !in 16..(record.size - tagxStart).toLong() ||
                        tagxLength % 4L != 0L || controls !in 1..255
                    ) return false
                    val marker = tagxStart + tagxLength.toInt() - 4
                    return record[marker] == 0.toByte() && record[marker + 1] == 0.toByte() &&
                        record[marker + 2] == 0.toByte() && record[marker + 3] == 1.toByte()
                }
                val flowRecordValid = flowIndex in (headerIndex + 1).toLong() until records.size.toLong() &&
                    records.getOrElse(flowIndex.toInt() + 1) { input.length().toInt() } - records[flowIndex.toInt()] >= 12 &&
                    ascii(readAt(input, records[flowIndex.toInt()].toLong(), 4), 0, 4) == "FDST"
                return headerIndex.takeIf {
                    ascii(kf8Header, 16, 4) == "MOBI" && unsignedInt(kf8Header, 104) == 8L &&
                        kf8Length >= 248 && 16L + kf8Length <= kf8Header.size &&
                        flowRecordValid && isIndexRecord(divisionIndex) && isIndexRecord(skeletonIndex)
                }
            }
            entryOffset += entryLength.toInt()
        }
        return null
    }

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
            val hybridKf8Index = findHybridKf8HeaderIndex(input, records, firstRecord)
            val selectedHeaderIndex = hybridKf8Index ?: 0
            val bookHeader = if (hybridKf8Index == null) firstRecord else {
                val start = records[hybridKf8Index]
                val end = records.getOrElse(hybridKf8Index + 1) { input.length().toInt() }
                readAt(input, start.toLong(), end - start)
            }
            val textRecordStart = selectedHeaderIndex + 1
            val compression = unsignedShort(bookHeader, 0)
            val declaredLength = unsignedInt(bookHeader, 4)
            val textRecordCount = unsignedShort(bookHeader, 8)
            val recordSize = unsignedShort(bookHeader, 10)
            val encryption = unsignedShort(bookHeader, 12)
            val mobiHeaderLength = unsignedInt(bookHeader, 20)
            if (bookHeader.size < 16 || ascii(bookHeader, 16, 4) != "MOBI" ||
                mobiHeaderLength < 24 || 16L + mobiHeaderLength > bookHeader.size
            ) {
                throw IOException("O cabeçalho do livro MOBI é inválido.")
            }
            if (encryption != 0) throw IOException("Livros MOBI protegidos por DRM não são compatíveis.")
            if (compression !in setOf(COMPRESSION_NONE, COMPRESSION_PALMDOC, COMPRESSION_HUFF_CDIC)) {
                throw IOException("O tipo de compressão deste MOBI não é compatível.")
            }
            if (declaredLength !in 1..MAX_TEXT_BYTES.toLong() ||
                textRecordCount == 0 || textRecordStart + textRecordCount > recordCount ||
                recordSize !in 1..8_192
            ) {
                throw IOException("O tamanho ou a paginação do livro MOBI é inválida.")
            }
            val extraDataFlags = if (mobiHeaderLength >= 228 && bookHeader.size >= 244) {
                unsignedShort(bookHeader, 242)
            } else {
                0
            }
            val huffDecoder = if (compression == COMPRESSION_HUFF_CDIC) {
                val firstHuffRecord = unsignedInt(bookHeader, 112) + selectedHeaderIndex
                val huffRecordCount = unsignedInt(bookHeader, 116)
                if (firstHuffRecord < textRecordStart + textRecordCount.toLong() ||
                    huffRecordCount !in 2..MAX_RECORDS.toLong() ||
                    firstHuffRecord + huffRecordCount > recordCount
                ) {
                    throw IOException("O índice dos registros HUFF/CDIC é inválido.")
                }
                var dictionaryBytes = 0L
                val huffRecords = List(huffRecordCount.toInt()) { offset ->
                    val recordIndex = firstHuffRecord.toInt() + offset
                    val start = records[recordIndex]
                    val end = records.getOrElse(recordIndex + 1) { input.length().toInt() }
                    if (end <= start || end - start > MAX_HUFF_DICTIONARY_BYTES) {
                        throw IOException("Um registro HUFF/CDIC está vazio ou excede o limite.")
                    }
                    dictionaryBytes += end - start
                    if (dictionaryBytes > MAX_HUFF_DICTIONARY_BYTES) {
                        throw IOException("O dicionário HUFF/CDIC excede o limite de leitura.")
                    }
                    readAt(input, start.toLong(), end - start)
                }
                HuffCdicTextDecoder.create(huffRecords)
            } else {
                null
            }

            val output = BoundedByteOutputStream(declaredLength.toInt())
            repeat(textRecordCount) { index ->
                val recordIndex = textRecordStart + index
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
                else if (compression == COMPRESSION_PALMDOC) decompressPalmDoc(textRecord, output)
                else output.writeBounded(requireNotNull(huffDecoder).decode(textRecord, declaredLength.toInt() - output.size()))
            }
            if (output.size() != declaredLength.toInt()) {
                throw IOException("O conteúdo MOBI não corresponde ao tamanho declarado.")
            }

            val charset = mobiCharset(bookHeader)
            if (unsignedInt(bookHeader, 104) == 8L) {
                val html = Kf8HtmlReconstructor.reconstruct(
                    readRecord = { index ->
                        val start = records[index]
                        val end = records.getOrElse(index + 1) { input.length().toInt() }
                        if (end <= start || end - start > MAX_HUFF_DICTIONARY_BYTES) {
                            throw IOException("Um registro de índice KF8 excede o limite de leitura.")
                        }
                        readAt(input, start.toLong(), end - start)
                    },
                    recordCount = recordCount,
                    header = bookHeader,
                    rawText = output.toByteArray(),
                    charset = charset,
                )
                if (html.isBlank()) throw IOException("O livro AZW3 não contém texto para leitura.")
                return@use html
            }
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
        private val maxBytes = initialCapacity

        fun writeBounded(bytes: ByteArray) {
            if (bytes.size > maxBytes - count) {
                throw IOException("O texto MOBI excede o tamanho declarado.")
            }
            write(bytes)
        }

        fun copyBackReference(distance: Int, length: Int) {
            if (distance <= 0 || distance > count) {
                throw IOException("Uma referência PalmDOC aponta para fora do texto.")
            }
            if (length > maxBytes - count) {
                throw IOException("O texto MOBI excede o tamanho declarado.")
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
