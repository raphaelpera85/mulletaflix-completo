package org.mulletaflix.feature.player

import org.junit.Assert.assertEquals
import org.junit.Test

class TrackLabelPolicyTest {
    @Test
    fun `audio label exposes codec channels and default marker`() {
        assertEquals(
            "Português (Brasil) • AAC • 5.1 • Padrão",
            trackLabel(
                TrackInfo(
                    index = 1,
                    displayName = "Português (Brasil)",
                    codec = "aac",
                    channels = 6,
                    isDefault = true,
                ),
            ),
        )
    }

    @Test
    fun `subtitle label exposes forced marker and ignores blank codec`() {
        assertEquals(
            "English • Forçada",
            trackLabel(
                TrackInfo(
                    index = 2,
                    displayName = "English",
                    codec = " ",
                    isForced = true,
                ),
            ),
        )
    }
}
