package org.mulletaflix.core.common

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import org.mulletaflix.core.common.util.FormatUtils

class FormatUtilsTest {

    @Test
    fun `ticks to millis and millis to ticks roundtrip correctly`() {
        val millis = 1500L
        val ticks = FormatUtils.millisToTicks(millis)
        assertEquals(15_000_000L, ticks)
        assertEquals(millis, FormatUtils.ticksToMillis(ticks))
    }

    @Test
    fun `formatDuration formats hours minutes and seconds correctly`() {
        assertEquals("0:00", FormatUtils.formatDuration(0L))
        assertEquals("0:45", FormatUtils.formatDuration(45_000L))
        assertEquals("12:34", FormatUtils.formatDuration(754_000L))
        assertEquals("1:02:03", FormatUtils.formatDuration(3_723_000L))
    }

    @Test
    fun `formatRuntimeTicks formats human friendly runtime`() {
        assertNull(FormatUtils.formatRuntimeTicks(null))
        assertNull(FormatUtils.formatRuntimeTicks(0L))
        // 45 minutes = 45 * 60 * 10_000_000 = 27_000_000_000 ticks
        assertEquals("45 min", FormatUtils.formatRuntimeTicks(27_000_000_000L))
        // 2 hours = 120 minutes = 120 * 60 * 10_000_000 = 72_000_000_000 ticks
        assertEquals("2h", FormatUtils.formatRuntimeTicks(72_000_000_000L))
        // 2 hours 15 minutes = 135 minutes = 81_000_000_000 ticks
        assertEquals("2h 15min", FormatUtils.formatRuntimeTicks(81_000_000_000L))
    }

    @Test
    fun `formatFileSize formats sizes in byte units`() {
        assertEquals("0 B", FormatUtils.formatFileSize(0L))
        assertEquals("500.0 B", FormatUtils.formatFileSize(500L))
        assertEquals("1.0 KB", FormatUtils.formatFileSize(1024L))
        assertEquals("2.5 MB", FormatUtils.formatFileSize((2.5 * 1024 * 1024).toLong()))
        assertEquals("1.5 GB", FormatUtils.formatFileSize((1.5 * 1024 * 1024 * 1024).toLong()))
    }

    @Test
    fun `formatPercentage formats percentages safely`() {
        assertNull(FormatUtils.formatPercentage(null))
        assertNull(FormatUtils.formatPercentage(0.0))
        assertNull(FormatUtils.formatPercentage(-5.0))
        assertEquals("50%", FormatUtils.formatPercentage(50.4))
        assertEquals("100%", FormatUtils.formatPercentage(105.0))
    }
}
