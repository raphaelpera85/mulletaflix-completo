package org.mulletaflix.core.testing

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.model.MediaItemType

object TestMediaData {
    val sampleMovie = MediaItem(
        id = "test-movie-1",
        name = "MulletaFlix Movie Test",
        type = MediaItemType.Movie,
        overview = "Filme de teste para verificação unitária e de UI.",
        year = 2026,
        has4K = true,
        hasHD = true,
    )
}
