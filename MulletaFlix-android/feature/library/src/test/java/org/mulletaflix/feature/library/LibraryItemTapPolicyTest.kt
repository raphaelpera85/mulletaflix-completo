package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test

class LibraryItemTapPolicyTest {
    @Test
    fun `offline cards show a local preview instead of a network-only details screen`() {
        assertEquals(LibraryItemTapAction.ShowOfflinePreview, libraryItemTapAction(isOffline = true))
    }

    @Test
    fun `online cards keep the normal details navigation`() {
        assertEquals(LibraryItemTapAction.OpenDetails, libraryItemTapAction(isOffline = false))
    }
}
