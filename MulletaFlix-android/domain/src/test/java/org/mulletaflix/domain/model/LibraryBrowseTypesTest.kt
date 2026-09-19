package org.mulletaflix.domain.model

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Test

class LibraryBrowseTypesTest {

    @Test
    fun `tv shows library browses series only`() {
        assertEquals("Series", LibraryBrowseTypes.forCollectionType("tvshows"))
    }

    @Test
    fun `collection type match is case and whitespace insensitive`() {
        assertEquals("Series", LibraryBrowseTypes.forCollectionType(" TVShows "))
        assertEquals("Movie", LibraryBrowseTypes.forCollectionType("Movies"))
    }

    @Test
    fun `movies library browses movies only`() {
        assertEquals("Movie", LibraryBrowseTypes.forCollectionType("movies"))
    }

    @Test
    fun `music library browses albums only`() {
        assertEquals("MusicAlbum", LibraryBrowseTypes.forCollectionType("music"))
    }

    @Test
    fun `books library browses books only`() {
        assertEquals("Book,Audiobook", LibraryBrowseTypes.forCollectionType("books"))
    }

    @Test
    fun `live tv library browses channels only`() {
        assertEquals("LiveTvChannel", LibraryBrowseTypes.forCollectionType("livetv"))
    }

    @Test
    fun `unknown library type never browses seasons episodes or tracks`() {
        listOf(null, "", "mixed", "unknown-type").forEach { collectionType ->
            val types = LibraryBrowseTypes.forCollectionType(collectionType)
            assertFalse("Season leaked for $collectionType", types.contains("Season"))
            assertFalse("Episode leaked for $collectionType", types.contains("Episode"))
            assertFalse("Audio leaked for $collectionType", types.contains("Audio,"))
            assertEquals(LibraryBrowseTypes.DEFAULT, types)
        }
    }

    @Test
    fun `no library type ever browses seasons or episodes`() {
        val collectionTypes = listOf(
            null, "", "movies", "tvshows", "music", "musicvideos", "books",
            "boxsets", "homevideos", "photos", "playlists", "livetv", "mixed",
        )
        collectionTypes.forEach { collectionType ->
            val types = LibraryBrowseTypes.forCollectionType(collectionType).split(",")
            assertFalse(types.contains("Season"))
            assertFalse(types.contains("Episode"))
            assertFalse(types.contains("Audio"))
        }
    }
}
