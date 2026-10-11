package org.mulletaflix.feature.itemdetail

import java.io.FilterInputStream
import java.io.IOException
import java.io.InputStream
import java.util.zip.ZipFile
import org.xmlpull.v1.XmlPullParser
import org.xmlpull.v1.XmlPullParserFactory

internal data class DocxBookHeading(val title: String, val depth: Int, val textOffset: Int)

internal data class DocxBookContent(val text: String, val headings: List<DocxBookHeading>)

/** Extracts readable paragraphs from a DOCX package without unpacking files to disk. */
internal object DocxBookTextExtractor {
    const val CONTENT_TYPE = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    const val MAX_PACKAGE_BYTES = 64L * 1024L * 1024L
    const val MAX_DOCUMENT_XML_BYTES = 16L * 1024L * 1024L
    private const val DOCUMENT_ENTRY = "word/document.xml"
    private const val STYLES_ENTRY = "word/styles.xml"
    private const val CONTENT_TYPES_ENTRY = "[Content_Types].xml"
    private const val MAX_TEXT_CHARACTERS = 8 * 1024 * 1024
    private const val MAX_HEADINGS = 10_000
    private const val MAX_STYLES_XML_BYTES = 4L * 1024L * 1024L
    private const val MAX_PARAGRAPH_STYLES = 4_096
    private const val MAX_STYLE_INHERITANCE_DEPTH = 32
    private val suppressedElements = setOf("del", "moveFrom")

    fun supports(contentType: String?): Boolean = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.equals(CONTENT_TYPE, ignoreCase = true) == true

    /** Identifies DOCX by package entries, including when the server sends a generic MIME type. */
    fun isDocxPackage(file: java.io.File): Boolean = runCatching {
        if (!file.isFile || file.length() < 4L) return@runCatching false
        ZipFile(file).use { zip ->
            zip.getEntry(CONTENT_TYPES_ENTRY)?.isDirectory == false &&
                zip.getEntry(DOCUMENT_ENTRY)?.isDirectory == false
        }
    }.getOrDefault(false)

    fun extract(file: java.io.File): String {
        return extractContent(file).text
    }

    fun extractContent(file: java.io.File): DocxBookContent {
        if (!file.isFile || file.length() <= 0L) throw IOException("O arquivo DOCX está vazio ou ausente.")
        if (file.length() > MAX_PACKAGE_BYTES) throw IOException("O arquivo DOCX excede o limite de leitura direta.")

        return try {
            ZipFile(file).use { zip ->
                val entry = zip.getEntry(DOCUMENT_ENTRY)
                    ?.takeUnless { it.isDirectory }
                    ?: throw IOException("O arquivo DOCX não contém o documento principal.")
                if (entry.size > MAX_DOCUMENT_XML_BYTES) {
                    throw IOException("O texto principal do DOCX excede o limite de leitura.")
                }
                val paragraphStyles = zip.getEntry(STYLES_ENTRY)
                    ?.takeUnless { it.isDirectory }
                    ?.let { stylesEntry ->
                        if (stylesEntry.size > MAX_STYLES_XML_BYTES) {
                            throw IOException("A configuração de estilos DOCX excede o limite de leitura.")
                        }
                        zip.getInputStream(stylesEntry).use { input ->
                            parseParagraphStyles(
                                BoundedXmlInputStream(
                                    input,
                                    MAX_STYLES_XML_BYTES,
                                    "A configuração de estilos DOCX excede o limite de leitura.",
                                ),
                            )
                        }
                    }.orEmpty()
                zip.getInputStream(entry).use { input ->
                    parseDocumentXml(
                        BoundedXmlInputStream(
                            input,
                            MAX_DOCUMENT_XML_BYTES,
                            "O texto principal do DOCX excede o limite de leitura.",
                        ),
                        paragraphStyles,
                    )
                }
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("Não foi possível ler o documento DOCX.", failure)
        }
    }

    private data class ParagraphStyleDefinition(val basedOn: String?, val outlineLevel: Int?)

    private fun parseParagraphStyles(input: InputStream): Map<String, ParagraphStyleDefinition> {
        val parser = XmlPullParserFactory.newInstance().apply { isNamespaceAware = true }.newPullParser()
        parser.setFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL, false)
        parser.setInput(input, null)
        val styles = LinkedHashMap<String, ParagraphStyleDefinition>()
        var styleDepth = 0
        var styleId: String? = null
        var styleType: String? = null
        var basedOn: String? = null
        var outlineLevel: Int? = null
        try {
            while (true) {
                when (parser.nextToken()) {
                    XmlPullParser.START_TAG -> {
                        val name = parser.name.substringAfter(':')
                        if (name == "style") {
                            styleDepth++
                            if (styleDepth == 1) {
                                styleId = attributeValue(parser, "styleId")
                                styleType = attributeValue(parser, "type")
                                basedOn = null
                                outlineLevel = null
                            }
                        } else if (styleDepth == 1 && styleType == "paragraph") {
                            when (name) {
                                "basedOn" -> basedOn = attributeValue(parser, "val")
                                "outlineLvl" -> outlineLevel = outlineLevel(parser)
                            }
                        }
                    }
                    XmlPullParser.END_TAG -> if (parser.name.substringAfter(':') == "style" && styleDepth > 0) {
                        if (styleDepth == 1 && styleType == "paragraph") {
                            styleId?.takeIf { it.isNotBlank() }?.let { id ->
                                if (styles.size >= MAX_PARAGRAPH_STYLES) {
                                    throw IOException("O DOCX excede o limite de estilos de parágrafo.")
                                }
                                styles[id] = ParagraphStyleDefinition(basedOn, outlineLevel)
                            }
                        }
                        styleDepth--
                    }
                    XmlPullParser.DOCDECL -> throw IOException("Os estilos DOCX contêm uma declaração XML não permitida.")
                    XmlPullParser.END_DOCUMENT -> break
                }
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("Os estilos XML do DOCX são inválidos.", failure)
        }
        return styles
    }

    private fun parseDocumentXml(
        input: InputStream,
        paragraphStyles: Map<String, ParagraphStyleDefinition>,
    ): DocxBookContent {
        val parser = XmlPullParserFactory.newInstance().apply { isNamespaceAware = true }.newPullParser()
        parser.setFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL, false)
        parser.setInput(input, null)

        val text = StringBuilder()
        val headings = mutableListOf<DocxBookHeading>()
        var documentRootSeen = false
        var bodyDepth = 0
        var textDepth = 0
        var suppressedDepth = 0
        var paragraphDepth = 0
        var paragraphStyleId: String? = null
        var directOutlineLevel: Int? = null
        var paragraphText: StringBuilder? = null
        var paragraphTextOffset: Int? = null

        try {
            while (true) {
                when (parser.nextToken()) {
                    XmlPullParser.START_TAG -> {
                        val name = parser.name.substringAfter(':')
                        if (name == "document" && bodyDepth == 0) documentRootSeen = true
                        if (suppressedDepth > 0) {
                            suppressedDepth++
                            continue
                        }
                        if (bodyDepth > 0 && name in suppressedElements) {
                            suppressedDepth = 1
                            continue
                        }
                        when (name) {
                            "body" -> bodyDepth++
                            "p" -> if (bodyDepth > 0) {
                                paragraphDepth++
                                if (paragraphDepth == 1) {
                                    paragraphStyleId = null
                                    directOutlineLevel = null
                                    paragraphText = StringBuilder()
                                    paragraphTextOffset = null
                                }
                            }
                            "pStyle" -> if (bodyDepth > 0 && paragraphDepth == 1) {
                                paragraphStyleId = attributeValue(parser, "val")
                            }
                            "outlineLvl" -> if (bodyDepth > 0 && paragraphDepth == 1) {
                                directOutlineLevel = outlineLevel(parser)
                            }
                            "t" -> if (bodyDepth > 0) textDepth++
                            "tab" -> if (bodyDepth > 0) append(text, "\t")
                            "br", "cr" -> if (bodyDepth > 0) appendLineBreak(text)
                        }
                    }
                    XmlPullParser.TEXT, XmlPullParser.CDSECT -> {
                        if (bodyDepth > 0 && suppressedDepth == 0 && textDepth > 0) {
                            val value = parser.text.orEmpty()
                            if (paragraphDepth > 0) {
                                val firstContent = value.indexOfFirst { !it.isWhitespace() }
                                if (paragraphTextOffset == null && firstContent >= 0) {
                                    paragraphTextOffset = text.length + firstContent
                                }
                                paragraphText?.append(value)
                            }
                            append(text, value)
                        }
                    }
                    XmlPullParser.ENTITY_REF -> {
                        if (bodyDepth > 0 && suppressedDepth == 0 && textDepth > 0) {
                            val value = resolveEntityReference(parser)
                            if (paragraphDepth > 0) {
                                val firstContent = value.indexOfFirst { !it.isWhitespace() }
                                if (paragraphTextOffset == null && firstContent >= 0) {
                                    paragraphTextOffset = text.length + firstContent
                                }
                                paragraphText?.append(value)
                            }
                            append(text, value)
                        }
                    }
                    XmlPullParser.END_TAG -> {
                        val name = parser.name.substringAfter(':')
                        if (suppressedDepth > 0) {
                            suppressedDepth--
                            continue
                        }
                        when (name) {
                            "t" -> if (textDepth > 0) textDepth--
                            "p" -> if (bodyDepth > 0 && paragraphDepth > 0) {
                                paragraphDepth--
                                if (paragraphDepth == 0) {
                                    val title = paragraphText?.toString()?.trim().orEmpty()
                                    val offset = paragraphTextOffset
                                    val depth = directOutlineLevel
                                        ?: paragraphStyleId?.let { resolveParagraphStyleLevel(it, paragraphStyles) }
                                        ?: paragraphStyleId?.let(::builtInHeadingLevel)
                                    if (title.isNotEmpty() && offset != null && depth != null) {
                                        if (headings.size >= MAX_HEADINGS) {
                                            throw IOException("O DOCX excede o limite de capítulos do sumário.")
                                        }
                                        headings += DocxBookHeading(title, depth, offset)
                                    }
                                    paragraphStyleId = null
                                    directOutlineLevel = null
                                    paragraphText = null
                                    paragraphTextOffset = null
                                }
                                appendLineBreak(text)
                            }
                            "tc" -> if (bodyDepth > 0) {
                                if (text.isNotEmpty() && text.last() == '\n') text.setLength(text.length - 1)
                                append(text, "\t")
                            }
                            "body" -> if (bodyDepth > 0) bodyDepth--
                        }
                    }
                    XmlPullParser.DOCDECL -> throw IOException("O documento DOCX contém uma declaração XML não permitida.")
                    XmlPullParser.END_DOCUMENT -> break
                }
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("O XML principal do DOCX é inválido.", failure)
        }

        if (!documentRootSeen || bodyDepth != 0) throw IOException("O DOCX não contém uma estrutura de documento válida.")
        val rawText = text.toString()
        val leadingTrim = rawText.indexOfFirst { !it.isWhitespace() }.let { if (it < 0) rawText.length else it }
        return DocxBookContent(
            text = rawText.trim(),
            headings = headings.map { it.copy(textOffset = (it.textOffset - leadingTrim).coerceAtLeast(0)) },
        )
    }

    private fun attributeValue(parser: XmlPullParser, name: String): String? =
        (0 until parser.attributeCount).firstOrNull { parser.getAttributeName(it) == name }
            ?.let(parser::getAttributeValue)

    private fun outlineLevel(parser: XmlPullParser): Int? =
        attributeValue(parser, "val")?.toIntOrNull()?.takeIf { it in 0..8 }

    private fun builtInHeadingLevel(styleId: String): Int? =
        Regex("(?i)^heading([1-9])$").matchEntire(styleId)
            ?.groupValues?.get(1)?.toIntOrNull()?.minus(1)

    private fun resolveParagraphStyleLevel(
        styleId: String,
        styles: Map<String, ParagraphStyleDefinition>,
    ): Int? {
        val visited = HashSet<String>()
        var currentStyleId: String? = styleId
        repeat(MAX_STYLE_INHERITANCE_DEPTH) {
            val current = currentStyleId ?: return null
            if (!visited.add(current)) return null
            val definition = styles[current]
            definition?.outlineLevel?.let { return it }
            builtInHeadingLevel(current)?.let { return it }
            currentStyleId = definition?.basedOn ?: return null
        }
        return null
    }

    private fun append(target: StringBuilder, value: String) {
        if (target.length.toLong() + value.length > MAX_TEXT_CHARACTERS) {
            throw IOException("O texto extraído do DOCX excede o limite de leitura.")
        }
        target.append(value)
    }

    private fun appendLineBreak(target: StringBuilder) {
        if (target.isNotEmpty() && target.last() != '\n') append(target, "\n")
    }

    private fun resolveEntityReference(parser: XmlPullParser): String {
        parser.text?.takeUnless { it.isEmpty() || it == parser.name }?.let { return it }
        val name = parser.name.orEmpty()
        val codePoint = when {
            name.startsWith("#x", ignoreCase = true) -> name.drop(2).toIntOrNull(16)
            name.startsWith('#') -> name.drop(1).toIntOrNull()
            else -> null
        }
        if (codePoint != null) {
            val validXmlCharacter = codePoint == 0x9 || codePoint == 0xA || codePoint == 0xD ||
                codePoint in 0x20..0xD7FF || codePoint in 0xE000..0xFFFD || codePoint in 0x10000..0x10FFFF
            if (validXmlCharacter) return String(Character.toChars(codePoint))
            throw IOException("O texto do DOCX contém uma referência numérica inválida.")
        }
        return when (name) {
            "amp" -> "&"
            "apos" -> "'"
            "gt" -> ">"
            "lt" -> "<"
            "quot" -> "\""
            else -> throw IOException("O texto do DOCX contém uma entidade XML não suportada.")
        }
    }

    private class BoundedXmlInputStream(
        input: InputStream,
        private val maxBytes: Long,
        private val limitMessage: String,
    ) : FilterInputStream(input) {
        private var bytesRead = 0L

        override fun read(): Int {
            if (bytesRead >= maxBytes) {
                if (super.read() < 0) return -1
                throw IOException(limitMessage)
            }
            return super.read().also { if (it >= 0) bytesRead++ }
        }

        override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
            if (length == 0) return 0
            val remaining = maxBytes - bytesRead
            if (remaining <= 0L) {
                if (super.read() < 0) return -1
                throw IOException(limitMessage)
            }
            val count = super.read(buffer, offset, minOf(length.toLong(), remaining).toInt())
            if (count > 0) bytesRead += count
            return count
        }
    }
}
