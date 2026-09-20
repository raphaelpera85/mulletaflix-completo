package org.mulletaflix.feature.downloads

import androidx.compose.ui.layout.ContentScale
import org.junit.Assert.assertEquals
import org.junit.Test

class OfflineArtworkPolicyTest {
    @Test
    fun `offline posters preserve the complete artwork`() {
        assertEquals(ContentScale.Fit, offlineArtworkContentScale())
    }
}
