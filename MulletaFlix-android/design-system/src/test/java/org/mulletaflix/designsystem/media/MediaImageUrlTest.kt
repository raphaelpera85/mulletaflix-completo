package org.mulletaflix.designsystem.media

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class MediaImageUrlTest {
    @Test
    fun `resolves relative image path against selected server`() {
        assertEquals(
            "http://lan-server:8096/Items/item-1/Images/Primary?tag=abc&api_key=token",
            resolveMediaUrl("http://lan-server:8096/", "/Items/item-1/Images/Primary?tag=abc", "token"),
        )
    }

    @Test
    fun `encodes authentication token in image query`() {
        assertEquals(
            "http://server/Users/user/Images/Primary?api_key=token%2Bwith%2Fslash",
            resolveMediaUrl("http://server", "Users/user/Images/Primary", "token+with/slash"),
        )
    }

    @Test
    fun `does not modify absolute image URLs or blank paths`() {
        assertEquals("https://cdn.example/image.jpg", resolveMediaUrl("http://server", "https://cdn.example/image.jpg", "token"))
        assertNull(resolveMediaUrl("http://server", "", "token"))
    }
}
