package org.mulletaflix.feature.itemdetail

import java.io.ByteArrayOutputStream
import java.io.FilterInputStream
import java.io.IOException
import java.io.InputStream
import java.nio.charset.StandardCharsets
import java.util.zip.ZipFile
import org.xmlpull.v1.XmlPullParser
import org.xmlpull.v1.XmlPullParserFactory

/** Extracts the readable text from an OpenDocument Text package without unpacking it to disk. */
internal object OdtBookTextExtractor {
    const val CONTENT_TYPE = "application/vnd.oasis.opendocument.text"
    const val MAX_PACKAGE_BYTES = 64L * 1024L * 1024L
    const val MAX_CONTENT_XML_BYTES = 16L * 1024L * 1024L

    private const val MIME_ENTRY = "mimetype"
    private const val CONTENT_ENTRY = "content.xml"
    private const val MAX_MIME_ENTRY_BYTES = 128
    private const val MAX_TEXT_CHARACTERS = 8 * 1024 * 1024
    private const val ZIP_LOCAL_HEADER_BYTES = 30
    private const val MIME_DATA_OFFSET = 38
    private const val OFFICE_NAMESPACE = "urn:oasis:names:tc:opendocument:xmlns:office:1.0"
    private const val TEXT_NAMESPACE = "urn:oasis:names:tc:opendocument:xmlns:text:1.0"
    private const val TABLE_NAMESPACE = "urn:oasis:names:tc:opendocument:xmlns:table:1.0"
    private val mimeBytes = CONTENT_TYPE.toByteArray(StandardCharsets.US_ASCII)
    private val mimePrefixLength = MIME_DATA_OFFSET + mimeBytes.size
    val ZIP_MIMETYPE_PREFIX_BYTES: Int get() = mimePrefixLength

    fun supports(contentType: String?): Boolean = contentType
        ?.substringBefore(';')
        ?.trim()
        ?.equals(CONTENT_TYPE, ignoreCase = true) == true

    /** ODF's stored, first ZIP entry lets generic HTTP responses be identified before full download. */
    fun hasOpenDocumentTextZipPrefix(prefix: ByteArray): Boolean {
        if (prefix.size < mimePrefixLength) return false
        if (prefix[0] != 'P'.code.toByte() || prefix[1] != 'K'.code.toByte() ||
            prefix[2] != 0x03.toByte() || prefix[3] != 0x04.toByte()
        ) return false
        if (prefix[8] != 0.toByte() || prefix[9] != 0.toByte()) return false
        if (unsignedShort(prefix, 26) != MIME_ENTRY.length || unsignedShort(prefix, 28) != 0) return false
        if (!prefix.copyOfRange(ZIP_LOCAL_HEADER_BYTES, MIME_DATA_OFFSET)
                .contentEquals(MIME_ENTRY.toByteArray(StandardCharsets.US_ASCII))
        ) return false
        return prefix.copyOfRange(MIME_DATA_OFFSET, mimePrefixLength).contentEquals(mimeBytes)
    }

    /** Detects a generic ODT response from its small MIME entry and required main text entry. */
    fun hasOpenDocumentTextPackage(file: java.io.File): Boolean = runCatching {
        if (!file.isFile || file.length() < 4L || file.length() > MAX_PACKAGE_BYTES) return@runCatching false
        ZipFile(file).use { zip ->
            val mimeEntry = zip.getEntry(MIME_ENTRY)?.takeUnless { it.isDirectory } ?: return@use false
            val contentEntry = zip.getEntry(CONTENT_ENTRY)?.takeUnless { it.isDirectory } ?: return@use false
            mimeEntry.size in 0..MAX_MIME_ENTRY_BYTES.toLong() &&
                contentEntry.size <= MAX_CONTENT_XML_BYTES &&
                zip.getInputStream(mimeEntry).use(::readMimeEntry) == CONTENT_TYPE
        }
    }.getOrDefault(false)

    fun extract(file: java.io.File): String {
        if (!file.isFile || file.length() <= 0L) throw IOException("O arquivo ODT está vazio ou ausente.")
        if (file.length() > MAX_PACKAGE_BYTES) throw IOException("O arquivo ODT excede o limite de leitura direta.")

        return try {
            ZipFile(file).use { zip ->
                val mimeEntry = zip.getEntry(MIME_ENTRY)?.takeUnless { it.isDirectory }
                if (mimeEntry != null &&
                    (mimeEntry.size > MAX_MIME_ENTRY_BYTES || zip.getInputStream(mimeEntry).use(::readMimeEntry) != CONTENT_TYPE)
                ) {
                    throw IOException("O pacote não é um documento OpenDocument Text válido.")
                }
                val content = zip.getEntry(CONTENT_ENTRY)
                    ?.takeUnless { it.isDirectory }
                    ?: throw IOException("O arquivo ODT não contém o documento principal.")
                if (content.size > MAX_CONTENT_XML_BYTES) {
                    throw IOException("O XML principal do ODT excede o limite de leitura.")
                }
                zip.getInputStream(content).use { input -> parseContentXml(BoundedXmlInputStream(input)) }
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("Não foi possível ler o documento ODT.", failure)
        }
    }

    private fun parseContentXml(input: InputStream): String {
        val parser = XmlPullParserFactory.newInstance().apply { isNamespaceAware = true }.newPullParser()
        parser.setFeature(XmlPullParser.FEATURE_PROCESS_DOCDECL, false)
        parser.setInput(input, null)

        val text = StringBuilder()
        var documentRootSeen = false
        var bodyDepth = 0
        var officeTextDepth = 0
        var paragraphDepth = 0

        try {
            while (true) {
                when (parser.nextToken()) {
                    XmlPullParser.START_TAG -> {
                        val namespace = parser.namespace.orEmpty()
                        val name = parser.name
                        if (namespace == OFFICE_NAMESPACE && name == "document-content" && bodyDepth == 0) {
                            documentRootSeen = true
                        }
                        if (namespace == OFFICE_NAMESPACE && name == "body" && documentRootSeen) bodyDepth++
                        if (namespace == OFFICE_NAMESPACE && name == "text" && bodyDepth > 0) officeTextDepth++
                        if (officeTextDepth > 0 && namespace == TEXT_NAMESPACE && name in setOf("p", "h")) {
                            appendLineBreak(text)
                            paragraphDepth++
                        }
                        if (paragraphDepth > 0 && namespace == TEXT_NAMESPACE) {
                            when (name) {
                                "s" -> appendSpaces(text, parser.getAttributeValue(TEXT_NAMESPACE, "c"))
                                "tab" -> append(text, "\t")
                                "line-break" -> appendLineBreak(text)
                            }
                        }
                    }
                    XmlPullParser.TEXT, XmlPullParser.CDSECT -> {
                        if (officeTextDepth > 0 && paragraphDepth > 0) append(text, parser.text.orEmpty())
                    }
                    XmlPullParser.ENTITY_REF -> {
                        if (officeTextDepth > 0 && paragraphDepth > 0) append(text, resolveEntityReference(parser))
                    }
                    XmlPullParser.END_TAG -> {
                        val namespace = parser.namespace.orEmpty()
                        val name = parser.name
                        if (namespace == TEXT_NAMESPACE && name in setOf("p", "h") && paragraphDepth > 0) {
                            appendLineBreak(text)
                            paragraphDepth--
                        }
                        if (officeTextDepth > 0 && namespace == TABLE_NAMESPACE && name == "table-cell") {
                            trimTrailingLineBreak(text)
                            append(text, "\t")
                        }
                        if (officeTextDepth > 0 && namespace == TABLE_NAMESPACE && name == "table-row") {
                            if (text.isNotEmpty() && text.last() == '\t') text.setLength(text.length - 1)
                            appendLineBreak(text)
                        }
                        if (namespace == OFFICE_NAMESPACE && name == "text" && officeTextDepth > 0) officeTextDepth--
                        if (namespace == OFFICE_NAMESPACE && name == "body" && bodyDepth > 0) bodyDepth--
                    }
                    XmlPullParser.DOCDECL -> throw IOException("O documento ODT contém uma declaração XML não permitida.")
                    XmlPullParser.END_DOCUMENT -> break
                }
            }
        } catch (failure: IOException) {
            throw failure
        } catch (failure: Exception) {
            throw IOException("O XML principal do ODT é inválido.", failure)
        }

        if (!documentRootSeen || bodyDepth != 0 || officeTextDepth != 0) {
            throw IOException("O ODT não contém uma estrutura de documento válida.")
        }
        return text.toString().trim()
    }

    private fun appendSpaces(target: StringBuilder, countValue: String?) {
        val count = countValue?.toLongOrNull() ?: 1L
        if (count < 1L || target.length.toLong() + count > MAX_TEXT_CHARACTERS) {
            throw IOException("O texto extraído do ODT excede o limite de leitura.")
        }
        repeat(count.toInt()) { target.append(' ') }
    }

    private fun append(target: StringBuilder, value: String) {
        if (target.length.toLong() + value.length > MAX_TEXT_CHARACTERS) {
            throw IOException("O texto extraído do ODT excede o limite de leitura.")
        }
        target.append(value)
    }

    private fun appendLineBreak(target: StringBuilder) {
        if (target.isNotEmpty() && target.last() != '\n' && target.last() != '\t') append(target, "\n")
    }

    private fun trimTrailingLineBreak(target: StringBuilder) {
        while (target.isNotEmpty() && target.last() == '\n') target.setLength(target.length - 1)
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
            throw IOException("O texto do ODT contém uma referência numérica inválida.")
        }
        return when (name) {
            "amp" -> "&"
            "apos" -> "'"
            "gt" -> ">"
            "lt" -> "<"
            "quot" -> "\""
            else -> throw IOException("O texto do ODT contém uma entidade XML não suportada.")
        }
    }

    private fun readMimeEntry(input: InputStream): String? {
        val bytes = ByteArrayOutputStream(MAX_MIME_ENTRY_BYTES)
        val buffer = ByteArray(32)
        while (bytes.size() <= MAX_MIME_ENTRY_BYTES) {
            val read = input.read(buffer, 0, minOf(buffer.size, MAX_MIME_ENTRY_BYTES + 1 - bytes.size()))
            if (read < 0) break
            bytes.write(buffer, 0, read)
        }
        if (bytes.size() > MAX_MIME_ENTRY_BYTES) return null
        return bytes.toByteArray().toString(StandardCharsets.US_ASCII)
    }

    private fun unsignedShort(bytes: ByteArray, offset: Int): Int =
        (bytes[offset].toInt() and 0xFF) or ((bytes[offset + 1].toInt() and 0xFF) shl 8)

    private class BoundedXmlInputStream(input: InputStream) : FilterInputStream(input) {
        private var bytesRead = 0L

        override fun read(): Int {
            if (bytesRead >= MAX_CONTENT_XML_BYTES) {
                if (super.read() < 0) return -1
                throw IOException("O XML principal do ODT excede o limite de leitura.")
            }
            return super.read().also { if (it >= 0) bytesRead++ }
        }

        override fun read(buffer: ByteArray, offset: Int, length: Int): Int {
            if (length == 0) return 0
            val remaining = MAX_CONTENT_XML_BYTES - bytesRead
            if (remaining <= 0L) {
                if (super.read() < 0) return -1
                throw IOException("O XML principal do ODT excede o limite de leitura.")
            }
            val count = super.read(buffer, offset, minOf(length.toLong(), remaining).toInt())
            if (count > 0) bytesRead += count
            return count
        }
    }
}
