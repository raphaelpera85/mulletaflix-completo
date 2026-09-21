package org.mulletaflix.feature.search

import org.junit.Assert.assertEquals
import org.junit.Test

class SearchLayoutPolicyTest {
    @Test
    fun phoneUsesAvailableWidth() {
        assertEquals(390, searchContentMaxWidthDp(390, isTelevision = false))
    }

    @Test
    fun tabletUsesCenteredReadableWidth() {
        assertEquals(1000, searchContentMaxWidthDp(1280, isTelevision = false))
    }

    @Test
    fun televisionUsesWiderContentWidth() {
        assertEquals(1280, searchContentMaxWidthDp(1920, isTelevision = true))
    }
}
