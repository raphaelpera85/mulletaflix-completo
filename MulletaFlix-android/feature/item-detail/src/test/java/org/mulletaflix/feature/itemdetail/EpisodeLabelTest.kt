package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class EpisodeLabelTest {

    @Test
    fun `formats season and episode numbers with zero padding`() {
        assertEquals("1x01 Piloto", episodeLabel(1, 1, "Piloto"))
        assertEquals("12x07 Season Finale", episodeLabel(12, 7, "Season Finale"))
    }

    @Test
    fun `falls back to the episode name when numbers are missing`() {
        assertEquals("A Casa do Dragão - S01E07", episodeLabel(null, null, "A Casa do Dragão - S01E07"))
        assertEquals("Extra: Making Of", episodeLabel(null, 5, "Extra: Making Of"))
        assertEquals("Extra: Making Of", episodeLabel(1, null, "Extra: Making Of"))
    }
}
