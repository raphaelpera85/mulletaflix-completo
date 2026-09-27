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
    fun `tv decides recent row visibility from the exact library rather than its display name`() {
        val sameNameMovieLibrary = movies.copy(id = "same-name-movies", name = "Coleção")
        val sameNameBookLibrary = books.copy(id = "same-name-books", name = "Coleção")

        assertEquals(true, shouldShowLibraryOnDevice(sameNameMovieLibrary, isTelevision = true))
        assertEquals(false, shouldShowLibraryOnDevice(sameNameBookLibrary, isTelevision = true))
        assertEquals(true, shouldShowLibraryOnDevice(sameNameBookLibrary, isTelevision = false))
    }

    @Test
    fun `tv hides recent rows and errors for books when another library shares its name`() {
        val sameNameMovieLibrary = movies.copy(id = "same-name-movies", name = "Coleção")
        val sameNameBookLibrary = books.copy(id = "same-name-books", name = "Coleção")
        val movie = MediaItem("movie-1", "Filme", MediaItemType.Movie)
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)
        val libraries = listOf(sameNameMovieLibrary, sameNameBookLibrary)
        val latestById = mapOf(sameNameMovieLibrary.id to listOf(movie), sameNameBookLibrary.id to listOf(book))
        val errorsById = mapOf(sameNameBookLibrary.id to "Falha ao carregar livros")

        val tvSections = homeRecentLibrarySections(libraries, latestById, errorsById, isTelevision = true)
        val phoneSections = homeRecentLibrarySections(libraries, latestById, errorsById, isTelevision = false)

        assertEquals(listOf(sameNameMovieLibrary), tvSections.map { it.library })
        assertEquals(listOf(movie), tvSections.single().items)
        assertEquals(listOf(sameNameMovieLibrary, sameNameBookLibrary), phoneSections.map { it.library })
        assertEquals("Falha ao carregar livros", phoneSections.last().errorMessage)
    }

    @Test
    fun `phone and tablet keep books libraries available`() {
        val libraries = listOf(movies, books, legacyBooksName)
        assertEquals(libraries, homeLibrariesForDevice(libraries, isTelevision = false))
    }

    @Test
    fun `tv hides book entries from home rows while handhelds retain them`() {
        val movie = MediaItem("movie-1", "Filme", MediaItemType.Movie)
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)
        val items = listOf(movie, book)

        assertEquals(listOf(movie), homeMediaItemsForDevice(items, isTelevision = true))
        assertEquals(items, homeMediaItemsForDevice(items, isTelevision = false))
    }

    @Test
    fun `tv hides book libraries with whitespace around collection type`() {
        assertEquals(listOf(movies), homeLibrariesForDevice(listOf(movies, spacedBooks), isTelevision = true))
    }
}
