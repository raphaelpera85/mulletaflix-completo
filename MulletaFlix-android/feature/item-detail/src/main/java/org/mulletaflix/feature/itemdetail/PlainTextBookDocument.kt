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
import java.util.UUID
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

internal data class PlainTextBookLinkSpan(
    val start: Int,
    val endExclusive: Int,
    val targetChunk: Int,
    val targetOffset: Int = 0,
)

internal data class PlainTextBookInlineImage(
    val textOffset: Int,
    val mimeType: String,
    val description: String,
    val bytes: ByteArray,
)

internal class PlainTextBookDocument private constructor(
    val chunks: List<String>,
    val chapters: List<PlainTextBookChapter> = emptyList(),
    val linksByChunk: Map<Int, List<PlainTextBookLinkSpan>> = emptyMap(),
    val inlineImagesByChunk: Map<Int, List<PlainTextBookInlineImage>> = emptyMap(),
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
        const val MAX_INLINE_IMAGE_BYTES = EmbeddedBookImageDecoder.DEFAULT_MAX_BYTES
        private const val MAX_HTML_HEADINGS = 10_000
        private const val MAX_HTML_LINK_MARKERS = 10_000
        private const val MAX_HTML_INLINE_IMAGES = 128
        private const val MAX_HTML_INLINE_IMAGE_TOTAL_BYTES = 4 * 1024 * 1024
        private const val HTML_MARKER_START = "\uE000MFLIX"
        private const val HTML_MARKER_END = "\uE001"
        private const val HTML_CONTENT_TYPE = "text/html"
        private const val HTML_CHARSET_SNIFF_BYTES = 1_024
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
        private val HTML_WINDOWS_1252_LABELS = setOf(
            "ascii", "ansi_x3.4-1968", "cp1252", "cp819", "csisolatin1", "ibm819", "iso-8859-1",
            "iso-ir-100", "iso8859-1", "iso88591", "iso_8859-1", "iso_8859-1:1987", "l1", "latin1",
            "us-ascii", "windows-1252", "x-cp1252",
        )
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
        private val HTML_BLOCK_ELEMENTS = setOf(
            "address", "article", "aside", "blockquote", "dd", "div", "dl", "dt", "fieldset",
            "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
            "header", "hr", "li", "main", "nav", "ol", "p", "pre", "section", "table", "tr", "ul",
        )
        private val HTML_PARAGRAPH_ELEMENTS = setOf("blockquote", "h1", "h2", "h3", "h4", "h5", "h6", "p")
        private val HIDDEN_STYLE_DECLARATION = Regex(
            "(?:^|;)\\s*(?:display\\s*:\\s*none|visibility\\s*:\\s*(?:hidden|collapse))(?:\\s*!important)?\\s*(?:;|$)",
            RegexOption.IGNORE_CASE,
        )

        fun supports(contentType: String?): Boolean = mimeType(contentType).lowercase() in
            setOf(CONTENT_TYPE, HTML_CONTENT_TYPE) + MARKDOWN_CONTENT_TYPES + FICTION_BOOK_CONTENT_TYPES + RTF_CONTENT_TYPES ||
            DocxBookTextExtractor.supports(contentType) || OdtBookTextExtractor.supports(contentType) ||
            FictionBookZipTextExtractor.supports(contentType) || MobiBookTextExtractor.supports(contentType) ||
            MobiBookTextExtractor.supportsKf8(contentType)

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
            if (MobiBookTextExtractor.supports(contentType) || MobiBookTextExtractor.supportsKf8(contentType) ||
                MobiBookTextExtractor.hasMobiDatabase(file)
            ) {
                val parsed = htmlToBookDocument(MobiBookTextExtractor.extractHtml(file))
                if (parsed.text.isBlank()) throw IOException("O livro MOBI não contém conteúdo para leitura.")
                val chunks = splitIntoChunks(parsed.text)
                val chapters = parsed.headings.map { heading ->
                    PlainTextBookChapter(heading.title, heading.depth, chunkIndexForOffset(chunks, heading.textOffset))
                }
                return PlainTextBookDocument(
                    chunks,
                    chapters,
                    mapHtmlLinksToChunks(chunks, parsed),
                    mapHtmlImagesToChunks(chunks, parsed),
                )
            }
            if (OdtBookTextExtractor.supports(contentType) || OdtBookTextExtractor.hasOpenDocumentTextPackage(file)) {
                val content = OdtBookTextExtractor.extractContent(file)
                val text = content.text
                    .replace("\u0000", "\uFFFD")
                    .replace("\r\n", "\n")
                    .replace('\r', '\n')
                if (text.isBlank()) throw IOException("O arquivo ODT não contém conteúdo para leitura.")
                val chunks = splitIntoChunks(text)
                val chapters = content.headings.map { heading ->
                    PlainTextBookChapter(heading.title, heading.depth, chunkIndexForOffset(chunks, heading.textOffset))
                }
                return PlainTextBookDocument(chunks, chapters)
            }
            if (DocxBookTextExtractor.supports(contentType) || DocxBookTextExtractor.isDocxPackage(file)) {
                val content = DocxBookTextExtractor.extractContent(file)
                val text = content.text
                    .replace("\u0000", "\uFFFD")
                    .replace("\r\n", "\n")
                    .replace('\r', '\n')
                if (text.isBlank()) throw IOException("O arquivo DOCX não contém conteúdo para leitura.")
                val chunks = splitIntoChunks(text)
                val chapters = content.headings.map { heading ->
                    PlainTextBookChapter(
                        title = heading.title,
                        depth = heading.depth,
                        chunkIndex = chunkIndexForOffset(chunks, heading.textOffset),
                    )
                }
                return PlainTextBookDocument(chunks, chapters)
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
            val payloadBytes = if (isRtf) RtfBookTextParser.preserveBinaryByteOffsets(bytes) else bytes
            val decoded = decode(payloadBytes, contentType, isFictionBook, isHtml)
            if (isFictionBook) {
                return openFictionBook(decoded, "O arquivo de texto não contém conteúdo para leitura.")
            }
            val markdownDocument = if (isMarkdown) MarkdownBookTextParser.parseDocument(decoded) else null
            val htmlDocument = if (isHtml) htmlToBookDocument(decoded) else null
            val text = when {
                isHtml -> requireNotNull(htmlDocument).text
                isMarkdown -> requireNotNull(markdownDocument).text
                isRtf -> RtfBookTextParser.parse(decoded)
                else -> decoded
            }
                .replace("\u0000", "\uFFFD")
                .replace("\r\n", "\n")
                .replace('\r', '\n')
            if (text.isBlank()) throw IOException("O arquivo de texto não contém conteúdo para leitura.")
            val chunks = splitIntoChunks(text)
            val chapters = (markdownDocument?.headings.orEmpty().map { heading ->
                PlainTextBookChapter(heading.title, heading.depth, chunkIndexForOffset(chunks, heading.textOffset))
            } + htmlDocument?.headings.orEmpty().map { heading ->
                PlainTextBookChapter(heading.title, heading.depth, chunkIndexForOffset(chunks, heading.textOffset))
            })
            return PlainTextBookDocument(
                chunks,
                chapters,
                htmlDocument?.let { mapHtmlLinksToChunks(chunks, it) }.orEmpty(),
                htmlDocument?.let { mapHtmlImagesToChunks(chunks, it) }.orEmpty(),
            )
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
            html: Boolean = false,
        ): String = when {
            bytes.startsWith(UTF8_BOM) -> String(bytes, UTF8_BOM.size, bytes.size - UTF8_BOM.size, StandardCharsets.UTF_8)
            bytes.size >= 2 && bytes[0] == 0xFF.toByte() && bytes[1] == 0xFE.toByte() ->
                String(bytes, 2, bytes.size - 2, StandardCharsets.UTF_16LE)
            bytes.size >= 2 && bytes[0] == 0xFE.toByte() && bytes[1] == 0xFF.toByte() ->
                String(bytes, 2, bytes.size - 2, StandardCharsets.UTF_16BE)
            fictionBook && bytes.isUtf16BeXmlWithoutBom() -> String(bytes, StandardCharsets.UTF_16BE)
            fictionBook && bytes.isUtf16LeXmlWithoutBom() -> String(bytes, StandardCharsets.UTF_16LE)
            else -> (declaredCharset(contentType) ?: if (html) htmlMetaCharset(bytes) else null)
                ?.let { String(bytes, it) }
                ?: decodeUtf8(bytes)
                ?: String(bytes, WINDOWS_1252)
        }

        private fun htmlMetaCharset(bytes: ByteArray): Charset? {
            if (bytes.isEmpty()) return null
            val prefix = String(
                bytes,
                0,
                minOf(bytes.size, HTML_CHARSET_SNIFF_BYTES),
                StandardCharsets.ISO_8859_1,
            )
            var cursor = 0
            while (cursor < prefix.length) {
                val tagStart = prefix.indexOf('<', cursor)
                if (tagStart < 0) break
                if (prefix.startsWith("<!--", tagStart)) {
                    val commentEnd = prefix.indexOf("-->", tagStart + 4)
                    if (commentEnd < 0) break
                    cursor = commentEnd + 3
                    continue
                }
                val firstTagCharacter = prefix.getOrNull(tagStart + 1)
                if (firstTagCharacter == null ||
                    !(firstTagCharacter.isLetter() || firstTagCharacter == '/' || firstTagCharacter == '!')
                ) {
                    cursor = tagStart + 1
                    continue
                }
                val tagEnd = findHtmlTagEnd(prefix, tagStart)
                if (tagEnd < 0) break
                val tag = parseHtmlTag(prefix, tagStart, tagEnd)
                if (tag != null && !tag.isClosing && tag.name == "plaintext") break
                if (tag != null && !tag.isClosing && tag.name in RAW_TEXT_HTML_ELEMENTS) {
                    cursor = findRawTextHtmlElementEnd(prefix, tag.name, tagEnd + 1)?.plus(1) ?: break
                    continue
                }
                if (tag != null && !tag.isClosing && tag.name == "meta") {
                    val direct = tag.attributes["charset"]?.trim()?.trimEnd('/')
                    val isContentType = tag.attributes["http-equiv"]
                        ?.equals("content-type", ignoreCase = true) == true
                    val legacy = if (isContentType) {
                        tag.attributes["content"]?.let { HTML_CONTENT_CHARSET_REGEX.find(it)?.groupValues?.get(1) }
                    } else {
                        null
                    }
                    for (charsetName in listOfNotNull(direct, legacy)) {
                        val charset = htmlCharset(charsetName)
                        if (charset != null) return charset
                    }
                }
                cursor = tagEnd + 1
            }
            return null
        }

        private fun htmlCharset(label: String): Charset? = when (label.trim().lowercase()) {
            "utf-16", "utf-16le", "utf-16be" -> StandardCharsets.UTF_8
            in HTML_WINDOWS_1252_LABELS -> WINDOWS_1252
            else -> runCatching { Charset.forName(label.trim()) }.getOrNull()
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

        private val HTML_CONTENT_CHARSET_REGEX = Regex(
            "charset\\s*=\\s*['\"]?\\s*([A-Za-z0-9._:-]+)",
            RegexOption.IGNORE_CASE,
        )

        private data class HtmlBookHeading(val title: String, val depth: Int, val textOffset: Int)
        private data class HtmlBookDocument(
            val text: String,
            val headings: List<HtmlBookHeading>,
            val links: List<HtmlBookLink>,
            val images: List<HtmlBookInlineImage>,
        )
        private data class HtmlBookInlineImage(
            val textOffset: Int,
            val mimeType: String,
            val description: String,
            val bytes: ByteArray,
        )
        private data class HtmlBookLink(
            val start: Int,
            val endExclusive: Int,
            val targetId: String,
            val targetOffset: Int = -1,
        )
        private data class HtmlHeadingMarker(
            val id: String,
            val depth: Int,
            val start: String,
            val end: String,
        )
        private data class HtmlMarkerEvent(val marker: HtmlHeadingMarker, val opening: Boolean)
        private data class HtmlInlineMarker(
            val token: String,
            val kind: Kind,
            val value: String = "",
            val image: HtmlBookInlineImage? = null,
        ) {
            enum class Kind { ANCHOR, LINK_START, LINK_END, IMAGE }
        }
        private data class ActiveHtmlLink(val targetId: String, val textOffset: Int)
        private data class ActiveHtmlHeading(
            val marker: HtmlHeadingMarker,
            val textOffset: Int,
            val title: StringBuilder = StringBuilder(),
        )

        private fun htmlToBookDocument(html: String): HtmlBookDocument {
            val withoutNonContent = removeNonContentHtmlElements(html)
            val markers = mutableListOf<HtmlHeadingMarker>()
            val activeMarkers = mutableListOf<HtmlHeadingMarker>()
            val inlineMarkers = mutableListOf<HtmlInlineMarker>()
            val activeInlineLinks = mutableListOf<HtmlInlineMarker>()
            val namespace = generateSequence { UUID.randomUUID().toString() }
                .first { !withoutNonContent.contains("$HTML_MARKER_START$it") }
            val headingTagPrefix = "mulletaflixheading${namespace.replace("-", "")}"
            val taggedHtml = markHtmlHeadings(withoutNonContent, headingTagPrefix)
            val rendered = renderHtmlText(
                taggedHtml, headingTagPrefix, namespace, markers, activeMarkers, inlineMarkers, activeInlineLinks,
            )
            val normalized = normalizeHtmlText(rendered)
            val text = StringBuilder(normalized.length)
            val headings = mutableListOf<HtmlBookHeading>()
            val activeHeadings = mutableListOf<ActiveHtmlHeading>()
            val anchors = linkedMapOf<String, Int>()
            val activeLinks = mutableListOf<ActiveHtmlLink>()
            val links = mutableListOf<HtmlBookLink>()
            val images = mutableListOf<HtmlBookInlineImage>()
            val eventsByToken = buildMap {
                markers.forEach { marker ->
                    put(marker.start, HtmlMarkerEvent(marker, opening = true))
                    put(marker.end, HtmlMarkerEvent(marker, opening = false))
                }
            }
            val inlineEventsByToken = inlineMarkers.associateBy(HtmlInlineMarker::token)
            var cursor = 0
            while (cursor < normalized.length) {
                val markerStart = normalized.indexOf(HTML_MARKER_START, cursor)
                if (markerStart < 0) {
                    appendHtmlText(normalized, cursor, normalized.length, text, activeHeadings)
                    break
                }
                appendHtmlText(normalized, cursor, markerStart, text, activeHeadings)
                val markerEnd = normalized.indexOf(HTML_MARKER_END, markerStart + HTML_MARKER_START.length)
                if (markerEnd < 0) {
                    appendHtmlText(normalized, markerStart, normalized.length, text, activeHeadings)
                    break
                }
                val tokenEnd = markerEnd + HTML_MARKER_END.length
                val event = eventsByToken[normalized.substring(markerStart, tokenEnd)]
                if (event != null && event.opening) {
                    activeHeadings += ActiveHtmlHeading(event.marker, text.length)
                } else if (event != null) {
                    val activeIndex = activeHeadings.indexOfLast { it.marker === event.marker }
                    if (activeIndex >= 0) {
                        val active = activeHeadings.removeAt(activeIndex)
                        val title = active.title.toString().replace(Regex("\\s+"), " ").trim()
                        if (title.isNotEmpty()) {
                            headings += HtmlBookHeading(title, active.marker.depth, active.textOffset)
                        }
                    }
                } else when (val inlineEvent = inlineEventsByToken[normalized.substring(markerStart, tokenEnd)]) {
                    null -> appendHtmlText(normalized, markerStart, tokenEnd, text, activeHeadings)
                    else -> when (inlineEvent.kind) {
                        HtmlInlineMarker.Kind.ANCHOR -> anchors.putIfAbsent(inlineEvent.value, text.length)
                        HtmlInlineMarker.Kind.LINK_START -> activeLinks += ActiveHtmlLink(inlineEvent.value, text.length)
                        HtmlInlineMarker.Kind.LINK_END -> {
                            val active = activeLinks.removeLastOrNull()
                            if (active != null && active.textOffset < text.length) {
                                links += HtmlBookLink(active.textOffset, text.length, active.targetId)
                            }
                        }
                        HtmlInlineMarker.Kind.IMAGE -> inlineEvent.image?.let { image ->
                            images += image.copy(textOffset = text.length)
                            text.append('\uFFFC')
                        }
                    }
                }
                cursor = tokenEnd
            }
            headings.sortBy(HtmlBookHeading::textOffset)
            return HtmlBookDocument(
                text = text.toString(),
                headings = headings,
                links = links.mapNotNull { link ->
                    anchors[link.targetId]?.let { anchorOffset ->
                        var targetOffset = anchorOffset
                        while (targetOffset < text.length && text[targetOffset].isWhitespace()) targetOffset++
                        link.copy(targetOffset = targetOffset)
                    }
                },
                images = images,
            )
        }

        /** Converts book HTML without building Android spans for every character. */
        private fun renderHtmlText(
            html: String,
            headingTagPrefix: String,
            namespace: String,
            markers: MutableList<HtmlHeadingMarker>,
            activeMarkers: MutableList<HtmlHeadingMarker>,
            inlineMarkers: MutableList<HtmlInlineMarker>,
            activeInlineLinks: MutableList<HtmlInlineMarker>,
        ): String {
            val output = StringBuilder(html.length)
            val decodedEntities = HashMap<String, String>()
            var cursor = 0
            while (cursor < html.length) {
                val tagStart = html.indexOf('<', cursor)
                val textEnd = if (tagStart < 0) html.length else tagStart
                appendDecodedHtmlText(html, cursor, textEnd, output, decodedEntities)
                if (tagStart < 0) break

                if (html.startsWith("<!--", tagStart)) {
                    val commentEnd = html.indexOf("-->", tagStart + 4)
                    cursor = if (commentEnd < 0) html.length else commentEnd + 3
                    continue
                }
                val tagEnd = findHtmlTagEnd(html, tagStart)
                if (tagEnd < 0) {
                    appendDecodedHtmlText(html, tagStart, html.length, output, decodedEntities)
                    break
                }
                val tag = parseHtmlTag(html, tagStart, tagEnd)
                if (tag == null) {
                    appendDecodedHtmlText(html, tagStart, tagEnd + 1, output, decodedEntities)
                    cursor = tagEnd + 1
                    continue
                }
                val depth = tag.name.removePrefix(headingTagPrefix).toIntOrNull()
                    ?.takeIf { tag.name.startsWith(headingTagPrefix) && it in 1..6 }
                if (!tag.isClosing) {
                    val target = tag.attributes["id"]?.takeIf(String::isNotBlank)
                        ?: tag.attributes["name"]?.takeIf { tag.name == "a" && it.isNotBlank() }
                    if (target != null && inlineMarkers.size < MAX_HTML_LINK_MARKERS) {
                        inlineMarkers += HtmlInlineMarker(
                            token = "$HTML_MARKER_START${namespace}A${inlineMarkers.size}$HTML_MARKER_END",
                            kind = HtmlInlineMarker.Kind.ANCHOR,
                            value = decodeHtmlAttribute(target, decodedEntities),
                        ).also { output.append(it.token) }
                    }
                    if (tag.name == "a") {
                        val href = tag.attributes["href"]?.let { decodeHtmlAttribute(it, decodedEntities) }
                        val fragment = href?.takeIf { it.startsWith('#') && it.length > 1 }?.substring(1)
                        if (fragment != null && inlineMarkers.size < MAX_HTML_LINK_MARKERS) {
                            val link = HtmlInlineMarker(
                                token = "$HTML_MARKER_START${namespace}L${inlineMarkers.size}$HTML_MARKER_END",
                                kind = HtmlInlineMarker.Kind.LINK_START,
                                value = fragment,
                            )
                            inlineMarkers += link
                            activeInlineLinks += link
                            output.append(link.token)
                        }
                    }
                    if (tag.name == "img" && inlineMarkers.count { it.kind == HtmlInlineMarker.Kind.IMAGE } < MAX_HTML_INLINE_IMAGES) {
                        val source = tag.attributes["src"]?.let { decodeHtmlAttribute(it, decodedEntities) }
                        val retainedBytes = inlineMarkers.sumOf { it.image?.bytes?.size ?: 0 }
                        val remainingBytes = MAX_HTML_INLINE_IMAGE_TOTAL_BYTES - retainedBytes
                        val image = source?.let {
                            decodeEmbeddedHtmlImage(
                                it,
                                tag.attributes["alt"].orEmpty(),
                                minOf(MAX_INLINE_IMAGE_BYTES, remainingBytes),
                            )
                        }
                        if (image != null) {
                            inlineMarkers += HtmlInlineMarker(
                                token = "$HTML_MARKER_START${namespace}I${inlineMarkers.size}$HTML_MARKER_END",
                                kind = HtmlInlineMarker.Kind.IMAGE,
                                image = image,
                            ).also { output.append(it.token) }
                        }
                    }
                }
                if (depth != null) {
                    if (!tag.isClosing) {
                        if (markers.size >= MAX_HTML_HEADINGS) {
                            throw IOException("O HTML excede o limite de capítulos do sumário.")
                        }
                        val id = markers.size.toString()
                        val marker = HtmlHeadingMarker(
                            id = id,
                            depth = depth,
                            start = "$HTML_MARKER_START${namespace}S$id$HTML_MARKER_END",
                            end = "$HTML_MARKER_START${namespace}E$id$HTML_MARKER_END",
                        )
                        markers += marker
                        activeMarkers += marker
                        output.append(marker.start)
                    } else {
                        val index = activeMarkers.indexOfLast { it.depth == depth }
                        if (index >= 0) output.append(activeMarkers.removeAt(index).end)
                    }
                }
                if (tag.isClosing && tag.name == "a") {
                    val activeLink = activeInlineLinks.removeLastOrNull()
                    if (activeLink != null) {
                        inlineMarkers += HtmlInlineMarker(
                            token = "$HTML_MARKER_START${namespace}E${inlineMarkers.size}$HTML_MARKER_END",
                            kind = HtmlInlineMarker.Kind.LINK_END,
                        ).also { output.append(it.token) }
                    }
                }
                if (!tag.isClosing && tag.name in VISIBLE_RAW_TEXT_HTML_ELEMENTS) {
                    val rawEnd = findRawTextHtmlElementEnd(html, tag.name, tagEnd + 1)
                    val contentEnd = rawEnd ?: html.length
                    appendDecodedHtmlText(html, tagEnd + 1, contentEnd, output, decodedEntities)
                    cursor = rawEnd?.plus(1) ?: html.length
                    continue
                } else if (tag.name == "br" || tag.name in HTML_BLOCK_ELEMENTS) {
                    appendHtmlLineBreak(output, blankLine = tag.name in HTML_PARAGRAPH_ELEMENTS)
                }
                cursor = tagEnd + 1
            }
            return output.toString()
        }

        private fun decodeHtmlAttribute(value: String, cachedEntities: MutableMap<String, String>): String =
            buildString(value.length) { appendDecodedHtmlText(value, 0, value.length, this, cachedEntities) }

        private fun mapHtmlLinksToChunks(
            chunks: List<String>,
            document: HtmlBookDocument,
        ): Map<Int, List<PlainTextBookLinkSpan>> {
            if (document.links.isEmpty()) return emptyMap()
            val spans = mutableMapOf<Int, MutableList<PlainTextBookLinkSpan>>()
            val chunkStarts = IntArray(chunks.size)
            var totalLength = 0
            chunks.forEachIndexed { chunkIndex, chunk ->
                chunkStarts[chunkIndex] = totalLength
                totalLength += chunk.length
            }
            document.links.forEach { link ->
                val targetChunk = chunkIndexForOffset(chunks, link.targetOffset)
                val targetOffset = link.targetOffset - chunkStarts[targetChunk]
                val firstChunk = (chunkStarts.binarySearch(link.start).let { result ->
                    if (result >= 0) result else -result - 2
                }).coerceAtLeast(0)
                var index = firstChunk
                while (index < chunks.size && chunkStarts[index] < link.endExclusive) {
                    val chunkStart = chunkStarts[index]
                    val chunkEnd = chunkStart + chunks[index].length
                    val start = maxOf(link.start, chunkStart)
                    val end = minOf(link.endExclusive, chunkEnd)
                    if (start < end) {
                        spans.getOrPut(index, ::mutableListOf) += PlainTextBookLinkSpan(
                            start = start - chunkStart,
                            endExclusive = end - chunkStart,
                            targetChunk = targetChunk,
                            targetOffset = targetOffset,
                        )
                    }
                    index++
                }
            }
            return spans
        }

        private fun mapHtmlImagesToChunks(
            chunks: List<String>,
            document: HtmlBookDocument,
        ): Map<Int, List<PlainTextBookInlineImage>> {
            if (document.images.isEmpty()) return emptyMap()
            val result = mutableMapOf<Int, MutableList<PlainTextBookInlineImage>>()
            document.images.forEach { image ->
                val chunkIndex = chunkIndexForOffset(chunks, image.textOffset)
                var chunkStart = 0
                repeat(chunkIndex) { chunkStart += chunks[it].length }
                result.getOrPut(chunkIndex, ::mutableListOf) += PlainTextBookInlineImage(
                    textOffset = image.textOffset - chunkStart,
                    mimeType = image.mimeType,
                    description = image.description,
                    bytes = image.bytes,
                )
            }
            return result
        }

        private fun decodeEmbeddedHtmlImage(
            source: String,
            description: String,
            maxBytes: Int,
        ): HtmlBookInlineImage? {
            val image = EmbeddedBookImageDecoder.decodeDataUri(source, description, maxBytes) ?: return null
            return HtmlBookInlineImage(
                textOffset = 0,
                mimeType = image.mimeType,
                description = image.description,
                bytes = image.bytes,
            )
        }

        private fun appendHtmlLineBreak(output: StringBuilder, blankLine: Boolean) {
            val required = if (blankLine) 2 else 1
            var trailing = 0
            while (trailing < required && trailing < output.length && output[output.lastIndex - trailing] == '\n') {
                trailing++
            }
            repeat(required - trailing) { output.append('\n') }
        }

        private fun appendDecodedHtmlText(
            source: String,
            start: Int,
            end: Int,
            output: StringBuilder,
            cachedEntities: MutableMap<String, String>,
        ) {
            var cursor = start
            while (cursor < end) {
                val ampersand = source.indexOf('&', cursor)
                if (ampersand < 0 || ampersand >= end) {
                    output.append(source, cursor, end)
                    return
                }
                output.append(source, cursor, ampersand)
                val semicolon = source.indexOf(';', ampersand + 1)
                if (semicolon < 0 || semicolon >= end || semicolon - ampersand > 33) {
                    output.append('&')
                    cursor = ampersand + 1
                    continue
                }
                val entity = source.substring(ampersand + 1, semicolon)
                val decoded = decodeHtmlEntity(entity, cachedEntities)
                if (decoded == null) output.append(source, ampersand, semicolon + 1) else output.append(decoded)
                cursor = semicolon + 1
            }
        }

        private fun decodeHtmlEntity(entity: String, cachedEntities: MutableMap<String, String>): String? {
            if (entity.startsWith('#')) {
                val hexadecimal = entity.startsWith("#x", ignoreCase = true)
                val number = entity.substring(if (hexadecimal) 2 else 1).toIntOrNull(if (hexadecimal) 16 else 10)
                    ?: return null
                if (!Character.isValidCodePoint(number) || number == 0) return "\uFFFD"
                return String(Character.toChars(number))
            }
            cachedEntities[entity]?.let { return it }
            val decoded = Html.fromHtml("&$entity;", Html.FROM_HTML_MODE_LEGACY).toString()
                .takeIf { it != "&$entity;" } ?: return null
            if (cachedEntities.size < 2_048) cachedEntities[entity] = decoded
            return decoded
        }

        private fun appendHtmlText(
            source: String,
            start: Int,
            end: Int,
            output: StringBuilder,
            activeHeadings: List<ActiveHtmlHeading>,
        ) {
            if (start >= end) return
            val text = source.substring(start, end)
            output.append(text)
            activeHeadings.lastOrNull()?.title?.append(text)
        }

        private fun normalizeHtmlText(text: String): String = text
            .replace(Regex("[\\t\\u000B\\f ]+"), " ")
            .replace(Regex(" *\\n *"), "\n")
            .replace(Regex("\\n{3,}"), "\n\n")
            .trim()

        private fun markHtmlHeadings(html: String, headingTagPrefix: String): String {
            val marked = StringBuilder(html.length)
            var copyFrom = 0
            var scanFrom = 0
            while (scanFrom < html.length) {
                val tagStart = html.indexOf('<', scanFrom)
                if (tagStart < 0) break
                val tagEnd = findHtmlTagEnd(html, tagStart)
                if (tagEnd < 0) break
                val tag = parseHtmlTag(html, tagStart, tagEnd)
                if (tag != null && !tag.isClosing && tag.name in VISIBLE_RAW_TEXT_HTML_ELEMENTS) {
                    scanFrom = findRawTextHtmlElementEnd(html, tag.name, tagEnd + 1)
                        ?.plus(1) ?: html.length
                    continue
                }
                val depth = tag?.name?.removePrefix("h")?.toIntOrNull()
                    ?.takeIf { tag.name == "h$it" && it in 1..6 }
                if (tag != null && depth != null) {
                    marked.append(html, copyFrom, tagStart)
                    if (tag.isClosing) {
                        marked.append("</$headingTagPrefix$depth></p>")
                    } else {
                        marked.append("<p><$headingTagPrefix$depth")
                        marked.append(html, tagStart + 1 + tag.name.length, tagEnd)
                        marked.append('>')
                    }
                    copyFrom = tagEnd + 1
                }
                scanFrom = tagEnd + 1
            }
            marked.append(html, copyFrom, html.length)
            return marked.toString()
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
            val attributes: Map<String, String>,
        )

        private fun parseHtmlTag(html: String, start: Int, end: Int): HtmlTag? {
            var index = start + 1
            while (index < end && html[index].isWhitespace()) index++
            val isClosing = index < end && html[index] == '/'
            if (isClosing) index++
            while (index < end && html[index].isWhitespace()) index++
            val nameStart = index
            while (index < end && !html[index].isWhitespace() && html[index] != '/') index++
            if (index == nameStart) return null
            val name = html.substring(nameStart, index).lowercase()
            var isHidden = false
            val attributes = mutableMapOf<String, String>()
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
                attributes.putIfAbsent(attributeName, attributeValue)
                isHidden = isHidden || attributeName == "hidden" ||
                    (attributeName == "aria-hidden" && attributeValue.equals("true", ignoreCase = true)) ||
                    (attributeName == "style" && HIDDEN_STYLE_DECLARATION.containsMatchIn(attributeValue))
            }
            return HtmlTag(name, isClosing, isHidden, attributes)
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
                val nameStart = candidate + 2
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

        internal fun splitIntoChunks(text: String): List<String> {
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
