package org.mulletaflix.data.db

import androidx.room.*
import kotlinx.coroutines.flow.Flow

@Entity(tableName = "media_items")
data class MediaItemEntity(
    @PrimaryKey val id: String,
    val name: String,
    val type: String,
    val overview: String?,
    val year: Int?,
    val runtimeTicks: Long?,
    val isFavorite: Boolean,
    val isPlayed: Boolean,
    val playedPercentage: Double?,
    val playbackPositionTicks: Long?,
    val primaryImageTag: String?,
    val backdropImageTag: String?,
    val seriesId: String?,
    val seriesName: String?,
    val seasonId: String?,
    val indexNumber: Int?,
    val parentIndexNumber: Int?,
    val userId: String,
    val lastUpdated: Long = System.currentTimeMillis(),
)

@Dao
interface MediaItemDao {

    @Query("SELECT * FROM media_items WHERE userId = :userId AND isFavorite = 1 ORDER BY name ASC")
    fun observeFavorites(userId: String): Flow<List<MediaItemEntity>>

    @Query("SELECT * FROM media_items WHERE userId = :userId AND isPlayed = 1 ORDER BY lastUpdated DESC LIMIT 50")
    fun observeRecentlyWatched(userId: String): Flow<List<MediaItemEntity>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsert(item: MediaItemEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun upsertAll(items: List<MediaItemEntity>)

    @Query("DELETE FROM media_items WHERE id = :id")
    suspend fun delete(id: String)

    @Query("DELETE FROM media_items WHERE userId = :userId")
    suspend fun clearForUser(userId: String)

    @Query("SELECT * FROM media_items WHERE id = :id")
    suspend fun getById(id: String): MediaItemEntity?
}

@Database(entities = [MediaItemEntity::class], version = 1, exportSchema = false)
abstract class MulletaFlixDatabase : RoomDatabase() {
    abstract fun mediaItemDao(): MediaItemDao
}
