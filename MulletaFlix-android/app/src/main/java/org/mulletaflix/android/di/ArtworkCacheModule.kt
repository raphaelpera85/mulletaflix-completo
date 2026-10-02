package org.mulletaflix.android.di

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton
import org.mulletaflix.android.CoilArtworkCacheCleaner
import org.mulletaflix.core.common.cache.ArtworkCacheCleaner

@Module
@InstallIn(SingletonComponent::class)
abstract class ArtworkCacheModule {
    @Binds
    @Singleton
    abstract fun bindArtworkCacheCleaner(impl: CoilArtworkCacheCleaner): ArtworkCacheCleaner
}
