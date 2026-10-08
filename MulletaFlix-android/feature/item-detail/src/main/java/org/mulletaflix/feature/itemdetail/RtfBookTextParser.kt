package org.mulletaflix.feature.itemdetail

import java.io.IOException
import java.nio.charset.Charset

/** Converts the readable text of a bounded RTF payload without evaluating embedded content. */
internal object RtfBookTextParser {
    private const val MAX_GROUP_DEPTH = 256
    private val skippedDestinations = setOf(
        "annotation", "colortbl", "datastore", "filetbl", "fldinst", "fonttbl", "footer", "footerf",
        "footerl", "footerr", "header", "headerf", "headerl", "headerr", "info", "listtable",
        "listoverridetable", "object", "pict", "stylesheet", "xmlnstbl",
    )
    private val windows1252 = Charset.forName("windows-1252")

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
                        if (start + 2 >= rtf.length) throw IOException("O RTF contém um escape hexadecimal incompleto.")
                        val value = rtf.substring(start + 1, start + 3).toIntOrNull(16)
                            ?: throw IOException("O RTF contém um escape hexadecimal inválido.")
                        if (!state.skipGroup) appendVisible(output, state, decodeWindows1252(value))
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
        if (word != "bin" || parameter == null || parameter <= 0) return end
        return (end + parameter).coerceAtMost(rtf.length)
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

    private fun decodeWindows1252(byte: Int): Char =
        String(byteArrayOf(byte.toByte()), windows1252).first()

    private data class State(
        var skipGroup: Boolean = false,
        var hidden: Boolean = false,
        var groupStart: Boolean = true,
        var ignoreNextDestination: Boolean = false,
        var unicodeFallbackCount: Int = 1,
        var fallbackRemaining: Int = 0,
    )
}
