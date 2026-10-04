package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class BookReaderFontSizeTest {
    @Test
    fun defaultIsReadableAndChangesInTenPercentSteps() {
        assertEquals(100, BookReaderFontSize.DEFAULT_PERCENT)
        assertEquals(90, BookReaderFontSize.decrease(100))
        assertEquals(110, BookReaderFontSize.increase(100))
    }

    @Test
    fun changesStopAtSupportedMinimumAndMaximum() {
        assertEquals(BookReaderFontSize.MIN_PERCENT, BookReaderFontSize.decrease(BookReaderFontSize.MIN_PERCENT))
        assertEquals(BookReaderFontSize.MAX_PERCENT, BookReaderFontSize.increase(BookReaderFontSize.MAX_PERCENT))
    }

    @Test
    fun persistedValuesAreClampedAndRoundedToSupportedSteps() {
        assertEquals(BookReaderFontSize.MIN_PERCENT, BookReaderFontSize.normalize(0))
        assertEquals(100, BookReaderFontSize.normalize(96))
        assertEquals(BookReaderFontSize.MAX_PERCENT, BookReaderFontSize.normalize(500))
    }
}
