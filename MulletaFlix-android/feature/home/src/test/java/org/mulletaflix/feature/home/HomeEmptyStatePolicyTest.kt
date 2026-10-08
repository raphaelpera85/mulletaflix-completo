package org.mulletaflix.feature.home

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class HomeEmptyStatePolicyTest {
    @Test
    fun `empty message appears only when all feed sections are truly empty`() {
        assertTrue(shouldShowEmptyHomeState(HomeState(isLoading = false)))
        assertFalse(
            shouldShowEmptyHomeState(
                HomeState(
                    isLoading = false,
                    favoriteItems = listOf(MediaItem("favorite", "Favorito", MediaItemType.Movie)),
                ),
            ),
        )
        assertFalse(shouldShowEmptyHomeState(HomeState(isLoading = false, resumeError = "Falha")))
    }

    @Test
    fun `tv empty state ignores hidden books libraries and recent items`() {
        val books = MediaItem(
            id = "books",
            name = "Leitura",
            type = MediaItemType.CollectionFolder,
            collectionType = "books",
        )
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)
        val state = HomeState(
            isLoading = false,
            libraries = listOf(books),
            recentlyAddedByLibrary = mapOf(books.id to listOf(book)),
        )

        assertTrue(shouldShowEmptyHomeState(state, isTelevision = true))
        assertFalse(shouldShowEmptyHomeState(state, isTelevision = false))
    }

    @Test
    fun `tv empty state ignores errors for hidden books libraries`() {
        val books = MediaItem(
            id = "books",
            name = "Leitura",
            type = MediaItemType.CollectionFolder,
            collectionType = "books",
        )
        val state = HomeState(
            isLoading = false,
            libraries = listOf(books),
            recentlyAddedErrorsByLibrary = mapOf(books.id to "Erro ao carregar livros"),
        )

        assertTrue(shouldShowEmptyHomeState(state, isTelevision = true))
        assertFalse(shouldShowEmptyHomeState(state, isTelevision = false))
    }

    @Test
    fun `tv treats home containing only book titles as empty`() {
        val book = MediaItem("book-1", "Livro", MediaItemType.Book)
        val audiobook = MediaItem("audiobook-1", "Audiolivro", MediaItemType.AudioBook)
        val state = HomeState(
            isLoading = false,
            heroItem = book,
            resumeItems = listOf(book, audiobook),
            nextUpItems = listOf(book, audiobook),
            favoriteItems = listOf(book, audiobook),
        )

        assertTrue(shouldShowEmptyHomeState(state, isTelevision = true))
        assertFalse(shouldShowEmptyHomeState(state, isTelevision = false))
    }

}
