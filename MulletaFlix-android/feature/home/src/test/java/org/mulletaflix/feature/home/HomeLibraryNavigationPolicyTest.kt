package org.mulletaflix.feature.home

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

class HomeLibraryNavigationPolicyTest {
    @Test
    fun `live tv library opens the dedicated live tv screen`() {
        val library = MediaItem("tv", "TV ao vivo", MediaItemType.CollectionFolder, collectionType = "livetv")

        assertTrue(shouldOpenLiveTv(library))
    }

    @Test
    fun `regular libraries keep the paginated library screen`() {
        val library = MediaItem("movies", "Filmes", MediaItemType.CollectionFolder, collectionType = "movies")

        assertFalse(shouldOpenLiveTv(library))
    }
}
