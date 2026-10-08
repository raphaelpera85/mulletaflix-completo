package org.mulletaflix.feature.itemdetail

import android.text.Html
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.IOException
import java.io.StringReader
import java.nio.ByteBuffer
import java.nio.charset.Charset
import java.nio.charset.CodingErrorAction
import java.nio.charset.StandardCharsets
import org.json.JSONObject
import org.readium.r2.shared.publication.Locator
import org.xmlpull.v1.XmlPullParser
import org.xmlpull.v1.XmlPullParserFactory

/** Small, virtualized text document for servers that return readable content directly. */
internal data class PlainTextBookChapter(
    val title: String,
    val depth: Int,
    val chunkIndex: Int,
)

internal class PlainTextBookDocument private constructor(
    val chunks: List<String>,
    val chapters: List<PlainTextBookChapter> = emptyList(),
) {
    val chunkCount: Int get() = chunks.size

    fun locatorForChunk(index: Int): Locator {
        require(index in chunks.indices)
        val progression = if (chunks.size == 1) 0.0 else index.toDouble() / (chunks.size - 1)
        return requireNotNull(
            Locator.fromJSON(
                JSONObject()
                    .put("href", "$LOCATOR_PREFIX$index")
                    .put("type", CONTENT_TYPE)
                    .put(
                        "locations",
                        JSONObject()
                            .put("position", index + 1)
                            .put("totalProgression", progression),
                    ),
            ),
        )
    }

    fun chunkIndexFromLocator(locator: Locator?): Int? {
        val href = locator?.href?.toString() ?: return null
        return href.removePrefix(LOCATOR_PREFIX)
            .takeIf { href.startsWith(LOCATOR_PREFIX) }
            ?.toIntOrNull()
            ?.takeIf { it in chunks.indices }
    }

    companion object {
        const val CONTENT_TYPE = "text/plain"
        const val MAX_TEXT_BYTES = 8L * 1024L * 1024L
        const val MAX_HTML_BYTES = 2L * 1024L * 1024L
        private const val HTML_CONTENT_TYPE = "text/html"
        private val MARKDOWN_CONTENT_TYPES = setOf("text/markdown", "text/x-markdown")
        private val RTF_CONTENT_TYPES = setOf("application/rtf", "application/x-rtf", "text/rtf")
        private val FICTION_BOOK_CONTENT_TYPES = setOf(
            "application/x-fictionbook+xml",
            "application/vnd.fictionbook+xml",
            "application/xml",
            "text/xml",
        )
        private val FICTION_BOOK_BLOCK_ELEMENTS = setOf(
            "annotation", "cite", "date", "empty-line", "epigraph", "p", "poem",
            "section", "stanza", "subtitle", "text-author", "title", "v",
        )
        private val FICTION_BOOK_ENTITIES = mapOf(
            "amp" to "&", "apos" to "'", "gt" to ">", "lt" to "<", "quot" to "\"",
            "mdash" to "—", "ndash" to "–", "hellip" to "…",
            "laquo" to "«", "raquo" to "»", "ldquo" to "“", "rdquo" to "”",
            "lsquo" to "‘", "rsquo" to "’", "nbsp" to "\u00a0", "thinsp" to "\u2009",
            "copy" to "©", "reg" to "®", "trade" to "™", "bull" to "•", "deg" to "°",
            "plusmn" to "±", "times" to "×", "divide" to "÷", "euro" to "€",
            "pound" to "£", "sect" to "§", "para" to "¶",
        )
        private const val MAX_CHUNK_CHARACTERS = 1_200
        private const val LOCATOR_PREFIX = "mulletaflix-text-chunk-"
        private val UTF8_BOM = byteArrayOf(0xEF.toByte(), 0xBB.toByte(), 0xBF.toByte())
        private val WINDOWS_1252 = Charset.forName("windows-1252")
        private val NON_CONTENT_HTML_ELEMENTS = setOf(
            "script", "style", "head", "iframe", "object", "svg", "template", "embed",
        )
        private val VOID_HTML_ELEMENTS = setOf(
            "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta",
            "param", "source", "track", "wbr",
        )
        private val RAW_TEXT_HTML_ELEMENTS = setOf(
            "script", "style", "textarea", "title", "xmp", "iframe", "noembed", "noframes", "plaintext",
        )
        private val VISIBLE_RAW_TEXT_HTML_ELEMENTS = setOf(
            "textarea", "title", "xmp", "noembed", "noframes", "plaintext",
        )
        private val HIDDEN_STYLE_DECLARATION = Regex(
            "(?:^|;)\\s*(?:display\\s*:\\s*none|visibility\\s*:\\s*(?:hidden|collapse))(?:\\s*!important)?\\s*(?:;|$)",
            RegexOption.IGNORE_CASE,
        )

        fun supports(contentType: String?): Boolean = mimeType(contentType).lowercase() in
            setOf(CONTENT_TYPE, HTML_CONTENT_TYPE) + MARKDOWN_CONTENT_TYPES + FICTION_BOOK_CONTENT_TYPES + RTF_CONTENT_TYPES ||
            DocxBookTextExtractor.supports(contentType) || OdtBookTextExtractor.supports(contentType) ||
            FictionBookZipTextExtractor.supports(contentType)

        fun isRtfContentType(contentType: String?): Boolean =
            mimeType(contentType).lowercase() in RTF_CONTENT_TYPES

        /** Detect RTF when servers return a generic binary MIME type. */
        fun hasRtfHeader(file: File): Boolean = runCatching {
            file.inputStream().buffered().use { input ->
                val signature = ByteArray(5)
                var count = 0
                while (count < signature.size) {
                    val read = input.read(signature, count, signature.size - count)
                    if (read < 0) break
                    count += read
                }
                count == signature.size && signature.contentEquals("{\\rtf".toByteArray(StandardCharsets.US_ASCII))
            }
        }.getOrDefault(false)

        fun isFictionBookContentType(contentType: String?): Boolean =
            mimeType(contentType).lowercase() in FICTION_BOOK_CONTENT_TYPES

        /** Detect FictionBook by its XML root when a server sends a generic MIME type. */
        fun hasFictionBookRoot(file: File): Boolean = runCatching {
            file.inputStream().buffered().use { input ->
                val parser = newFictionBookParser()
                parser.setInput(input, null)
                while (true) {
                    when (parser.nextToken()) {
                        XmlPullParser.START_TAG -> return@runCatching parser.name == "FictionBook"
                        XmlPullParser.DOCDECL -> Unit
                        XmlPullParser.END_DOCUMENT -> return@runCatching false
                    }
                }
                false
            }
        }.getOrDefault(false)

        fun isHtml(contentType: String?): Boolean = mimeType(contentType)
            .equals(HTML_CONTENT_TYPE, ignoreCase = true)

        fun isMarkdown(contentType: String?): Boolean = mimeType(contentType)
            .lowercase() in MARKDOWN_CONTENT_TYPES

        private fun mimeType(contentType: String?): String = contentType
            ?.substringBefore(';')
            ?.trim()
            .orEmpty()

        fun open(file: File, contentType: String? = CONTENT_TYPE): PlainTextBookDocument {
            if (!file.isFile || file.length() <= 0L) throw IOException("O arquivo de texto está vazio ou ausente.")
            if (OdtBookTextExtractor.supports(contentType) || OdtBookTextExtractor.hasOpenDocumentTextPackage(file)) {
                val text = OdtBookTextExtractor.extract(file)
                    .replace("\u0000", "\uFFFD")
                    .replace("\r\n", "\n")
                    .replace('\r', '\n')
                if (text.isBlank()) throw IOException("O arquivo ODT não contém conteúdo para leitura.")
                return PlainTextBookDocument(splitIntoChunks(text))
            }
            if (DocxBookTextExtractor.supports(contentType) || DocxBookTextExtractor.isDocxPackage(file)) {
                val text = DocxBookTextExtractor.extract(file)
                    .replace("\u0000", "\uFFFD")
                    .replace("\r\n", "\n")
                    .replace('\r', '\n')
                if (text.isBlank()) throw IOException("O arquivo DOCX não contém conteúdo para leitura.")
                return PlainTextBookDocument(splitIntoChunks(text))
            }
            if (FictionBookZipTextExtractor.supports(contentType) ||
                FictionBookZipTextExtractor.hasFictionBookPackage(file)
            ) {
                val xmlBytes = FictionBookZipTextExtractor.extractXml(file)
                val xml = decode(xmlBytes, contentType, fictionBook = true)
                return openFictionBook(xml, "O arquivo FB2 ZIP não contém conteúdo para leitura.")
            }
            val isHtml = isHtml(contentType)
            val isMarkdown = isMarkdown(contentType)
            val isFictionBook = isFictionBookContentType(contentType) || hasFictionBookRoot(file)
            val isRtf = isRtfContentType(contentType) || hasRtfHeader(file)
            val maxBytes = if (isHtml) MAX_HTML_BYTES else MAX_TEXT_BYTES
            if (!supports(contentType) && !isHtml && !isFictionBook && !isRtf) {
                throw IOException("Formato de texto não suportado.")
            }
            if (file.length() > maxBytes) throw IOException("O arquivo de texto excede o limite de leitura direta.")
            val bytes = file.inputStream().use { input ->
                val output = ByteArrayOutputStream(file.length().toInt().coerceAtLeast(32))
                val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                var copied = 0L
                while (true) {
                    val read = input.read(buffer)
                    if (read < 0) break
                    copied += read
                    if (copied > maxBytes) throw IOException("O arquivo de texto excede o limite de leitura direta.")
                    output.write(buffer, 0, read)
                }
                output.toByteArray()
            }
            val decoded = decode(bytes, contentType, isFictionBook)
            if (isFictionBook) {
                return openFictionBook(decoded, "O arquivo de texto não contém conteúdo para leitura.")
            }
            val text = when {
                isHtml -> htmlToText(decoded)
                isMarkdown -> MarkdownBookTextParser.parse(decoded)
                isRtf -> RtfBookTextParser.parse(decoded)
                else -> decoded
            }
                .replace("\u0000", "\uFFFD")
                .replace("\r\n", "\n")
                .replace('\r', '\n')
            if (text.isBlank()) throw IOException("O arquivo de texto não contém conteúdo para leitura.")
            return PlainTextBookDocument(splitIntoChunks(text))
        }

        private fun openFictionBook(xml: String, emptyMessage: String): PlainTextBookDocument {
            val parsed = fictionBookToText(xml)
            if (parsed.text.isBlank()) throw IOException(emptyMessage)
            val chunks = splitIntoChunks(parsed.text)
            val chapters = parsed.headings.map { heading ->
                PlainTextBookChapter(
                    title = heading.title,
                    depth = heading.depth,
                    chunkIndex = chunkIndexForOffset(chunks, heading.rawTextOffset),
                )
            }
            return PlainTextBookDocument(chunks, chapters)
        }

        private fun chunkIndexForOffset(chunks: List<String>, offset: Int): Int {
            var chunkStart = 0
            chunks.forEachIndexed { index, chunk ->
                if (offset < chunkStart + chunk.length) return index
                chunkStart += chunk.length
            }
            return (chunks.size - 1).coerceAtLeast(0)
        }

        private data class FictionBookHeading(val title: String, val depth: Int, val rawTextOffset: Int)
        private data class ParsedFictionBook(val text: String, val headings: List<FictionBookHeading>)

        /** Reads FictionBook chapters and body text without resolving external entities. */
        private fun fictionBookToText(xml: String): ParsedFictionBook {
            val parser = newFictionBookParser()
            parser.setInput(StringReader(xml))
            val text = StringBuilder(xml.length.coerceAtMost(MAX_CHUNK_CHARACTERS * 4))
            val headings = mutableListOf<FictionBookHeading>()
            var rootSeen = false
            var bodyDepth = 0
            var binaryDepth = 0
            var sectionDepth = 0
            var activeTitleDepth: Int? = null
            var activeTitleText: StringBuilder? = null
            var activeTitleOffset: Int? = null
            try {
                while (true) {
                    when (parser.nextToken()) {
                    XmlPullParser.START_TAG -> {
                        val name = parser.name.substringAfter(':')
                        if (!rootSeen) {
                            if (name != "FictionBook") throw IOException("O XML não é um livro FictionBook.")
                            rootSeen = true
                        }
                        if (name == "body") {
                            bodyDepth++
                        } else if (bodyDepth > 0 && name == "binary") {
                            binaryDepth++
                        } else if (bodyDepth > 0 && binaryDepth == 0 && name == "section") {
                            sectionDepth++
                            text.append('\n')
                        } else if (bodyDepth > 0 && binaryDepth == 0 && name == "title" && sectionDepth > 0) {
                            activeTitleDepth = sectionDepth - 1
                            activeTitleText = StringBuilder()
                            activeTitleOffset = null
                            text.append('\n')
                        } else if (bodyDepth > 0 && binaryDepth == 0 && name in FICTION_BOOK_BLOCK_ELEMENTS) {
                            text.append('\n')
                        }
                    }
                    XmlPullParser.END_TAG -> {
                        val name = parser.name.substringAfter(':')
                        if (bodyDepth > 0 && binaryDepth == 0 && name == "title" && activeTitleText != null) {
                            val title = activeTitleText.toString().trim()
                            val depth = activeTitleDepth
                            val offset = activeTitleOffset
                            if (title.isNotEmpty() && depth != null && offset != null) {
                                headings += FictionBookHeading(title, depth, offset)
                            }
                            activeTitleDepth = null
                            activeTitleText = null
                            activeTitleOffset = null
                            text.append('\n')
                        } else if (bodyDepth > 0 && binaryDepth == 0 && name in FICTION_BOOK_BLOCK_ELEMENTS) {
                            text.append('\n')
                        }
                        if (name == "binary" && binaryDepth > 0) binaryDepth--
                        if (name == "section" && bodyDepth > 0 && sectionDepth > 0) sectionDepth--
                        if (name == "body" && bodyDepth > 0) bodyDepth--
                    }
                    XmlPullParser.TEXT, XmlPullParser.CDSECT -> {
                        if (bodyDepth > 0 && binaryDepth == 0) {
                            val value = parser.text
                            activeTitleText?.let { title ->
                                if (activeTitleOffset == null) {
                                    val firstContent = value.indexOfFirst { !it.isWhitespace() }
                                    if (firstContent >= 0) activeTitleOffset = text.length + firstContent
                                }
                                title.append(value)
                            }
                            text.append(value)
                        }
                    }
                    XmlPullParser.ENTITY_REF -> {
                        if (bodyDepth > 0 && binaryDepth == 0) {
                            val entity = FICTION_BOOK_ENTITIES[parser.name]
                                ?: throw IOException("O livro contém uma entidade XML não suportada.")
                            activeTitleText?.let { title ->
                                if (activeTitleOffset == null && entity.any { !it.isWhitespace() }) {
                                    activeTitleOffset = text.length
                                }
                                title.append(entity)
                            }
                            text.append(entity)
                        }
                    }
                        // Factory parser keeps DTD processing disabled; ignore declarations without resolving them.
                        XmlPullParser.DOCDECL -> Unit
                        XmlPullParser.END_DOCUMENT -> break
                    }
                }
            } catch (error: IOException) {
                throw error
            } catch (error: Exception) {
                throw IOException("O livro FictionBook está malformado ou usa XML não suportado.", error)
            }
            if (!rootSeen) throw IOException("O arquivo não contém um livro FictionBook válido.")
            val (normalizedText, normalizedOffsets) = normalizeFictionBookText(
                text = text.toString(),
                offsets = headings.map(FictionBookHeading::rawTextOffset),
            )
            val normalizedHeadings = headings.mapIndexed { index, heading ->
                heading.copy(rawTextOffset = normalizedOffsets[index])
            }
            return ParsedFictionBook(normalizedText, normalizedHeadings)
        }

        private fun normalizeFictionBookText(text: String, offsets: List<Int>): Pair<String, List<Int>> {
            val normalized = StringBuilder(text.length)
            val normalizedOffsets = IntArray(offsets.size)
            var headingIndex = 0
            var pendingSpace = false
            var index = 0
            while (index <= text.length) {
                while (headingIndex < offsets.size && offsets[headingIndex] <= index) {
                    normalizedOffsets[headingIndex] = normalized.length
                    headingIndex++
                }
                if (index == text.length) break
                when (val character = text[index]) {
                    '\r' -> {
                        pendingSpace = false
                        appendFictionBookLineBreak(normalized)
                        if (text.getOrNull(index + 1) == '\n') index++
                    }
                    '\n' -> {
                        pendingSpace = false
                        appendFictionBookLineBreak(normalized)
                    }
                    '\t', '\u000B', '\u000C', ' ', '\u00A0' -> pendingSpace = true
                    else -> {
                        if (pendingSpace && normalized.lastOrNull() != '\n') normalized.append(' ')
                        pendingSpace = false
                        normalized.append(character)
                    }
                }
                index++
            }
            val leadingTrim = normalized.indexOfFirst { !it.isWhitespace() }.coerceAtLeast(0)
            return normalized.toString().trim() to normalizedOffsets.map { (it - leadingTrim).coerceAtLeast(0) }
        }

        private fun appendFictionBookLineBreak(output: StringBuilder) {
            if (output.lastOrNull() == '\n') {
                if (output.length < 2 || output[output.length - 2] != '\n') output.append('\n')
            } else {
                output.append('\n')
            }
        }

        private fun newFictionBookParser(): XmlPullParser =
            XmlPullParserFactory.newInstance().apply { isNamespaceAware = true }
                .newPullParser()

        private fun decode(
            bytes: ByteArray,
            contentType: String?,
            fictionBook: Boolean = false,
        ): String = when {
            bytes.startsWith(UTF8_BOM) -> String(bytes, UTF8_BOM.size, bytes.size - UTF8_BOM.size, StandardCharsets.UTF_8)
            bytes.size >= 2 && bytes[0] == 0xFF.toByte() && bytes[1] == 0xFE.toByte() ->
                String(bytes, 2, bytes.size - 2, StandardCharsets.UTF_16LE)
            bytes.size >= 2 && bytes[0] == 0xFE.toByte() && bytes[1] == 0xFF.toByte() ->
                String(bytes, 2, bytes.size - 2, StandardCharsets.UTF_16BE)
            fictionBook && bytes.isUtf16BeXmlWithoutBom() -> String(bytes, StandardCharsets.UTF_16BE)
            fictionBook && bytes.isUtf16LeXmlWithoutBom() -> String(bytes, StandardCharsets.UTF_16LE)
            else -> declaredCharset(contentType)?.let { String(bytes, it) }
                ?: decodeUtf8(bytes)
                ?: String(bytes, WINDOWS_1252)
        }

        private fun ByteArray.isUtf16BeXmlWithoutBom(): Boolean =
            size >= 4 && this[0] == 0x00.toByte() && this[1] == '<'.code.toByte() && this[2] == 0x00.toByte()

        private fun ByteArray.isUtf16LeXmlWithoutBom(): Boolean =
            size >= 4 && this[0] == '<'.code.toByte() && this[1] == 0x00.toByte() && this[3] == 0x00.toByte()

        private fun declaredCharset(contentType: String?): Charset? = contentType
            ?.split(';')
            ?.drop(1)
            ?.firstNotNullOfOrNull { parameter ->
                val parts = parameter.split('=', limit = 2)
                val name = parts.getOrNull(0)?.trim()?.lowercase()
                val value = parts.getOrNull(1)?.trim()?.trim('"', '\'')
                if (name == "charset" && !value.isNullOrBlank()) {
                    runCatching { Charset.forName(value) }.getOrNull()
                } else {
                    null
                }
            }

        private fun htmlToText(html: String): String {
            val withoutNonContent = removeNonContentHtmlElements(html)
            return Html.fromHtml(withoutNonContent, Html.FROM_HTML_MODE_LEGACY)
                .toString()
                .replace(Regex("[\\t\\u000B\\f ]+"), " ")
                .replace(Regex(" *\\n *"), "\n")
                .replace(Regex("\\n{3,}"), "\n\n")
                .trim()
        }

        /** Removes non-rendered/active elements in one forward scan; HTML input is untrusted. */
        private fun removeNonContentHtmlElements(html: String): String {
            val visible = StringBuilder(html.length)
            var copyFrom = 0
            var scanFrom = 0
            while (scanFrom < html.length) {
                val tagStart = html.indexOf('<', scanFrom)
                if (tagStart < 0) break
                if (html.startsWith("<!--", tagStart)) {
                    visible.append(html, copyFrom, tagStart)
                    val commentEnd = html.indexOf("-->", tagStart + 4)
                    if (commentEnd < 0) return visible.toString()
                    copyFrom = commentEnd + 3
                    scanFrom = copyFrom
                    continue
                }
                val tagEnd = findHtmlTagEnd(html, tagStart)
                if (tagEnd < 0) break
                val tag = parseHtmlTag(html, tagStart, tagEnd)
                if (tag != null && !tag.isClosing &&
                    (tag.name in NON_CONTENT_HTML_ELEMENTS || tag.isHidden)
                ) {
                    visible.append(html, copyFrom, tagStart)
                    if (tag.name in VOID_HTML_ELEMENTS) {
                        copyFrom = tagEnd + 1
                        scanFrom = copyFrom
                        continue
                    }
                    val closingEnd = findMatchingHtmlElementEnd(html, tag.name, tagEnd + 1)
                        ?: return visible.toString()
                    copyFrom = closingEnd + 1
                    scanFrom = copyFrom
                } else if (tag != null && !tag.isClosing &&
                    tag.name in VISIBLE_RAW_TEXT_HTML_ELEMENTS
                ) {
                    scanFrom = findRawTextHtmlElementEnd(html, tag.name, tagEnd + 1)
                        ?.plus(1) ?: html.length
                } else {
                    // Keep ordinary tags in the retained slice for Android's HTML parser.
                    scanFrom = tagEnd + 1
                }
            }
            visible.append(html, copyFrom, html.length)
            return visible.toString()
        }

        private data class HtmlTag(
            val name: String,
            val isClosing: Boolean,
            val isHidden: Boolean,
        )

        private fun parseHtmlTag(html: String, start: Int, end: Int): HtmlTag? {
            var index = start + 1
            while (index < end && html[index].isWhitespace()) index++
            val isClosing = index < end && html[index] == '/'
            if (isClosing) index++
            while (index < end && html[index].isWhitespace()) index++
            val nameStart = index
            while (index < end && (html[index].isLetterOrDigit() || html[index] == '-' || html[index] == ':')) index++
            if (index == nameStart) return null
            val name = html.substring(nameStart, index).lowercase()
            var isHidden = false
            while (index < end) {
                while (index < end && (html[index].isWhitespace() || html[index] == '/')) index++
                if (index >= end) break
                val attributeStart = index
                while (index < end && !html[index].isWhitespace() && html[index] !in "=/>") index++
                if (index == attributeStart) {
                    index++
                    continue
                }
                val attributeName = html.substring(attributeStart, index).lowercase()
                while (index < end && html[index].isWhitespace()) index++
                var attributeValue = ""
                if (index < end && html[index] == '=') {
                    index++
                    while (index < end && html[index].isWhitespace()) index++
                    if (index < end && html[index] in "'\"") {
                        val quote = html[index++]
                        val valueStart = index
                        while (index < end && html[index] != quote) index++
                        attributeValue = html.substring(valueStart, index)
                        if (index < end) index++
                    } else {
                        val valueStart = index
                        while (index < end && !html[index].isWhitespace() && html[index] != '>') index++
                        attributeValue = html.substring(valueStart, index)
                    }
                }
                isHidden = isHidden || attributeName == "hidden" ||
                    (attributeName == "aria-hidden" && attributeValue.equals("true", ignoreCase = true)) ||
                    (attributeName == "style" && HIDDEN_STYLE_DECLARATION.containsMatchIn(attributeValue))
            }
            return HtmlTag(name, isClosing, isHidden)
        }

        private fun findHtmlTagEnd(html: String, start: Int): Int {
            var quote: Char? = null
            for (index in start + 1 until html.length) {
                val character = html[index]
                if (quote != null) {
                    if (character == quote) quote = null
                } else when (character) {
                    '\'', '"' -> quote = character
                    '>' -> return index
                }
            }
            return -1
        }

        private fun findMatchingHtmlElementEnd(html: String, name: String, start: Int): Int? {
            if (name in RAW_TEXT_HTML_ELEMENTS) return findRawTextHtmlElementEnd(html, name, start)
            var depth = 1
            var searchFrom = start
            while (searchFrom < html.length) {
                val candidate = html.indexOf('<', searchFrom)
                if (candidate < 0) return null
                if (html.startsWith("<!--", candidate)) {
                    val commentEnd = html.indexOf("-->", candidate + 4)
                    if (commentEnd < 0) return null
                    searchFrom = commentEnd + 3
                    continue
                }
                val tagEnd = findHtmlTagEnd(html, candidate)
                if (tagEnd < 0) return null
                val tag = parseHtmlTag(html, candidate, tagEnd)
                if (tag != null && !tag.isClosing && tag.name in RAW_TEXT_HTML_ELEMENTS) {
                    val rawTextEnd = findRawTextHtmlElementEnd(html, tag.name, tagEnd + 1)
                        ?: return null
                    searchFrom = rawTextEnd + 1
                    continue
                }
                if (tag?.name == name) {
                    if (tag.isClosing) {
                        depth--
                        if (depth == 0) return tagEnd
                    } else if (tag.name !in VOID_HTML_ELEMENTS) {
                        depth++
                    }
                }
                searchFrom = tagEnd + 1
            }
            return null
        }

        private fun findRawTextHtmlElementEnd(html: String, name: String, start: Int): Int? {
            var searchFrom = start
            while (searchFrom < html.length) {
                val candidate = html.indexOf("</", searchFrom)
                if (candidate < 0) return null
                var nameStart = candidate + 2
                while (nameStart < html.length && html[nameStart].isWhitespace()) nameStart++
                val nameEnd = nameStart + name.length
                val boundary = html.getOrNull(nameEnd)
                if (boundary != null && (boundary == '>' || boundary.isWhitespace()) &&
                    html.regionMatches(nameStart, name, 0, name.length, ignoreCase = true)
                ) return findHtmlTagEnd(html, candidate).takeIf { it >= 0 }
                searchFrom = candidate + 2
            }
            return null
        }

        private fun decodeUtf8(bytes: ByteArray): String? = runCatching {
            StandardCharsets.UTF_8.newDecoder()
                .onMalformedInput(CodingErrorAction.REPORT)
                .onUnmappableCharacter(CodingErrorAction.REPORT)
                .decode(ByteBuffer.wrap(bytes))
                .toString()
        }.getOrNull()

        private fun splitIntoChunks(text: String): List<String> {
            val chunks = ArrayList<String>((text.length / MAX_CHUNK_CHARACTERS).coerceAtLeast(1))
            var start = 0
            while (start < text.length) {
                val limit = (start + MAX_CHUNK_CHARACTERS).coerceAtMost(text.length)
                var end = if (limit == text.length) limit else text.lastIndexOf('\n', limit - 1)
                    .takeIf { it >= start + MAX_CHUNK_CHARACTERS / 3 }
                    ?.plus(1) ?: limit
                if (end < text.length && end > start && Character.isHighSurrogate(text[end - 1]) &&
                    Character.isLowSurrogate(text[end])
                ) end--
                if (end == start) end = limit
                chunks += text.substring(start, end)
                start = end
            }
            return chunks
        }

        private fun ByteArray.startsWith(prefix: ByteArray): Boolean =
            size >= prefix.size && prefix.indices.all { this[it] == prefix[it] }
    }
}
