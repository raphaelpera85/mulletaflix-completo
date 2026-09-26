using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Plugins.GoodShort
{
    /// <summary>
    /// A GoodShort series. On GoodShort a "book" is what MulletaFlix models as a Series.
    /// </summary>
    public sealed class GoodShortSeries
    {
        /// <summary>
        /// Gets or sets the numeric book id. It is the only stable key GoodShort exposes and it is
        /// the trailing number of a drama URL.
        /// </summary>
        public string SeriesId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized title, for example "[Dublado] Abandonada pelo Don, Coroada pela Máfia".
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL slug GoodShort uses for this book, for example
        /// "dublado-abandonada-pelo-don-coroada-pela-máfia-31001499548".
        /// </summary>
        /// <remarks>
        /// Kept because the HTML/JSON-LD fallback can only address a series page through its slug;
        /// the JSON API is addressed by id alone. When the API is unreachable and no slug was ever
        /// learned for this id, the fallback cannot run.
        /// </remarks>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cover URL exactly as GoodShort returned it.
        /// </summary>
        /// <remarks>
        /// GoodShort serves these from acf.goodshort.com without a signature or expiry, so they are
        /// reused verbatim; unlike DramaBox there is no "@w=&amp;h=" transform suffix to rewrite.
        /// </remarks>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the synopsis.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of episodes the source reports.
        /// </summary>
        public int EpisodeCount { get; set; }

        /// <summary>
        /// Gets or sets the language name reported by GoodShort, for example PORTUGUESE.
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the community rating, when GoodShort reported a non-zero one.
        /// </summary>
        /// <remarks>
        /// GoodShort returns ratings 0 for most books, which means "unrated" rather than "rated
        /// zero". Treating it as a real value would publish a 0.0 score for the whole catalogue.
        /// </remarks>
        public double? Rating { get; set; }

        /// <summary>
        /// Gets or sets the premiere timestamp when the source exposed one.
        /// </summary>
        public DateTime? PremiereDate { get; set; }

        /// <summary>
        /// Gets or sets the category names, which map to MulletaFlix genres.
        /// </summary>
        public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the episodes.
        /// </summary>
        /// <remarks>
        /// Only the channel-page source can enumerate all of them. The JSON-LD ItemList that the
        /// detail page carries is truncated by GoodShort itself, so a series read through the HTML
        /// fallback holds a partial list and <see cref="EpisodesAreComplete"/> stays false.
        /// </remarks>
        public IReadOnlyList<GoodShortEpisode> Episodes { get; set; } = Array.Empty<GoodShortEpisode>();

        /// <summary>
        /// Gets a value indicating whether the series carries any episode.
        /// </summary>
        public bool HasEpisodes => Episodes.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the episode list holds every episode GoodShort reports.
        /// </summary>
        /// <remarks>
        /// The providers use this to decide whether a missing episode number means "this series
        /// really has no such episode" or merely "the source we could reach did not list it".
        /// </remarks>
        public bool EpisodesAreComplete => EpisodeCount > 0 && Episodes.Count >= EpisodeCount;
    }

    /// <summary>
    /// A single episode.
    /// </summary>
    public sealed class GoodShortEpisode
    {
        /// <summary>
        /// Gets or sets the episode id.
        /// </summary>
        public string EpisodeId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the one-based episode number.
        /// </summary>
        /// <remarks>
        /// GoodShort names chapters "001", "002", ... so the number is read from that name and not
        /// from the zero-based "index" field, which the source also sends.
        /// </remarks>
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the raw episode name, for example "Abandonei o Rei dos Deuses no Altar - EP 1".
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the episode thumbnail URL.
        /// </summary>
        public string Thumbnail { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration in seconds.
        /// </summary>
        /// <remarks>
        /// GoodShort reports "playTime" in seconds, cross-checked against the JSON-LD
        /// "PT1M21S" duration of the same episode, which is 81. The property is named for the unit
        /// because the neighbouring DramaBox payload reports milliseconds, and the two were
        /// confused once already.
        /// </remarks>
        public long DurationSeconds { get; set; }
    }

    /// <summary>
    /// One page of the GoodShort channel listing.
    /// </summary>
    public sealed class GoodShortEpisodePage
    {
        /// <summary>
        /// Gets or sets the episodes on this page.
        /// </summary>
        public IReadOnlyList<GoodShortEpisode> Episodes { get; set; } = Array.Empty<GoodShortEpisode>();

        /// <summary>
        /// Gets or sets the total number of episodes the source reports.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets the total number of pages the source reports.
        /// </summary>
        public int Pages { get; set; }
    }

    /// <summary>
    /// A ranked candidate produced by a GoodShort lookup.
    /// </summary>
    public sealed class GoodShortMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GoodShortMatch"/> class.
        /// </summary>
        /// <param name="series">The matched series.</param>
        /// <param name="score">The similarity score.</param>
        public GoodShortMatch(GoodShortSeries series, double score)
        {
            Series = series;
            Score = score;
        }

        /// <summary>
        /// Gets the matched series.
        /// </summary>
        public GoodShortSeries Series { get; }

        /// <summary>
        /// Gets the similarity score from 0 to 1.
        /// </summary>
        public double Score { get; }
    }
}
