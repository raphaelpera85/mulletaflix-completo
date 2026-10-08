package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import org.readium.r2.shared.publication.Href
import org.readium.r2.shared.publication.Link
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [35])
class BookReaderContentsTest {
    @Test
    fun flattenBookReaderContentsKeepsNestedOrderAndUsesFallbackForBlankTitles() {
        val publicationContents = listOf(
                Link(
                    href = href("part-one.xhtml"),
                    title = "Parte I",
                    children = listOf(
                        Link(href = href("chapter-one.xhtml"), title = " "),
                        Link(href = href("chapter-two.xhtml"), title = "Capítulo 2"),
                    ),
                ),
            Link(href = href("part-two.xhtml"), title = "Parte II"),
        )

        val entries = flattenBookReaderContents(publicationContents)

        assertEquals(
            listOf("Parte I", "Seção 2", "Capítulo 2", "Parte II"),
            entries.map(BookReaderContentsEntry::title),
        )
        assertEquals(listOf(0, 1, 1, 0), entries.map(BookReaderContentsEntry::depth))
        assertEquals(listOf(true, true, true, true), entries.map(BookReaderContentsEntry::isNavigable))
        assertEquals(
            listOf(
                "https://book.test/OPS/part-one.xhtml",
                "https://book.test/OPS/chapter-one.xhtml",
                "https://book.test/OPS/chapter-two.xhtml",
                "https://book.test/OPS/part-two.xhtml",
            ),
            entries.map { requireNotNull(it.link).href.toString() },
        )
    }

    @Test
    fun flattenBookReaderContentsHandlesEmptyNavigation() {
        assertEquals(emptyList<BookReaderContentsEntry>(), flattenBookReaderContents(emptyList()))
    }

    @Test
    fun groupHeadingSentinelIsNotNavigableButLinkedParentWithChildrenRemainsNavigable() {
        val entries = flattenBookReaderContents(
            listOf(
                Link(
                    href = rawHref("#"),
                    title = "Grupo sem destino",
                    children = listOf(Link(href = href("chapter.xhtml"), title = "Capítulo")),
                ),
                Link(
                    href = href("linked-parent.xhtml"),
                    title = "Seção com destino",
                    children = listOf(Link(href = href("nested.xhtml"), title = "Subseção")),
                ),
            ),
        )

        assertEquals(listOf(false, true, true, true), entries.map(BookReaderContentsEntry::isNavigable))
    }

    private fun href(value: String): Href = requireNotNull(Href("https://book.test/OPS/$value"))

    private fun rawHref(value: String): Href = requireNotNull(Href(value))
}
