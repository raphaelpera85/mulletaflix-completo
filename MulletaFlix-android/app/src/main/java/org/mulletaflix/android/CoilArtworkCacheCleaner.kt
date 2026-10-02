package org.mulletaflix.android

import android.content.Context
import dagger.hilt.android.qualifiers.ApplicationContext
import javax.inject.Inject
import javax.inject.Singleton
import org.mulletaflix.core.common.cache.ArtworkCacheCleaner

@Singleton
class CoilArtworkCacheCleaner @Inject constructor(
    @param:ApplicationContext private val context: Context,
) : ArtworkCacheCleaner {
    override suspend fun clear() {
        ImageCacheCleanup.clear(context)
    }
}
