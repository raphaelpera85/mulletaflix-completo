package org.mulletaflix.feature.itemdetail

import java.io.File
import java.io.IOException
import java.nio.charset.StandardCharsets
import java.nio.file.Files
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class MobiBookTextExtractorTest {
    @Test
    fun `reads uncompressed MOBI HTML and recognizes generic database signature`() = withTempDirectory { directory ->
        val source = "<h1>Capítulo</h1><p>Olá MOBI.</p>".toByteArray(StandardCharsets.UTF_8)
        val file = directory.resolve("book.bin").apply { writeBytes(mobiFile(source, source.size, encoding = 65_001)) }

        assertTrue(MobiBookTextExtractor.hasMobiDatabase(file))
        assertEquals(BookPayloadFormat.PLAIN_TEXT, detectBookPayloadFormat(file, "application/octet-stream"))
        assertEquals("Capítulo\n\nOlá MOBI.", PlainTextBookDocument.open(file, "application/octet-stream").chunks.joinToString(""))
    }

    @Test
    fun `reads PalmDOC compressed content using Windows 1252 encoding`() = withTempDirectory { directory ->
        val source = "<p>Ação – café.</p>".toByteArray(CharsetForTest.WINDOWS_1252)
        val compressed = palmDocLiteral(source)
        val file = directory.resolve("book.mobi").apply {
            writeBytes(mobiFile(compressed, source.size, compression = 2, encoding = 1_252))
        }

        val document = PlainTextBookDocument.open(file, MobiBookTextExtractor.CONTENT_TYPE)
        assertEquals("Ação – café.", document.chunks.joinToString(""))
    }

    @Test
    fun `expands overlapping PalmDOC back references`() = withTempDirectory { directory ->
        val source = "<p>AAAA</p>".toByteArray(StandardCharsets.US_ASCII)
        val compressed = byteArrayOf(
            4, '<'.code.toByte(), 'p'.code.toByte(), '>'.code.toByte(), 'A'.code.toByte(),
            0x80.toByte(), 0x08,
            4, '<'.code.toByte(), '/'.code.toByte(), 'p'.code.toByte(), '>'.code.toByte(),
        )
        val file = directory.resolve("repeated.mobi").apply {
            writeBytes(mobiFile(compressed, source.size, compression = 2))
        }

        assertEquals("AAAA", PlainTextBookDocument.open(file, MobiBookTextExtractor.CONTENT_TYPE).chunks.joinToString(""))
    }

    @Test
    fun `reads multiple MOBI text records with UTF-8 overlap and trailing markers`() = withTempDirectory { directory ->
        val source = "<p>café ☕</p>".toByteArray(StandardCharsets.UTF_8)
        val splitAfterFirstByteOfEAcute = source.indexOf(0xC3.toByte()) + 1
        val firstText = source.copyOfRange(0, splitAfterFirstByteOfEAcute)
        val secondText = source.copyOfRange(splitAfterFirstByteOfEAcute, source.size)
        val indexingTrailer = ByteArray(128) { 0x42 } + byteArrayOf(0x81.toByte(), 0x02)
        val firstRecord = palmDocLiteral(firstText) + byteArrayOf(0xA9.toByte(), 0x01) + indexingTrailer
        val secondRecord = palmDocLiteral(secondText) + byteArrayOf(0x00) + indexingTrailer
        val file = directory.resolve("overlap.mobi").apply {
            writeBytes(mobiFile(listOf(firstRecord, secondRecord), source.size, compression = 2, extraDataFlags = 3))
        }

        val document = PlainTextBookDocument.open(file, MobiBookTextExtractor.CONTENT_TYPE)
        assertEquals("café ☕", document.chunks.joinToString(""))
    }

    @Test
    fun `rejects DRM HUFF CDIC malformed records and incorrect declared text size`() = withTempDirectory { directory ->
        val html = "<p>texto</p>".toByteArray(StandardCharsets.UTF_8)
        val drm = directory.resolve("drm.mobi").apply {
            writeBytes(mobiFile(html, html.size, encryption = 1))
        }
        val huff = directory.resolve("huff.mobi").apply {
            writeBytes(mobiFile(html, html.size, compression = 17_480))
        }
        val lengthMismatch = directory.resolve("length.mobi").apply {
            writeBytes(mobiFile(html, html.size + 1))
        }
        val malformed = directory.resolve("malformed.mobi").apply { writeBytes(byteArrayOf(1, 2, 3)) }
        val malformedTrailer = directory.resolve("bad-trailer.mobi").apply {
            writeBytes(mobiFile(listOf(html), html.size, extraDataFlags = 2))
        }

        listOf(drm, huff, lengthMismatch, malformed, malformedTrailer).forEach { file ->
            assertThrows(IOException::class.java) { MobiBookTextExtractor.extractHtml(file) }
        }
        assertFalse(MobiBookTextExtractor.hasMobiDatabase(malformed))
    }

    private fun mobiFile(
        textRecord: ByteArray,
        declaredLength: Int,
        compression: Int = 1,
        encryption: Int = 0,
        encoding: Long = 65_001,
    ): ByteArray = mobiFile(listOf(textRecord), declaredLength, compression, encryption, encoding)

    private fun mobiFile(
        textRecords: List<ByteArray>,
        declaredLength: Int,
        compression: Int = 1,
        encryption: Int = 0,
        encoding: Long = 65_001,
        extraDataFlags: Int = 0,
    ): ByteArray {
        val headerSize = 248
        val recordCount = textRecords.size + 1
        val firstRecordOffset = 78 + recordCount * 8
        val textRecordOffsets = textRecords.indices.map { index ->
            firstRecordOffset + headerSize + textRecords.take(index).sumOf { it.size }
        }
        val bytes = ByteArray(firstRecordOffset + headerSize + textRecords.sumOf { it.size })
        putAscii(bytes, 0, "Test book")
        putAscii(bytes, 60, "BOOK")
        putAscii(bytes, 64, "MOBI")
        putShort(bytes, 76, recordCount)
        putInt(bytes, 78, firstRecordOffset)
        textRecordOffsets.forEachIndexed { index, offset ->
            putInt(bytes, 78 + (index + 1) * 8, offset)
        }

        putShort(bytes, firstRecordOffset, compression)
        putInt(bytes, firstRecordOffset + 4, declaredLength)
        putShort(bytes, firstRecordOffset + 8, textRecords.size)
        putShort(bytes, firstRecordOffset + 10, 4_096)
        putShort(bytes, firstRecordOffset + 12, encryption)
        putAscii(bytes, firstRecordOffset + 16, "MOBI")
        putInt(bytes, firstRecordOffset + 20, 232)
        putInt(bytes, firstRecordOffset + 24, 2)
        putInt(bytes, firstRecordOffset + 28, encoding.toInt())
        putShort(bytes, firstRecordOffset + 242, extraDataFlags)
        textRecords.forEachIndexed { index, record -> record.copyInto(bytes, textRecordOffsets[index]) }
        return bytes
    }

    private fun palmDocLiteral(bytes: ByteArray): ByteArray {
        val output = ArrayList<Byte>()
        var offset = 0
        while (offset < bytes.size) {
            val size = minOf(8, bytes.size - offset)
            output += size.toByte()
            repeat(size) { output += bytes[offset++] }
        }
        return output.toByteArray()
    }

    private fun withTempDirectory(block: (File) -> Unit) {
        val directory = Files.createTempDirectory("mobi-reader-test").toFile()
        try {
            block(directory)
        } finally {
            directory.deleteRecursively()
        }
    }

    private fun putAscii(bytes: ByteArray, offset: Int, value: String) {
        value.toByteArray(StandardCharsets.US_ASCII).copyInto(bytes, offset)
    }

    private fun putShort(bytes: ByteArray, offset: Int, value: Int) {
        bytes[offset] = (value ushr 8).toByte()
        bytes[offset + 1] = value.toByte()
    }

    private fun putInt(bytes: ByteArray, offset: Int, value: Int) {
        bytes[offset] = (value ushr 24).toByte()
        bytes[offset + 1] = (value ushr 16).toByte()
        bytes[offset + 2] = (value ushr 8).toByte()
        bytes[offset + 3] = value.toByte()
    }

    private object CharsetForTest {
        val WINDOWS_1252 = java.nio.charset.Charset.forName("windows-1252")
    }
}
