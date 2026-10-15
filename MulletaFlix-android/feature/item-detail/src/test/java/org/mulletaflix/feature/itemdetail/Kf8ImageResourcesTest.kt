package org.mulletaflix.feature.itemdetail

import android.util.Base64
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class Kf8ImageResourcesTest {
    @Test
    fun `adjusts hybrid KF8 first image index from selected header record`() {
        assertEquals(24, Kf8ImageResources.firstImageRecord(20, 4, 6, 10, 40))
    }

    @Test
    fun `falls back to first record after KF8 text for null or invalid image index`() {
        assertEquals(16, Kf8ImageResources.firstImageRecord(0xFFFFFFFFL, 4, 6, 10, 40))
        assertEquals(16, Kf8ImageResources.firstImageRecord(1, 4, 6, 10, 40))
    }

    @Test
    fun `uses legacy text end when hybrid MOBI image index is null`() {
        assertEquals(
            listOf(3..4),
            Kf8ImageResources.precedingResourceRanges(0xFFFFFFFFL, 2, 5, 10),
        )
    }

    @Test
    fun `rejects raster data with unsafe decoded dimensions`() {
        val oversized = Base64.decode(ONE_PIXEL_PNG, Base64.NO_WRAP).apply {
            writeInt(16, 9_000)
            writeInt(20, 9_000)
        }
        val source = "data:image/png;base64,${Base64.encodeToString(oversized, Base64.NO_WRAP)}"

        assertEquals(null, EmbeddedBookImageDecoder.decodeDataUri(source, "oversized"))
    }

    @Test
    fun `resolves local KF8 raster resource references into self-contained image data`() {
        val png = Base64.decode(ONE_PIXEL_PNG, Base64.NO_WRAP)
        val html = "<p>Before</p><img src='kindle:embed:1?mime=image/png' alt='Diagram'><p>After</p>"

        val resolved = Kf8ImageResources.inlineReferences(
            html = html,
            firstImageRecord = 7,
            recordCount = 8,
            readRecord = { record ->
                assertEquals(7, record)
                png
            },
        )

        assertTrue(resolved.contains("src='data:image/png;base64,$ONE_PIXEL_PNG'"))
        assertTrue(resolved.contains("alt='Diagram'"))
        assertTrue(resolved.contains("<p>Before</p>"))
        assertTrue(resolved.contains("<p>After</p>"))
    }

    @Test
    fun `resolves KF8 image resources stored in an image CONT and CRES pair`() {
        val png = Base64.decode(ONE_PIXEL_PNG, Base64.NO_WRAP)
        val container = imageContainerHeader()
        val compressedResource = ByteArray(12) { 0 }.also {
            "CRES".toByteArray().copyInto(it)
        } + png

        val resolved = Kf8ImageResources.inlineReferences(
            html = "<img src=\"kindle:embed:2?mime=image/png\">",
            firstImageRecord = 3,
            recordCount = 5,
            readRecord = { record -> if (record == 3) container else compressedResource },
        )

        assertTrue(resolved.contains("data:image/png;base64,$ONE_PIXEL_PNG"))
    }

    @Test
    fun `drops invalid and out of range embedded references without following external URLs`() {
        var recordsRead = 0
        val html = "<img src='kindle:embed:V?mime=image/png'><img src='https://example.test/a.png'>"

        val resolved = Kf8ImageResources.inlineReferences(
            html = html,
            firstImageRecord = 4,
            recordCount = 5,
            readRecord = {
                recordsRead++
                byteArrayOf(1, 2, 3)
            },
        )

        assertEquals(0, recordsRead)
        assertFalse(resolved.contains("kindle:embed:"))
        assertTrue(resolved.contains("https://example.test/a.png"))
    }

    @Test
    fun `maps KF8 image identifiers across the preceding MOBI resource range`() {
        val png = Base64.decode(ONE_PIXEL_PNG, Base64.NO_WRAP)
        val recordsRead = mutableListOf<Int>()
        val html = "<img src='kindle:embed:1'><img src='kindle:embed:2'><img src='kindle:embed:3'>"

        Kf8ImageResources.inlineReferences(
            html = html,
            firstImageRecord = 20,
            recordCount = 23,
            readRecord = { record -> recordsRead += record; png },
            precedingResourceRanges = listOf(10..11),
        )

        assertEquals(listOf(10, 10, 11, 20), recordsRead)
    }

    @Test
    fun `caps unique embedded image bytes before expanding repeated data URIs`() {
        val png = Base64.decode(ONE_PIXEL_PNG, Base64.NO_WRAP)
        val recordsRead = mutableListOf<Int>()

        val resolved = Kf8ImageResources.inlineReferences(
            html = "<img src='kindle:embed:1'><img src='kindle:embed:2'>",
            firstImageRecord = 5,
            recordCount = 7,
            readRecord = { record -> recordsRead += record; png },
            maxTotalImageBytes = png.size,
        )

        assertEquals(listOf(5, 5, 6), recordsRead)
        assertEquals(1, Regex("data:image/png;base64,").findAll(resolved).count())
    }

    @Test
    fun `caps repeated data URI expansion independently of unique decoded image bytes`() {
        val png = Base64.decode(ONE_PIXEL_PNG, Base64.NO_WRAP)
        val oneImageHtml = Kf8ImageResources.inlineReferences(
            html = "<img src='kindle:embed:1'>",
            firstImageRecord = 5,
            recordCount = 6,
            readRecord = { png },
        )
        val singleUriLength = Regex("data:image/png;base64,[^']+").find(oneImageHtml)!!.value.length

        val repeated = Kf8ImageResources.inlineReferences(
            html = "<img src='kindle:embed:1'><img src='kindle:embed:1'>",
            firstImageRecord = 5,
            recordCount = 6,
            readRecord = { png },
            maxTotalExpandedImageChars = singleUriLength,
        )

        assertEquals(1, Regex("data:image/png;base64,").findAll(repeated).count())
    }

    private fun imageContainerHeader(): ByteArray {
        val type = "application/image".toByteArray()
        return ByteArray(60 + 8 + 8 + type.size).apply {
            "CONT".toByteArray().copyInto(this)
            "EXTH".toByteArray().copyInto(this, 48)
            writeInt(52, 8 + 8 + type.size)
            writeInt(56, 1)
            writeInt(60, 539)
            writeInt(64, 8 + type.size)
            type.copyInto(this, 68)
        }
    }

    private fun ByteArray.writeInt(offset: Int, value: Int) {
        this[offset] = (value ushr 24).toByte()
        this[offset + 1] = (value ushr 16).toByte()
        this[offset + 2] = (value ushr 8).toByte()
        this[offset + 3] = value.toByte()
    }

    private companion object {
        const val ONE_PIXEL_PNG = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/F5sAAAAASUVORK5CYII="
    }
}
