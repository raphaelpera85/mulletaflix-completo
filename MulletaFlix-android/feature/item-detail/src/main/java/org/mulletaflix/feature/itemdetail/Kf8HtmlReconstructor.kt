package org.mulletaflix.feature.itemdetail

import java.io.IOException
import java.nio.charset.Charset
import java.nio.charset.StandardCharsets

/** Reassembles KF8 text flow from its bounded skeleton, fragment and division indexes. */
internal object Kf8HtmlReconstructor {
    private const val MAX_INDEX_ENTRIES = 32_768
    private const val MAX_INDEX_RECORDS = 256
    private const val MAX_INDEX_BYTES = 8 * 1024 * 1024
    private const val MAX_FLOWS = 64

    fun reconstruct(
        readRecord: (Int) -> ByteArray,
        recordCount: Int,
        header: ByteArray,
        rawText: ByteArray,
        charset: Charset,
    ): String {
        val skeletonIndex = header.unsignedInt(252).toRecordIndex(recordCount)
        val divisionIndex = header.unsignedInt(248).toRecordIndex(recordCount)
        val skeletons = readIndex(readRecord, recordCount, skeletonIndex, charset)
        val divisions = readIndex(readRecord, recordCount, divisionIndex, charset)
        val flowIndex = header.unsignedInt(192).toRecordIndex(recordCount)
        val textFlow = readTextFlow(readRecord(flowIndex), rawText)
        val result = StringBuilder(minOf(textFlow.size, 64 * 1024))
        var outputBytes = 0L
        var nextFragmentOffset = 0L
        var divisionCursor = 0

        skeletons.forEach { skeleton ->
            val divisionCount = (skeleton.values[1]?.firstOrNull()
                ?: throw IOException("O índice KF8 não informa os fragments do capítulo."))
                .toIntBounded(MAX_INDEX_ENTRIES)
            val skeletonStart = skeleton.values[6]?.getOrNull(0)
                ?: throw IOException("O índice KF8 não informa a posição do skeleton.")
            val skeletonLength = skeleton.values[6]?.getOrNull(1)
                ?: throw IOException("O índice KF8 não informa o tamanho do skeleton.")
            val skeletonEnd = checkedEnd(skeletonStart, skeletonLength, textFlow.size)
            var reconstructed = textFlow.copyOfRange(skeletonStart.toInt(), skeletonEnd.toInt())
            nextFragmentOffset = maxOf(nextFragmentOffset, skeletonEnd)

            repeat(divisionCount) {
                val division = divisions.getOrNull(divisionCursor++)
                    ?: throw IOException("O índice KF8 referencia um fragment inexistente.")
                val insertionPosition = division.name.toLongOrNull()
                    ?: throw IOException("Um fragment KF8 não informa uma posição de inserção válida.")
                val fragmentLength = division.values[6]?.getOrNull(1)
                    ?: throw IOException("Um fragment KF8 não informa o tamanho do conteúdo.")
                val fragmentEnd = checkedEnd(nextFragmentOffset, fragmentLength, textFlow.size)
                val localPosition = insertionPosition - skeletonStart
                if (localPosition !in 0..reconstructed.size.toLong()) {
                    throw IOException("Um fragment KF8 aponta para fora do skeleton.")
                }
                val insertion = localPosition.toInt()
                val fragment = textFlow.copyOfRange(nextFragmentOffset.toInt(), fragmentEnd.toInt())
                if (outputBytes + reconstructed.size + fragment.size > MAX_INDEX_BYTES.toLong() * 2) {
                    throw IOException("O HTML reconstruído do KF8 excede o limite de leitura.")
                }
                reconstructed = reconstructed.copyOfRange(0, insertion) + fragment +
                    reconstructed.copyOfRange(insertion, reconstructed.size)
                nextFragmentOffset = fragmentEnd
            }
            outputBytes += reconstructed.size
            if (outputBytes > MAX_INDEX_BYTES.toLong() * 2) {
                throw IOException("O HTML reconstruído do KF8 excede o limite de leitura.")
            }
            result.append(reconstructed.toString(charset))
        }
        if (divisionCursor != divisions.size || result.isEmpty()) {
            throw IOException("Os índices KF8 não produziram conteúdo legível.")
        }
        return result.toString()
    }

    private fun readTextFlow(record: ByteArray, rawText: ByteArray): ByteArray {
        if (record.size < 12 || record.ascii(0, 4) != "FDST") {
            throw IOException("A tabela de fluxos KF8 é inválida.")
        }
        val tableOffset = record.unsignedInt(4).toIntBounded(record.size)
        val flowCount = record.unsignedInt(8).toIntBounded(MAX_FLOWS)
        if (flowCount !in 1..MAX_FLOWS || tableOffset < 12 ||
            tableOffset > record.size - flowCount * 8
        ) throw IOException("A tabela de fluxos KF8 excede os limites.")
        val start = record.unsignedInt(tableOffset).toIntBounded(rawText.size)
        val end = record.unsignedInt(tableOffset + 4).toIntBounded(rawText.size)
        if (start >= end) throw IOException("O fluxo de texto KF8 está vazio ou inválido.")
        return rawText.copyOfRange(start, end)
    }

    private fun readIndex(
        readRecord: (Int) -> ByteArray,
        recordCount: Int,
        indexRecordNumber: Int,
        charset: Charset,
    ): List<IndexEntry> {
        val header = readRecord(indexRecordNumber)
        if (header.size < 184 || header.ascii(0, 4) != "INDX") {
            throw IOException("Um índice KF8 está incompleto.")
        }
        val headerLength = header.unsignedInt(4).toIntBounded(header.size)
        val firstEntryOffset = header.unsignedInt(20).toIntBounded(MAX_INDEX_BYTES)
        val indexRecordCount = header.unsignedInt(24).toIntBounded(MAX_INDEX_RECORDS)
        val tagxOffset = header.unsignedInt(180).toIntBounded(header.size)
        val cncCount = header.unsignedInt(48).toIntBounded(MAX_INDEX_RECORDS)
        if (headerLength < 184 || firstEntryOffset < 4 || indexRecordCount !in 1..MAX_INDEX_RECORDS ||
            tagxOffset < headerLength || tagxOffset > header.size - 12 ||
            indexRecordNumber.toLong() + indexRecordCount + cncCount >= recordCount
        ) throw IOException("O cabeçalho de um índice KF8 está fora dos limites.")
        val tags = parseTags(header, tagxOffset)
        val controlCount = header.unsignedInt(tagxOffset + 8).toIntBounded(255)
        if (controlCount !in 1..255) throw IOException("A tabela TAGX do KF8 é inválida.")

        val result = ArrayList<IndexEntry>()
        var bytesRead = header.size
        for (recordNumber in indexRecordNumber + 1..indexRecordNumber + indexRecordCount) {
            val data = readRecord(recordNumber)
            bytesRead += data.size
            if (bytesRead > MAX_INDEX_BYTES || data.size < 10 || result.size > MAX_INDEX_ENTRIES) {
                throw IOException("O índice KF8 excede o limite de leitura.")
            }
            val idxtOffset = data.unsignedInt(20).toIntBounded(data.size)
            val entryCount = data.unsignedInt(24).toIntBounded(MAX_INDEX_ENTRIES)
            if (entryCount == 0 || idxtOffset < 4 || idxtOffset > data.size - 4 - entryCount * 2 ||
                data.ascii(idxtOffset, 4) != "IDXT"
            ) throw IOException("As entradas de um índice KF8 estão inválidas.")
            val starts = List(entryCount) { data.unsignedShort(idxtOffset + 4 + it * 2) }
            if (starts.any { it >= idxtOffset } || starts.zipWithNext().any { (a, b) -> b <= a }) {
                throw IOException("A ordem das entradas do índice KF8 é inválida.")
            }
            starts.forEachIndexed { index, start ->
                val end = starts.getOrElse(index + 1) { idxtOffset }
                val entry = data.copyOfRange(start, end)
                if (entry.isEmpty()) throw IOException("Uma entrada do índice KF8 está vazia.")
                val nameLength = entry[0].toInt() and 0xFF
                if (nameLength > entry.size - 1) throw IOException("Um nome no índice KF8 está truncado.")
                val name = entry.copyOfRange(1, nameLength + 1).toString(charset)
                val values = parseTagValues(entry.copyOfRange(nameLength + 1, entry.size), tags, controlCount)
                result += IndexEntry(name, values)
            }
        }
        if (result.isEmpty() || result.size > MAX_INDEX_ENTRIES) {
            throw IOException("O índice KF8 não contém entradas utilizáveis.")
        }
        return result
    }

    private fun parseTags(header: ByteArray, offset: Int): List<TagDefinition> {
        if (header.ascii(offset, 4) != "TAGX") throw IOException("A assinatura TAGX do KF8 é inválida.")
        val length = header.unsignedInt(offset + 4).toIntBounded(header.size)
        if (length < 16 || offset > header.size - length || (length - 12) % 4 != 0) {
            throw IOException("A tabela TAGX do KF8 excede os limites.")
        }
        val tags = (offset + 12 until offset + length - 4 step 4).map { position ->
            TagDefinition(
                tag = header[position].toInt() and 0xFF,
                valuesPerEntry = header[position + 1].toInt() and 0xFF,
                mask = header[position + 2].toInt() and 0xFF,
                isEnd = header[position + 3].toInt() and 0xFF == 1,
            )
        }
        if (tags.isEmpty() || tags.size > 64 || tags.any { !it.isEnd && (it.mask == 0 || it.valuesPerEntry == 0) }) {
            throw IOException("A tabela TAGX do KF8 contém descritores inválidos.")
        }
        return tags
    }

    private fun parseTagValues(data: ByteArray, tags: List<TagDefinition>, controlCount: Int): Map<Int, List<Long>> {
        if (data.size < controlCount) throw IOException("Os controles de uma entrada KF8 estão truncados.")
        val controls = data.copyOfRange(0, controlCount)
        var controlIndex = 0
        var valueOffset = controlCount
        val values = linkedMapOf<Int, List<Long>>()
        tags.forEach { tag ->
            if (tag.isEnd) {
                controlIndex++
                if (controlIndex > controls.size) throw IOException("Os controles do índice KF8 estão incompletos.")
                return@forEach
            }
            val controlByte = controls.getOrNull(controlIndex)?.toInt()?.and(0xFF)
                ?: throw IOException("Os controles do índice KF8 estão incompletos.")
            val countBits = controlByte and tag.mask
            if (countBits == 0) return@forEach
            val valueCount = if (countBits == tag.mask && Integer.bitCount(tag.mask) > 1) {
                val (byteCount, consumed) = decodeForwardVwi(data, valueOffset)
                valueOffset += consumed
                var consumedValues = 0
                val parsed = ArrayList<Long>()
                while (consumedValues < byteCount) {
                    val (value, size) = decodeForwardVwi(data, valueOffset)
                    valueOffset += size
                    consumedValues += size
                    parsed += value
                }
                if (consumedValues.toLong() != byteCount || parsed.size % tag.valuesPerEntry != 0) {
                    throw IOException("Os valores variáveis do índice KF8 estão inconsistentes.")
                }
                values[tag.tag] = parsed
                return@forEach
            } else {
                if (countBits == tag.mask) 1 else {
                    var shiftedMask = tag.mask
                    var shiftedCount = countBits
                    while (shiftedMask and 1 == 0) {
                        shiftedMask = shiftedMask ushr 1
                        shiftedCount = shiftedCount ushr 1
                    }
                    shiftedCount
                }
            }
            if (valueCount > MAX_INDEX_ENTRIES) throw IOException("Uma entrada do índice KF8 excede o limite.")
            val parsed = ArrayList<Long>(valueCount * tag.valuesPerEntry)
            repeat(valueCount * tag.valuesPerEntry) {
                val (value, consumed) = decodeForwardVwi(data, valueOffset)
                valueOffset += consumed
                parsed += value
            }
            values[tag.tag] = parsed
        }
        return values
    }

    private fun decodeForwardVwi(data: ByteArray, offset: Int): Pair<Long, Int> {
        if (offset !in data.indices) throw IOException("Um valor variável do índice KF8 está truncado.")
        var value = 0L
        var cursor = offset
        while (cursor < data.size && cursor - offset < 5) {
            val byte = data[cursor++].toInt() and 0xFF
            if (value > (Long.MAX_VALUE ushr 7)) throw IOException("Um valor variável do índice KF8 excedeu o limite.")
            value = (value shl 7) or (byte and 0x7F).toLong()
            if (byte and 0x80 != 0) return value to (cursor - offset)
        }
        throw IOException("Um valor variável do índice KF8 está incompleto.")
    }

    private fun checkedEnd(start: Long, length: Long, limit: Int): Long {
        if (start < 0 || length <= 0 || start > limit - length) {
            throw IOException("Um fragment KF8 aponta para dados fora do fluxo de texto.")
        }
        return start + length
    }

    private fun Long.toRecordIndex(recordCount: Int): Int {
        if (this !in 1 until recordCount.toLong()) throw IOException("Um índice KF8 referencia um registro inválido.")
        return toInt()
    }

    private fun Long.toIntBounded(maximum: Int): Int {
        if (this !in 0..maximum.toLong()) throw IOException("Um valor do índice KF8 excede o limite.")
        return toInt()
    }

    private fun ByteArray.unsignedShort(offset: Int): Int {
        if (offset < 0 || offset > size - 2) throw IOException("Um campo do índice KF8 está truncado.")
        return ((this[offset].toInt() and 0xFF) shl 8) or (this[offset + 1].toInt() and 0xFF)
    }

    private fun ByteArray.unsignedInt(offset: Int): Long {
        if (offset < 0 || offset > size - 4) throw IOException("Um campo do índice KF8 está truncado.")
        return ((this[offset].toLong() and 0xFF) shl 24) or
            ((this[offset + 1].toLong() and 0xFF) shl 16) or
            ((this[offset + 2].toLong() and 0xFF) shl 8) or
            (this[offset + 3].toLong() and 0xFF)
    }

    private fun ByteArray.ascii(offset: Int, length: Int): String =
        if (offset < 0 || offset > size - length) "" else copyOfRange(offset, offset + length).toString(StandardCharsets.US_ASCII)

    private data class TagDefinition(val tag: Int, val valuesPerEntry: Int, val mask: Int, val isEnd: Boolean)
    private data class IndexEntry(val name: String, val values: Map<Int, List<Long>>)
}
