package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test

class LibraryItemTapPolicyTest {
    @Test
    fun `offline cards do not navigate into a network-only details screen`() {
        assertEquals(LibraryItemTapAction.ExplainOffline, libraryItemTapAction(isOffline = true))
    }

    @Test
    fun `online cards keep the normal details navigation`() {
        assertEquals(LibraryItemTapAction.OpenDetails, libraryItemTapAction(isOffline = false))
    }
}
