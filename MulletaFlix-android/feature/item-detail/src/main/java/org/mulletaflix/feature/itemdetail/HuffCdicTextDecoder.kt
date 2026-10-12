package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayOutputStream
import java.io.IOException

/** Bounded decoder for the HUFF/CDIC compression used by non-DRM Palm database books. */
internal class HuffCdicTextDecoder private constructor(
    private val firstLevel: Array<FirstLevelCode>,
    private val minimumCodes: Array<ULong>,
    private val maximumCodes: Array<ULong>,
    private val phrases: List<Phrase>,
) {
    fun decode(bytes: ByteArray, maximumOutputBytes: Int): ByteArray {
        if (maximumOutputBytes < 0) throw IOException("O limite de texto HUFF/CDIC é inválido.")
        val output = ByteArrayOutputStream(minOf(bytes.size * 2, maximumOutputBytes))
        decodeInto(bytes, output, maximumOutputBytes, BooleanArray(phrases.size), 0)
        return output.toByteArray()
    }

    private fun decodeInto(
        bytes: ByteArray,
        output: ByteArrayOutputStream,
        maximumOutputBytes: Int,
        decoding: BooleanArray,
        depth: Int,
    ) {
        if (depth > MAX_NESTED_PHRASE_DEPTH) throw IOException("O dicionário HUFF/CDIC excede a profundidade permitida.")
        var bitPosition = 0L
        val totalBits = bytes.size.toLong() * 8L
        var decodedCodes = 0
        while (bitPosition < totalBits) {
            if (++decodedCodes > MAX_CODES_PER_RECORD) {
                throw IOException("O bloco HUFF/CDIC contém códigos em excesso.")
            }
            val code = readCodeWindow(bytes, bitPosition)
            val initial = firstLevel[((code shr 24) and 0xFFu).toInt()]
            var codeLength = initial.length
            if (codeLength !in 1..32) throw IOException("A tabela HUFF/CDIC contém um código inválido.")

            val maxCode = if (initial.terminates) {
                if (codeLength > 8) throw IOException("A tabela HUFF/CDIC contém um código terminal inválido.")
                ((initial.maximum.toULong() + 1uL) shl (32 - codeLength)) - 1uL
            } else {
                var length = codeLength
                while (length <= 32 && code.toULong() < minimumCodes[length]) length++
                if (length > 32) throw IOException("O bloco HUFF/CDIC contém um código sem correspondência.")
                codeLength = length
                maximumCodes[length]
            }
            if (codeLength > totalBits - bitPosition) {
                break // os bits restantes do último byte são padding, não um código truncado.
            }
            val unsignedCode = code.toULong()
            if (unsignedCode > maxCode) throw IOException("O bloco HUFF/CDIC contém um código fora da tabela.")
            val phraseIndex = ((maxCode - unsignedCode) shr (32 - codeLength)).toLong()
            if (phraseIndex < 0L || phraseIndex >= phrases.size.toLong()) {
                throw IOException("O bloco HUFF/CDIC aponta para uma entrada inexistente.")
            }
            bitPosition += codeLength
            appendPhrase(phraseIndex.toInt(), output, maximumOutputBytes, decoding, depth)
        }
    }

    private fun appendPhrase(
        index: Int,
        output: ByteArrayOutputStream,
        maximumOutputBytes: Int,
        decoding: BooleanArray,
        depth: Int,
    ) {
        val phrase = phrases[index]
        if (phrase.literal) {
            appendBounded(output, phrase.bytes, maximumOutputBytes)
            return
        }
        if (decoding[index]) throw IOException("O dicionário HUFF/CDIC contém uma referência circular.")
        decoding[index] = true
        try {
            decodeInto(phrase.bytes, output, maximumOutputBytes, decoding, depth + 1)
        } finally {
            decoding[index] = false
        }
    }

    private fun appendBounded(output: ByteArrayOutputStream, bytes: ByteArray, maximumOutputBytes: Int) {
        if (bytes.size > maximumOutputBytes - output.size()) {
            throw IOException("O texto MOBI excede o tamanho declarado.")
        }
        output.write(bytes)
    }

    private fun readCodeWindow(bytes: ByteArray, bitPosition: Long): UInt {
        val byteOffset = (bitPosition / 8L).toInt()
        val bitOffset = (bitPosition % 8L).toInt()
        var window = 0uL
        repeat(5) { index ->
            val value = bytes.getOrNull(byteOffset + index)?.toUByte()?.toULong() ?: 0uL
            window = (window shl 8) or value
        }
        return ((window shr (8 - bitOffset)) and 0xFFFF_FFFFuL).toUInt()
    }

    private data class FirstLevelCode(val length: Int, val terminates: Boolean, val maximum: Long)
    private data class Phrase(val bytes: ByteArray, val literal: Boolean)

    companion object {
        private const val HUFF_HEADER_BYTES = 24
        private const val HUFF_FIRST_LEVEL_BYTES = 256 * 4
        private const val HUFF_SECOND_LEVEL_BYTES = 64 * 4
        private const val CDIC_HEADER_BYTES = 16
        private const val MAX_DICTIONARY_PHRASES = 65_536
        private const val MAX_NESTED_PHRASE_DEPTH = 16
        private const val MAX_CODES_PER_RECORD = 8 * 1024 * 1024

        fun create(records: List<ByteArray>): HuffCdicTextDecoder {
            if (records.size < 2) throw IOException("O arquivo MOBI não contém o dicionário HUFF/CDIC completo.")
            val huff = records.first()
            if (huff.size < HUFF_HEADER_BYTES || ascii(huff, 0, 4) != "HUFF" || unsignedInt(huff, 4) != 24L) {
                throw IOException("O cabeçalho HUFF está incompleto ou inválido.")
            }
            val firstOffset = unsignedInt(huff, 8)
            val secondOffset = unsignedInt(huff, 12)
            requireRegion(huff, firstOffset, HUFF_FIRST_LEVEL_BYTES)
            requireRegion(huff, secondOffset, HUFF_SECOND_LEVEL_BYTES)
            if (regionsOverlap(firstOffset, HUFF_FIRST_LEVEL_BYTES, secondOffset, HUFF_SECOND_LEVEL_BYTES)) {
                throw IOException("As tabelas HUFF se sobrepõem.")
            }

            val firstLevel = Array(256) { index ->
                val packed = unsignedInt(huff, firstOffset.toInt() + index * 4)
                val length = (packed and 0x1F).toInt()
                val terminates = packed and 0x80L != 0L
                if (length !in 1..32 || (length <= 8 && !terminates)) {
                    throw IOException("A tabela HUFF contém um código inválido.")
                }
                FirstLevelCode(length, terminates, packed ushr 8)
            }
            val minimumCodes = Array(33) { 0uL }
            val maximumCodes = Array(33) { 0uL }
            for (length in 1..32) {
                val min = unsignedInt(huff, secondOffset.toInt() + (length - 1) * 8)
                val max = unsignedInt(huff, secondOffset.toInt() + (length - 1) * 8 + 4)
                val shift = 32 - length
                minimumCodes[length] = min.toULong() shl shift
                maximumCodes[length] = ((max.toULong() + 1uL) shl shift) - 1uL
            }

            val phrases = ArrayList<Phrase>()
            var declaredPhraseCount: Long? = null
            records.drop(1).forEach { cdic ->
                if (cdic.size < CDIC_HEADER_BYTES || ascii(cdic, 0, 4) != "CDIC" || unsignedInt(cdic, 4) != 16L) {
                    throw IOException("O registro CDIC está incompleto ou inválido.")
                }
                val totalPhrases = unsignedInt(cdic, 8)
                val codeLength = unsignedInt(cdic, 12)
                if (totalPhrases !in 1..MAX_DICTIONARY_PHRASES.toLong() || codeLength !in 0..16) {
                    throw IOException("O tamanho do dicionário CDIC é inválido.")
                }
                if (declaredPhraseCount != null && declaredPhraseCount != totalPhrases) {
                    throw IOException("Os registros CDIC declaram tamanhos incompatíveis.")
                }
                declaredPhraseCount = totalPhrases
                val expectedCount = minOf(1L shl codeLength.toInt(), totalPhrases - phrases.size).toInt()
                val tableEnd = CDIC_HEADER_BYTES + expectedCount * 2
                if (expectedCount <= 0 || tableEnd > cdic.size) {
                    throw IOException("A tabela de frases CDIC está truncada.")
                }
                repeat(expectedCount) { index ->
                    val entryOffset = CDIC_HEADER_BYTES + index * 2
                    val phraseOffset = CDIC_HEADER_BYTES + 2 + unsignedShort(cdic, entryOffset)
                    if (phraseOffset < tableEnd || phraseOffset > cdic.size - 2) {
                        throw IOException("Uma frase CDIC aponta para dados inválidos.")
                    }
                    val packedLength = unsignedShort(cdic, phraseOffset)
                    val phraseLength = packedLength and 0x7FFF
                    val phraseStart = phraseOffset + 2
                    if (phraseLength == 0 || phraseLength > cdic.size - phraseStart) {
                        throw IOException("Uma frase CDIC está truncada ou vazia.")
                    }
                    phrases += Phrase(
                        cdic.copyOfRange(phraseStart, phraseStart + phraseLength),
                        packedLength and 0x8000 != 0,
                    )
                }
            }
            if (phrases.size.toLong() != declaredPhraseCount) {
                throw IOException("O dicionário CDIC está incompleto.")
            }
            return HuffCdicTextDecoder(firstLevel, minimumCodes, maximumCodes, phrases)
        }

        private fun requireRegion(bytes: ByteArray, offset: Long, length: Int) {
            if (offset < HUFF_HEADER_BYTES || offset > bytes.size.toLong() - length) {
                throw IOException("Uma tabela HUFF aponta para dados inválidos.")
            }
        }

        private fun regionsOverlap(firstOffset: Long, firstLength: Int, secondOffset: Long, secondLength: Int): Boolean =
            firstOffset < secondOffset + secondLength && secondOffset < firstOffset + firstLength

        private fun ascii(bytes: ByteArray, offset: Int, length: Int): String =
            if (offset < 0 || offset > bytes.size - length) "" else
                bytes.copyOfRange(offset, offset + length).toString(Charsets.US_ASCII)

        private fun unsignedShort(bytes: ByteArray, offset: Int): Int {
            if (offset < 0 || offset > bytes.size - 2) throw IOException("O registro HUFF/CDIC está incompleto.")
            return ((bytes[offset].toInt() and 0xFF) shl 8) or (bytes[offset + 1].toInt() and 0xFF)
        }

        private fun unsignedInt(bytes: ByteArray, offset: Int): Long {
            if (offset < 0 || offset > bytes.size - 4) throw IOException("O registro HUFF/CDIC está incompleto.")
            return ((bytes[offset].toLong() and 0xFF) shl 24) or
                ((bytes[offset + 1].toLong() and 0xFF) shl 16) or
                ((bytes[offset + 2].toLong() and 0xFF) shl 8) or
                (bytes[offset + 3].toLong() and 0xFF)
        }
    }
}
