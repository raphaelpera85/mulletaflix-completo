package org.mulletaflix.feature.itemdetail

/** Converts common Markdown structure to safe, readable plain text without rendering HTML. */
internal object MarkdownBookTextParser {
    private val heading = Regex("^ {0,3}#{1,6}[ \\t]+(.*?)[ \\t]*#*[ \\t]*$")
    private val blockQuote = Regex("^ {0,3}>[ \\t]?(?:>[ \\t]?)*")
    private val unorderedList = Regex("^(\\s*)[-+*][ \\t]+(.*)$")
    private val orderedList = Regex("^(\\s*)(\\d+)[.)][ \\t]+(.*)$")
    private val taskMarker = Regex("^\\[([ xX])][ \\t]+(.*)$")
    private val tableDivider = Regex("^\\s*\\|?\\s*:?-{3,}:?\\s*(\\|\\s*:?-{3,}:?\\s*)+\\|?\\s*$")
    private val horizontalRule = Regex("^ {0,3}(?:(?:\\*\\s*){3,}|(?:-\\s*){3,}|(?:_\\s*){3,})$")
    private val inlineImage = Regex("!\\[([^]]*)]\\([^)]*\\)")
    private val inlineLink = Regex("\\[([^]]+)]\\((?:<([^>]+)>|([^\\s)]+))(?:\\s+['\"][^)]*['\"])?\\)")
    private val autolink = Regex("<((?:https?://|mailto:)[^>]+)>", RegexOption.IGNORE_CASE)
    private val codeSpan = Regex("(`+)(.*?)\\1")
    private val strong = Regex("(?<!\\w)(\\*\\*|__)(?=\\S)(.+?\\S)\\1(?!\\w)")
    private val strike = Regex("(?<!\\w)~~(?=\\S)(.+?\\S)~~(?!\\w)")
    private val emphasis = Regex("(?<![\\w*])([*_])(?=\\S)(.+?\\S)\\1(?![\\w*])")
    private val escapedPunctuation = Regex("\\\\([!\\\"#$%&'()*+,./:;<=>?@\\[\\]\\^_`{|}~-])")

    fun parse(markdown: String): String {
        val output = StringBuilder(markdown.length.coerceAtMost(PlainTextBookDocument.MAX_TEXT_BYTES.toInt()))
        var lineCount = 0
        fun appendLine(line: String) {
            if (lineCount++ > 0) output.append('\n')
            output.append(line)
        }
        var fenceCharacter: Char? = null
        var fenceLength = 0

        for (line in markdown.lineSequence()) {
            val fence = FENCE.find(line)
            if (fenceCharacter == null && fence != null) {
                fenceCharacter = fence.groupValues[1][0]
                fenceLength = fence.groupValues[1].length
                appendLine("")
                continue
            }
            if (fenceCharacter != null) {
                val close = line.trimStart()
                if (close.firstOrNull() == fenceCharacter && close.takeWhile { it == fenceCharacter }.length >= fenceLength &&
                    close.dropWhile { it == fenceCharacter }.isBlank()
                ) {
                    fenceCharacter = null
                    fenceLength = 0
                    appendLine("")
                } else {
                    appendLine(line)
                }
                continue
            }

            val trimmed = line.trim()
            if (trimmed.isEmpty()) {
                appendLine("")
                continue
            }
            if (horizontalRule.matches(line)) {
                appendLine("")
                continue
            }

            var readable = heading.matchEntire(line)?.groupValues?.get(1) ?: line
            readable = readable.replace(blockQuote, "    ")
            val unordered = unorderedList.matchEntire(readable)
            if (unordered != null) {
                val indent = unordered.groupValues[1].replace("\t", "    ")
                val task = taskMarker.matchEntire(unordered.groupValues[2])
                readable = if (task == null) "$indent• ${unordered.groupValues[2]}" else {
                    val marker = if (task.groupValues[1].equals("x", ignoreCase = true)) "☑" else "☐"
                    "$indent$marker ${task.groupValues[2]}"
                }
            } else {
                val ordered = orderedList.matchEntire(readable)
                if (ordered != null) readable = "${ordered.groupValues[1]}${ordered.groupValues[2]}. ${ordered.groupValues[3]}"
            }

            val cells = readable.trim().removePrefix("|").removeSuffix("|")
            val tableCells = cells.split('|')
            readable = if (cells.contains('|') && tableCells.size > 1) {
                if (tableDivider.matches(line)) "" else tableCells.joinToString("    |    ") { it.trim() }
            } else readable
            appendLine(formatInline(readable))
        }

        return output.toString()
            .replace(Regex("\\n{3,}"), "\n\n")
            .trim()
    }

    private fun formatInline(text: String): String = text
        .replace(inlineImage) { it.groupValues[1] }
        .replace(inlineLink) { it.groupValues[1] }
        .replace(autolink) { it.groupValues[1] }
        .replace(codeSpan) { it.groupValues[2] }
        .replace(strong) { it.groupValues[2] }
        .replace(strike) { it.groupValues[1] }
        .replace(emphasis) { it.groupValues[2] }
        .replace(escapedPunctuation) { it.groupValues[1] }

    private val FENCE = Regex("^ {0,3}(`{3,}|~{3,}).*$")
}
