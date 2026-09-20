package org.mulletaflix.feature.library

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.designsystem.components.MediaCardShape
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class LibraryPresentationTest {
    @Test
    fun `poster media keeps the complete vertical cover shape`() {
        assertEquals(
            MediaCardShape.Portrait,
            libraryCardShape(MediaItem("movie", "Filme", MediaItemType.Movie)),
        )
        assertEquals(
            MediaCardShape.Portrait,
            libraryCardShape(MediaItem("series", "Série", MediaItemType.Series)),
        )
    }

    @Test
    fun `episodic and live media use the complete horizontal artwork shape`() {
        assertEquals(
            MediaCardShape.Landscape,
            libraryCardShape(MediaItem("episode", "Episódio", MediaItemType.Episode)),
        )
        assertEquals(
            MediaCardShape.Landscape,
            libraryCardShape(MediaItem("channel", "Canal", MediaItemType.LiveTvChannel)),
        )
    }
}
