using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Plugins.DramaBox
{
    /// <summary>
    /// A DramaBox book. On DramaBox a "book" is what MulletaFlix models as a Series.
    /// </summary>
    public sealed class DramaBoxBook
    {
        /// <summary>
        /// Gets or sets the numeric book id. This is the only stable key DramaBox exposes,
        /// and it is the last path segment of a drama URL.
        /// </summary>
        public string BookId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized title (Portuguese when the pt locale is crawled).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the English slug title.
        /// </summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cover URL exactly as DramaBox returned it.
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the localized synopsis.
        /// </summary>
        public string Overview { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of episodes.
        /// </summary>
        public int ChapterCount { get; set; }

        /// <summary>
        /// Gets or sets the uppercase language name reported by DramaBox, for example PORTUGUESE.
        /// </summary>
        public string Language { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the short language code, for example pt.
        /// </summary>
        public string SimpleLanguage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the community rating when the listing exposed one.
        /// </summary>
        public double? Rating { get; set; }

        /// <summary>
        /// Gets or sets the shelf time string, which carries the release date.
        /// </summary>
        public string ShelfTime { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the category names, which map to MulletaFlix genres.
        /// </summary>
        public IReadOnlyList<string> Genres { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the free-form tags, used as additional genres.
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the cast.
        /// </summary>
        public IReadOnlyList<DramaBoxPerformer> Performers { get; set; } = Array.Empty<DramaBoxPerformer>();

        /// <summary>
        /// Gets or sets the episodes. Only the detail page carries these.
        /// </summary>
        public IReadOnlyList<DramaBoxChapter> Chapters { get; set; } = Array.Empty<DramaBoxChapter>();

        /// <summary>
        /// Gets a value indicating whether this book came from a listing, which never carries episodes.
        /// </summary>
        public bool HasChapters => Chapters.Count > 0;
    }

    /// <summary>
    /// A cast member.
    /// </summary>
    public sealed class DramaBoxPerformer
    {
        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the slug form of the name.
        /// </summary>
        public string FormatName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the avatar URL.
        /// </summary>
        public string Avatar { get; set; } = string.Empty;
    }

    /// <summary>
    /// A single episode.
    /// </summary>
    public sealed class DramaBoxChapter
    {
        /// <summary>
        /// Gets or sets the chapter id.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the zero based index. DramaBox episode titles are always Chinese, so
        /// MulletaFlix derives "Episódio N" from this value instead of using <see cref="Name"/>.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the raw (Chinese) chapter name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the episode thumbnail URL.
        /// </summary>
        public string Cover { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the duration in milliseconds.
        /// </summary>
        public long DurationMs { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the episode is free to watch.
        /// </summary>
        public bool Unlock { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the source is an HLS playlist.
        /// </summary>
        public bool M3u8 { get; set; }
    }

    /// <summary>
    /// The persisted name to book id index, crawled from the DramaBox category listings.
    /// </summary>
    public sealed class DramaBoxIndexDocument
    {
        /// <summary>
        /// Gets or sets the UTC timestamp of the crawl.
        /// </summary>
        public DateTimeOffset BuiltUtc { get; set; }

        /// <summary>
        /// Gets or sets the locale the index was crawled in.
        /// </summary>
        public string Locale { get; set; } = "pt";

        /// <summary>
        /// Gets or sets the books.
        /// </summary>
        public List<DramaBoxBook> Books { get; set; } = new List<DramaBoxBook>();
    }
}
