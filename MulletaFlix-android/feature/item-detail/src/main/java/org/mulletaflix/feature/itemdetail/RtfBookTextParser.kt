package org.mulletaflix.feature.itemdetail

import java.io.IOException
import java.io.ByteArrayOutputStream
import java.nio.ByteBuffer
import java.nio.charset.Charset
import java.nio.charset.CharacterCodingException
import java.nio.charset.StandardCharsets
import java.nio.charset.UnsupportedCharsetException

/** Converts the readable text of a bounded RTF payload without evaluating embedded content. */
internal object RtfBookTextParser {
    private const val MAX_GROUP_DEPTH = 256
    private val skippedDestinations = setOf(
        "annotation", "colortbl", "datastore", "filetbl", "fldinst", "fonttbl", "footer", "footerf",
        "footerl", "footerr", "header", "headerf", "headerl", "headerr", "info", "listtable",
        "listoverridetable", "object", "pict", "stylesheet", "xmlnstbl",
    )
    private val windows1252 = Charset.forName("windows-1252")

    /** Replaces raw `\\binN` bytes before character decoding so their byte offsets stay exact. */
    fun preserveBinaryByteOffsets(bytes: ByteArray): ByteArray {
        val masked = bytes.copyOf()
        var cursor = 0
        while (cursor < masked.size) {
            if (masked[cursor] != '\\'.code.toByte()) {
                cursor++
                continue
            }
            cursor++
            if (cursor >= masked.size) break
            if (!masked[cursor].isAsciiLetter()) {
                cursor++
                continue
            }

            val wordStart = cursor
            while (cursor < masked.size && masked[cursor].isAsciiLetter()) cursor++
            val isBinary = String(masked, wordStart, cursor - wordStart, StandardCharsets.US_ASCII)
                .equals("bin", ignoreCase = true)
            val parameterStart = cursor
            if (cursor < masked.size && (masked[cursor] == '-'.code.toByte() || masked[cursor] == '+'.code.toByte())) {
                cursor++
            }
            val digitsStart = cursor
            while (cursor < masked.size && masked[cursor] in '0'.code.toByte()..'9'.code.toByte()) cursor++

            if (isBinary) {
                val length = if (digitsStart == cursor) null else
                    String(masked, parameterStart, cursor - parameterStart, StandardCharsets.US_ASCII).toLongOrNull()
                if (length == null || length < 0L) {
                    throw IOException("O RTF declara um tamanho binário inválido.")
                }
                if (cursor < masked.size && masked[cursor] == ' '.code.toByte()) cursor++
                val payloadEnd = cursor.toLong() + length
                if (payloadEnd > masked.size) {
                    throw IOException("O RTF contém um conteúdo binário incompleto.")
                }
                while (cursor < payloadEnd) masked[cursor++] = ' '.code.toByte()
            } else if (cursor < masked.size && masked[cursor] == ' '.code.toByte()) {
                cursor++
            }
        }
        return masked
    }

    fun parse(rtf: String): String {
        if (!rtf.trimStart().startsWith("{\\rtf", ignoreCase = true)) {
            throw IOException("O arquivo não contém um documento RTF válido.")
        }

        val states = ArrayDeque<State>()
        states.addLast(State())
        val output = StringBuilder(rtf.length.coerceAtMost(PlainTextBookDocument.MAX_TEXT_BYTES.toInt()))
        var index = 0

        while (index < rtf.length) {
            val state = states.last()
            when (val current = rtf[index]) {
                '{' -> {
                    if (states.size >= MAX_GROUP_DEPTH) throw IOException("O RTF possui grupos aninhados demais.")
                    states.addLast(state.copy(groupStart = true, fallbackRemaining = 0, ignoreNextDestination = false))
                    index++
                }
                '}' -> {
                    if (states.size == 1) throw IOException("O RTF contém grupos inválidos.")
                    states.removeLast()
                    index++
                }
                '\\' -> index = parseControl(rtf, index + 1, state, output)
                else -> {
                    if (!state.skipGroup) appendVisible(output, state, current)
                    if (!current.isWhitespace()) state.groupStart = false
                    index++
                }
            }
        }

        if (states.size != 1) throw IOException("O RTF contém grupos incompletos.")
        return output.toString()
            .replace("\u0000", "\uFFFD")
            .replace("\r\n", "\n")
            .replace('\r', '\n')
            .trim()
    }

    private fun parseControl(rtf: String, start: Int, state: State, output: StringBuilder): Int {
        if (start >= rtf.length) return start
        val symbol = rtf[start]
        if (!symbol.isLetter()) {
            when (symbol) {
                '\'', '{', '}', '\\' -> {
                    if (symbol == '\'') {
                        if (!state.skipGroup) return appendAnsiEscapeRun(rtf, start, state, output)
                        if (start + 2 >= rtf.length) throw IOException("O RTF contém um escape hexadecimal incompleto.")
                        if (rtf.substring(start + 1, start + 3).toIntOrNull(16) == null) {
                            throw IOException("O RTF contém um escape hexadecimal inválido.")
                        }
                        state.groupStart = false
                        return start + 3
                    }
                    if (!state.skipGroup) appendVisible(output, state, symbol)
                    state.groupStart = false
                    return start + 1
                }
                '*' -> {
                    state.ignoreNextDestination = true
                    return start + 1
                }
                '~' -> if (!state.skipGroup) appendVisible(output, state, '\u00a0')
                '_' -> if (!state.skipGroup) appendVisible(output, state, '\u2011')
                '-' -> Unit
                '\n', '\r' -> Unit
                else -> if (!state.skipGroup) appendVisible(output, state, symbol)
            }
            state.groupStart = false
            return start + 1
        }

        var end = start
        while (end < rtf.length && rtf[end].isLetter()) end++
        val word = rtf.substring(start, end).lowercase()
        val numberStart = end
        if (end < rtf.length && (rtf[end] == '-' || rtf[end] == '+')) end++
        while (end < rtf.length && rtf[end].isDigit()) end++
        val parameter = rtf.substring(numberStart, end).takeIf { it.isNotEmpty() }?.toIntOrNull()
        if (end < rtf.length && rtf[end] == ' ') end++

        if (state.ignoreNextDestination || (state.groupStart && word in skippedDestinations)) {
            state.skipGroup = true
            state.ignoreNextDestination = false
        }
        state.groupStart = false
        if (state.skipGroup) return skipBinaryPayload(rtf, end, word, parameter)

        when (word) {
            "ansicpg" -> {
                val codePage = parameter
                    ?: throw IOException("O RTF declara uma página de código inválida.")
                state.ansiCharset = ansiCharset(codePage)
            }
            "uc" -> parameter?.let { state.unicodeFallbackCount = it.coerceIn(0, 16) }
            "u" -> parameter?.let {
                if (!state.hidden) append(output, it.toChar())
                state.fallbackRemaining = state.unicodeFallbackCount
            }
            "par", "line", "page" -> if (!state.hidden) appendVisible(output, state, '\n')
            "tab" -> if (!state.hidden) appendVisible(output, state, '\t')
            "v" -> state.hidden = parameter != 0
            "emdash" -> if (!state.hidden) appendVisible(output, state, '\u2014')
            "endash" -> if (!state.hidden) appendVisible(output, state, '\u2013')
            "bullet" -> if (!state.hidden) appendVisible(output, state, '\u2022')
        }
        return skipBinaryPayload(rtf, end, word, parameter)
    }

    private fun skipBinaryPayload(rtf: String, end: Int, word: String, parameter: Int?): Int {
        if (word != "bin") return end
        if (parameter == null || parameter < 0) {
            throw IOException("O RTF declara um tamanho binário inválido.")
        }
        val payloadEnd = end.toLong() + parameter
        if (payloadEnd > rtf.length) {
            throw IOException("O RTF contém um conteúdo binário incompleto.")
        }
        return payloadEnd.toInt()
    }

    private fun appendVisible(output: StringBuilder, state: State, value: Char) {
        if (state.fallbackRemaining > 0) {
            state.fallbackRemaining--
        } else if (!state.hidden) {
            append(output, value)
        }
    }

    private fun append(output: StringBuilder, value: Char) {
        if (value == '\n' && output.isNotEmpty() && output.last() == '\n') return
        output.append(value)
    }

    private fun ansiCharset(codePage: Int): Charset {
        if (codePage !in 1..65_535) throw IOException("O RTF declara uma página de código inválida.")
        val charset = try {
            if (codePage == 65_001) StandardCharsets.UTF_8 else Charset.forName("windows-$codePage")
        } catch (failure: UnsupportedCharsetException) {
            throw IOException("A página de código ANSI $codePage do RTF não é compatível.", failure)
        } catch (failure: IllegalArgumentException) {
            throw IOException("A página de código ANSI $codePage do RTF não é compatível.", failure)
        }
        return charset
    }

    private fun appendAnsiEscapeRun(rtf: String, start: Int, state: State, output: StringBuilder): Int {
        val bytes = ByteArrayOutputStream()
        var cursor = start
        while (cursor < rtf.length && rtf[cursor] == '\'') {
            if (cursor + 2 >= rtf.length) throw IOException("O RTF contém um escape hexadecimal incompleto.")
            val value = rtf.substring(cursor + 1, cursor + 3).toIntOrNull(16)
                ?: throw IOException("O RTF contém um escape hexadecimal inválido.")
            bytes.write(value)
            cursor += 3
            if (cursor + 1 < rtf.length && rtf[cursor] == '\\' && rtf[cursor + 1] == '\'') cursor++
            else break
        }

        val decoded = try {
            state.ansiCharset.newDecoder().decode(ByteBuffer.wrap(bytes.toByteArray())).toString()
        } catch (failure: CharacterCodingException) {
            throw IOException("Os escapes RTF não são válidos para a página de código ANSI declarada.", failure)
        }
        decoded.forEach { appendVisible(output, state, it) }
        state.groupStart = false
        return cursor
    }

    private data class State(
        var skipGroup: Boolean = false,
        var hidden: Boolean = false,
        var groupStart: Boolean = true,
        var ignoreNextDestination: Boolean = false,
        var ansiCharset: Charset = windows1252,
        var unicodeFallbackCount: Int = 1,
        var fallbackRemaining: Int = 0,
    )

    private fun Byte.isAsciiLetter(): Boolean =
        this in 'A'.code.toByte()..'Z'.code.toByte() || this in 'a'.code.toByte()..'z'.code.toByte()
}
