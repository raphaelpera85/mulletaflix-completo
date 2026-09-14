package org.mulletaflix.android.di

import androidx.media3.common.util.UnstableApi
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import org.mulletaflix.android.service.Media3DownloadRepository
import org.mulletaflix.domain.repository.DownloadRepository
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DownloadModule {
    @Provides
    @Singleton
    @UnstableApi
    fun provideDownloadRepository(repository: Media3DownloadRepository): DownloadRepository = repository
}
