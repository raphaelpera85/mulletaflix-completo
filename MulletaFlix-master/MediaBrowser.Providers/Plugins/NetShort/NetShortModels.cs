using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Plugins.NetShort
{
    /// <summary>
    /// A NetShort series. NetShort calls it a "short play"; MulletaFlix models it as a Series.
    /// </summary>
    public sealed class NetShortSeries
    {
        /// <summary>
        /// Gets or sets the numeric series id. It is the trailing number of every NetShort URL for
        /// the series, and the only stable key the platform exposes.
        /// </summary>
        public string SeriesId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized title.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized synopsis.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cover URL exactly as NetShort returned it.
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the site-relative path of the series page, for example
        /// /pt/drama/além-do-99-perdões-2082282581191360513.
        /// </summary>
        public string PagePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category names, which map to MulletaFlix genres.
        /// </summary>
        public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the release timestamp, taken from the detail payload's publishTime.
        /// </summary>
        public DateTime? PremiereDate { get; set; }

        /// <summary>
        /// Gets or sets the number of episodes the platform reports.
        /// </summary>
        public int EpisodeCount { get; set; }

        /// <summary>
        /// Gets or sets the episodes. A search result never carries these; only the detail payload
        /// and the JSON-LD fallback do.
        /// </summary>
        public IReadOnlyList<NetShortEpisode> Episodes { get; set; } = Array.Empty<NetShortEpisode>();

        /// <summary>
        /// Gets a value indicating whether this series came with a real episode list.
        /// </summary>
        public bool HasEpisodes => Episodes.Count > 0;
    }

    /// <summary>
    /// A single NetShort episode.
    /// </summary>
    public sealed class NetShortEpisode
    {
        /// <summary>
        /// Gets or sets the episode id NetShort uses for the video.
        /// </summary>
        public string EpisodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the one-based episode number. Unlike DramaBox, NetShort numbers its episodes
        /// from one in the payload, so no adjustment is needed.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the episode thumbnail URL.
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the episode is behind a paywall.
        /// </summary>
        public bool Locked { get; set; }
    }

    /// <summary>
    /// A ranked candidate produced by a NetShort lookup.
    /// </summary>
    public sealed class NetShortMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NetShortMatch"/> class.
        /// </summary>
        /// <param name="series">The matched series.</param>
        /// <param name="score">The similarity score.</param>
        public NetShortMatch(NetShortSeries series, double score)
        {
            Series = series;
            Score = score;
        }

        /// <summary>
        /// Gets the matched series.
        /// </summary>
        public NetShortSeries Series { get; }

        /// <summary>
        /// Gets the similarity score from 0 to 1.
        /// </summary>
        public double Score { get; }
    }
}
