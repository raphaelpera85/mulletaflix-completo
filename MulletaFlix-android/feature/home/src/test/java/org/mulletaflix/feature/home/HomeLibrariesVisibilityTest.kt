package org.mulletaflix.feature.home

import org.junit.Assert.assertEquals
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class HomeLibrariesVisibilityTest {
    private val movies = MediaItem("movies", "Filmes", MediaItemType.Folder, collectionType = "movies")
    private val books = MediaItem("books", "Minha coleção", MediaItemType.Folder, collectionType = "books")
    private val spacedBooks = MediaItem("spaced-books", "Biblioteca personalizada", MediaItemType.Folder, collectionType = " books ")
    private val legacyBooksName = MediaItem("legacy-books", "Livros", MediaItemType.Folder)

    @Test
    fun `tv hides books library by collection type and display name`() {
        assertEquals(listOf(movies), homeLibrariesForDevice(listOf(movies, books, legacyBooksName), isTelevision = true))
    }

    @Test
    fun `phone and tablet keep books libraries available`() {
        val libraries = listOf(movies, books, legacyBooksName)
        assertEquals(libraries, homeLibrariesForDevice(libraries, isTelevision = false))
    }

    @Test
    fun `tv hides books recently added row even when library has custom name`() {
        assertEquals(false, shouldShowRecentlyAddedLibrary("Minha coleção", listOf(books), isTelevision = true))
        assertEquals(false, shouldShowRecentlyAddedLibrary("Biblioteca personalizada", listOf(spacedBooks), isTelevision = true))
        assertEquals(true, shouldShowRecentlyAddedLibrary("Filmes", listOf(movies), isTelevision = true))
    }

    @Test
    fun `phone and tablet keep recently added books row`() {
        assertEquals(true, shouldShowRecentlyAddedLibrary("Minha coleção", listOf(books), isTelevision = false))
    }

    @Test
    fun `tv hides book libraries with whitespace around collection type`() {
        assertEquals(listOf(movies), homeLibrariesForDevice(listOf(movies, spacedBooks), isTelevision = true))
    }
}
