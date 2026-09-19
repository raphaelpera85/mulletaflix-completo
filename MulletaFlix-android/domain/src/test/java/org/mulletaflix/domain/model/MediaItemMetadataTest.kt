package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class MediaItemMetadataTest {

    @Test
    fun `movie keeps a single production year`() {
        val item = MediaItem("movie", "Filme", MediaItemType.Movie, year = 2022)

        assertEquals("2022", item.displayYearRange())
    }

    @Test
    fun `series without end date is marked as ongoing`() {
        val item = MediaItem(
            "series",
            "Série",
            MediaItemType.Series,
            premiereDate = "2022-09-01T00:00:00Z",
        )

        assertEquals("2022 - Presente", item.displayYearRange())
    }

    @Test
    fun `series with a different end year exposes the full range`() {
        val item = MediaItem(
            "series",
            "Série",
            MediaItemType.Series,
            year = 2022,
            endDate = "2024-05-20T00:00:00Z",
        )

        assertEquals("2022 - 2024", item.displayYearRange())
    }

    @Test
    fun `missing dates do not render an empty metadata label`() {
        val item = MediaItem("unknown", "Sem ano", MediaItemType.Series)

        assertNull(item.displayYearRange())
    }

    @Test
    fun `unplayed count is kept on the domain item`() {
        val item = MediaItem("series", "Série", MediaItemType.Series, unplayedItemCount = 3)

        assertEquals(3, item.unplayedItemCount)
    }
}
