package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class BookSpeechRateTest {
    @Test
    fun `speech rate moves in quarter speed steps within accessible limits`() {
        assertEquals(100, BookSpeechRate.DEFAULT_PERCENT)
        assertEquals(125, BookSpeechRate.increase(100))
        assertEquals(1.25, BookSpeechRate.multiplier(125), 0.0)
        assertEquals(BookSpeechRate.MAX_PERCENT, BookSpeechRate.increase(BookSpeechRate.MAX_PERCENT))
        assertEquals(75, BookSpeechRate.decrease(100))
        assertEquals(BookSpeechRate.MIN_PERCENT, BookSpeechRate.decrease(BookSpeechRate.MIN_PERCENT))
        assertEquals(100, BookSpeechRate.normalize(112))
        assertEquals(BookSpeechRate.MIN_PERCENT, BookSpeechRate.normalize(0))
        assertEquals(BookSpeechRate.MAX_PERCENT, BookSpeechRate.normalize(500))
    }

    @Test
    fun `rate controls disable at minimum and maximum`() {
        assertFalse(BookSpeechRate.canDecrease(BookSpeechRate.MIN_PERCENT))
        assertTrue(BookSpeechRate.canIncrease(BookSpeechRate.MIN_PERCENT))
        assertTrue(BookSpeechRate.canDecrease(BookSpeechRate.MAX_PERCENT))
        assertFalse(BookSpeechRate.canIncrease(BookSpeechRate.MAX_PERCENT))
    }
}
