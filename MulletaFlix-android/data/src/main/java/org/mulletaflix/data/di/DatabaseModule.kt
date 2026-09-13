package org.mulletaflix.data.di

import android.content.Context
import androidx.room.Room
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import org.mulletaflix.data.db.MediaItemDao
import org.mulletaflix.data.db.MulletaFlixDatabase
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideDatabase(@ApplicationContext context: Context): MulletaFlixDatabase =
        Room.databaseBuilder(
            context,
            MulletaFlixDatabase::class.java,
            "mulletaflix.db"
        ).fallbackToDestructiveMigration()
         .build()

    @Provides
    @Singleton
    fun provideMediaItemDao(database: MulletaFlixDatabase): MediaItemDao =
        database.mediaItemDao()
}
