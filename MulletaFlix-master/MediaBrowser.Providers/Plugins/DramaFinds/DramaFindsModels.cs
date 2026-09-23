using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Plugins.DramaFinds
{
    /// <summary>
    /// A DramaFinds drama. On DramaFinds a "drama" is what MulletaFlix models as a Series.
    /// </summary>
    public sealed class DramaFindsDrama
    {
        /// <summary>
        /// Gets or sets the numeric drama id. This is the key the platform uses everywhere: inside
        /// every API payload and in the path of every public drama and episode URL.
        /// </summary>
        public string DramaId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the md5 identifier. It is stable but only ever appears inside API payloads,
        /// so it is carried for diagnostics rather than used as a key.
        /// </summary>
        public string Md5Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized title, Portuguese on the pt locale.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized synopsis.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cover URL with the expiring auth_key query already removed.
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the community rating when the payload exposed one.
        /// </summary>
        public double? Rating { get; set; }

        /// <summary>
        /// Gets or sets the number of episodes, read from "episodes" on a search row and from
        /// "totalCount" on the detail payload.
        /// </summary>
        public int EpisodeCount { get; set; }

        /// <summary>
        /// Gets or sets the release date exactly as the API reports it, for example 2025-12-22.
        /// It is kept as a string so the sort tie-break stays byte-for-byte reproducible.
        /// </summary>
        public string ReleaseDate { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the country or locale code, for example pt.
        /// </summary>
        public string Country { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the view count.
        /// </summary>
        public long ViewsCount { get; set; }

        /// <summary>
        /// Gets or sets the like count.
        /// </summary>
        public long LikesCount { get; set; }

        /// <summary>
        /// Gets or sets how many leading episodes are free to watch.
        /// </summary>
        public int FreeCount { get; set; }

        /// <summary>
        /// Gets or sets the free-form tags. The platform reports this field as null on the payloads
        /// seen so far, so in practice it stays empty.
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the episodes. The API never returns the list, so these are synthesized
        /// from <see cref="EpisodeCount"/> and only the detail payload carries them.
        /// </summary>
        public IReadOnlyList<DramaFindsEpisode> Episodes { get; set; } = Array.Empty<DramaFindsEpisode>();
    }

    /// <summary>
    /// One episode, synthesized from the episode count and the deterministic public URL.
    /// </summary>
    public sealed class DramaFindsEpisode
    {
        /// <summary>
        /// Gets or sets the one based episode number, which is also the number in the name.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the display name, always "Episódio N": the platform has no per-episode
        /// titles, and its own episode pages set exactly this string as their og:title.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the public watch URL, which is the only per-episode identifier the
        /// platform exposes.
        /// </summary>
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>
    /// A ranked candidate produced by a DramaFinds lookup.
    /// </summary>
    public sealed class DramaFindsMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DramaFindsMatch"/> class.
        /// </summary>
        /// <param name="drama">The matched drama.</param>
        /// <param name="score">The similarity score.</param>
        public DramaFindsMatch(DramaFindsDrama drama, double score)
        {
            Drama = drama;
            Score = score;
        }

        /// <summary>
        /// Gets the matched drama.
        /// </summary>
        public DramaFindsDrama Drama { get; }

        /// <summary>
        /// Gets the similarity score from 0 to 1.
        /// </summary>
        public double Score { get; }
    }
}
