using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Plugins.NartoDrama
{
    /// <summary>
    /// A NartoDrama series.
    /// </summary>
    /// <remarks>
    /// NartoDrama calls these "movies" in its own markup (<c>&lt;div class="movie-meta"&gt;</c>) but
    /// they are episodic series, which is how MulletaFlix models them. Everything this type carries
    /// comes from the single server rendered detail page described in <see cref="NartoDramaParser"/>.
    /// </remarks>
    public sealed class NartoDramaSeries
    {
        /// <summary>
        /// Gets or sets the URL slug, for example "abandonei-o-rei-dos-deuses-no-altar".
        /// </summary>
        /// <remarks>
        /// This is the value stored under the <c>NartoDrama</c> provider id. It was chosen over the
        /// numeric content id because the slug is the only key that can address a page on its own:
        /// <c>/detail/watch/&lt;slug&gt;</c> is the whole URL, the search results hand out slugs, and
        /// it is what a user pastes. The numeric id only addresses the poster CDN, and it is absent
        /// from the URL, so an id alone cannot be turned back into a series.
        /// </remarks>
        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric content id, for example 91437.
        /// </summary>
        /// <remarks>
        /// Read from the page and kept for diagnostics and for
        /// <see cref="NartoDramaParser.BuildPosterUrl"/>: it is the one way to rebuild a cover URL
        /// when neither the JSON-LD nor the og:image tag carries one.
        /// </remarks>
        public string ContentId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized title, with the site's " - Streaming grátis" marker removed.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cover URL exactly as the site published it.
        /// </summary>
        /// <remarks>
        /// The host serves both <c>.jpg</c> and <c>.webp</c> posters, so the URL is always reused
        /// verbatim rather than rebuilt from the content id.
        /// </remarks>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the synopsis.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of episodes the page reports.
        /// </summary>
        public int EpisodeCount { get; set; }

        /// <summary>
        /// Gets or sets the free-form tag labels the page renders, without their leading "#".
        /// </summary>
        /// <remarks>
        /// These are NartoDrama's own hashtags ("Distúrbio", "Casamento"), which are closer to
        /// keywords than to a fixed genre vocabulary, so they are published as tags rather than as
        /// genres.
        /// </remarks>
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the episodes read from the page's episode list.
        /// </summary>
        public IReadOnlyList<NartoDramaEpisode> Episodes { get; set; } = Array.Empty<NartoDramaEpisode>();

        /// <summary>
        /// Gets a value indicating whether the series carries any episode.
        /// </summary>
        public bool HasEpisodes => Episodes.Count > 0;

        /// <summary>
        /// Gets a value indicating whether the episode list holds every episode the page reports.
        /// </summary>
        /// <remarks>
        /// The page states its own episode total twice (a <c>currentEpCount</c> script variable and
        /// the "Episódios (N)" heading), so the two can be compared. The providers use this to tell
        /// "this series really has no episode 40" from "the page we could read did not list it".
        /// </remarks>
        public bool EpisodesAreComplete => EpisodeCount > 0 && Episodes.Count >= EpisodeCount;
    }

    /// <summary>
    /// A single episode.
    /// </summary>
    public sealed class NartoDramaEpisode
    {
        /// <summary>
        /// Gets or sets the one-based episode number, which is the last path segment of the episode URL.
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// Gets or sets the label the page renders on the episode, for example "001".
        /// </summary>
        /// <remarks>
        /// Kept because it is the only human readable episode name the page publishes: the anchor
        /// text is just "EP 1", so a title is derived from <see cref="Number"/> instead.
        /// </remarks>
        public string Label { get; set; } = string.Empty;
    }

    /// <summary>
    /// A ranked candidate produced by a NartoDrama lookup.
    /// </summary>
    public sealed class NartoDramaMatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NartoDramaMatch"/> class.
        /// </summary>
        /// <param name="series">The matched series.</param>
        /// <param name="score">The similarity score.</param>
        public NartoDramaMatch(NartoDramaSeries series, double score)
        {
            Series = series;
            Score = score;
        }

        /// <summary>
        /// Gets the matched series.
        /// </summary>
        public NartoDramaSeries Series { get; }

        /// <summary>
        /// Gets the similarity score from 0 to 1.
        /// </summary>
        public double Score { get; }
    }
}
