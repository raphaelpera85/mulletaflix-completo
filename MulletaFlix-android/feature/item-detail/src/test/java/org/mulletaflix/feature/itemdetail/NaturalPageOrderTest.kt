package org.mulletaflix.feature.itemdetail

import org.junit.Assert.assertEquals
import org.junit.Test

class NaturalPageOrderTest {
    @Test
    fun `sorts numeric pages naturally and keeps leading zero and case tie rules`() {
        val names = listOf(
            "pages/page10.jpg",
            "pages/page02.jpg",
            "pages/page2.png",
            "pages/Page2.png",
            "pages/page0002.jpg",
            "pages/page1.jpg",
        )

        assertEquals(
            listOf(
                "pages/page1.jpg",
                "pages/Page2.png",
                "pages/page2.png",
                "pages/page02.jpg",
                "pages/page0002.jpg",
                "pages/page10.jpg",
            ),
            NaturalPageOrder.sort(names) { it },
        )
    }

    @Test
    fun `sorts very long numeric tokens without integer overflow`() {
        val names = listOf("page999999999999999999999.jpg", "page1000000000000000000000.jpg", "page2.jpg")

        assertEquals(
            listOf("page2.jpg", "page999999999999999999999.jpg", "page1000000000000000000000.jpg"),
            NaturalPageOrder.sort(names) { it },
        )
    }

    @Test
    fun `sorts archive records by selected page name`() {
        data class Page(val name: String, val id: Int)
        val pages = listOf(Page("10.png", 10), Page("2.png", 2), Page("1.png", 1))

        assertEquals(listOf(1, 2, 10), NaturalPageOrder.sort(pages) { it.name }.map { it.id })
    }
}
