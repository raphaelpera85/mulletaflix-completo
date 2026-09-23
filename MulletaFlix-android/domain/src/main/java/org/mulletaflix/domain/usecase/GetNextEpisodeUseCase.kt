package org.mulletaflix.domain.usecase

import org.mulletaflix.domain.model.MediaItem
import org.mulletaflix.domain.repository.MediaRepository
import javax.inject.Inject

/**
 * UseCase to discover the next episode in a series for auto-play and next up flows.
 *
 * Checks:
 * 1. Next episode within the same season based on indexNumber or list sequence.
 * 2. If at the end of the season, checks if a subsequent season exists and finds its first episode.
 *
 * The three lookups now agree on failure: `getEpisodes` already propagated it, but
 * the season lookups collapsed it into `Result.success(null)`. `null` means "this
 * series has no next episode", and the player hides the "Próximo episódio" prompt on
 * `null` — so a transient 5xx during the season lookup ended the binge silently,
 * indistinguishable from a series finale. The failure now travels; whoever decides
 * whether to retry can tell the two apart.
 */
class GetNextEpisodeUseCase @Inject constructor(
    private val mediaRepository: MediaRepository,
) {
    suspend operator fun invoke(userId: String, currentItem: MediaItem): Result<MediaItem?> {
        val seriesId = currentItem.seriesId ?: return Result.success(null)
        val seasonId = currentItem.seasonId

        val episodesResult = mediaRepository.getEpisodes(userId, seriesId, seasonId)
        val episodes = episodesResult.getOrElse { return Result.failure(it) }

        if (episodes.isEmpty()) return Result.success(null)

        val currentIndex = currentItem.indexNumber
        val nextEpisode = if (currentIndex != null) {
            episodes
                .filter { (it.indexNumber ?: -1) > currentIndex }
                .minByOrNull { it.indexNumber ?: Int.MAX_VALUE }
        } else {
            val pos = episodes.indexOfFirst { it.id == currentItem.id }
            if (pos in 0 until episodes.size - 1) episodes[pos + 1] else null
        }

        if (nextEpisode != null) {
            return Result.success(nextEpisode)
        }

        // Check if there is a next season
        if (seasonId != null) {
            val seasons = mediaRepository.getSeasons(userId, seriesId)
                .getOrElse { return Result.failure(it) }
            val currentSeasonIndex = currentItem.parentIndexNumber
            val nextSeason = if (currentSeasonIndex != null) {
                seasons
                    .filter { (it.indexNumber ?: -1) > currentSeasonIndex }
                    .minByOrNull { it.indexNumber ?: Int.MAX_VALUE }
            } else {
                val sPos = seasons.indexOfFirst { it.id == seasonId }
                if (sPos in 0 until seasons.size - 1) seasons[sPos + 1] else null
            }

            if (nextSeason != null) {
                val nextSeasonEpisodes = mediaRepository
                    .getEpisodes(userId, seriesId, nextSeason.id)
                    .getOrElse { return Result.failure(it) }
                val firstEp = nextSeasonEpisodes.minByOrNull { it.indexNumber ?: 0 }
                    ?: nextSeasonEpisodes.firstOrNull()
                if (firstEp != null) {
                    return Result.success(firstEp)
                }
            }
        }

        // Chegou aqui sem falha nenhuma: a série realmente acabou.
        return Result.success(null)
    }
}
