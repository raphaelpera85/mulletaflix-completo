package org.mulletaflix.feature.itemdetail

import java.io.FilterInputStream
import java.io.IOException
import java.io.InputStream
import java.util.zip.ZipFile
import org.xmlpull.v1.XmlPullParser
import org.xmlpull.v1.XmlPullParserFactory

/** Extracts readable paragraphs from a DOCX package without unpacking files to disk. */
internal object DocxBookTextExtractor {
    const val CONTENT_TYPE = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    const val MAX_PACKAGE_BYTES = 64L * 1024L * 1024L
    const val MAX_DOCUMENT_XML_BYTES = 16L * 1024L * 1024L
    private const val DOCUMENT_ENTRY = "word/document.xml"
    private const val CONTENT_TYPES_ENTRY = "[Content_Types].xml"
    private const val MAX_TEXT_CHARACTERS = 8 * 1024 * 1024
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
                zip.getInputStream(entry).use { input -> parseDocumentXml(BoundedXmlInputStream(input)) }
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("Não foi possível ler o documento DOCX.", failure)
        }
    }

    private fun parseDocumentXml(input: InputStream): String {
        val parser = XmlPullParserFactory.newInstance().apply { isNamespaceAware = true }.newPullParser()
        parser.setFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL, false)
        parser.setInput(input, null)

        val text = StringBuilder()
        var documentRootSeen = false
        var bodyDepth = 0
        var textDepth = 0
        var suppressedDepth = 0

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
                            "t" -> if (bodyDepth > 0) textDepth++
                            "tab" -> if (bodyDepth > 0) append(text, "\t")
                            "br", "cr" -> if (bodyDepth > 0) appendLineBreak(text)
                        }
                    }
                    XmlPullParser.TEXT, XmlPullParser.CDSECT -> {
                        if (bodyDepth > 0 && suppressedDepth == 0 && textDepth > 0) {
                            append(text, parser.text.orEmpty())
                        }
                    }
                    XmlPullParser.ENTITY_REF -> {
                        if (bodyDepth > 0 && suppressedDepth == 0 && textDepth > 0) {
                            append(text, resolveEntityReference(parser))
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
                            "p" -> if (bodyDepth > 0) appendLineBreak(text)
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
        return text.toString().trim()
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

    private class BoundedXmlInputStream(input: InputStream) : FilterInputStream(input) {
        private var bytesRead = 0L

        override fun read(): Int {
            if (bytesRead >= MAX_DOCUMENT_XML_BYTES) {
                if (super.read() < 0) return -1
                throw IOException("O texto principal do DOCX excede o limite de leitura.")
            }
            return super.read().also { if (it >= 0) bytesRead++ }
        }

        override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
            if (length == 0) return 0
            val remaining = MAX_DOCUMENT_XML_BYTES - bytesRead
            if (remaining <= 0L) {
                if (super.read() < 0) return -1
                throw IOException("O texto principal do DOCX excede o limite de leitura.")
            }
            val count = super.read(buffer, offset, minOf(length.toLong(), remaining).toInt())
            if (count > 0) bytesRead += count
            return count
        }
    }
}
