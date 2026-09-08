#nullable disable

#pragma warning disable CS1591

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Controller.Entities.Movies
{
    /// <summary>
    /// Class Movie.
    /// </summary>
    public class Movie : Video, IHasSpecialFeatures, IHasTrailers, IHasLookupInfo<MovieInfo>, ISupportsBoxSetGrouping
    {
        /// <inheritdoc />
        [JsonIgnore]
        public IReadOnlyList<Guid> SpecialFeatureIds => GetExtras()
            .Where(extra => extra.ExtraType is not null && extra is Video)
            .Select(extra => extra.Id)
            .ToArray();

        /// <inheritdoc />
        [JsonIgnore]
        public IReadOnlyList<BaseItem> LocalTrailers => GetExtras([Model.Entities.ExtraType.Trailer]).ToArray();

        /// <summary>
        /// Gets or sets the name of the TMDb collection.
        /// </summary>
        /// <value>The name of the TMDb collection.</value>
        public string TmdbCollectionName { get; set; }

        [JsonIgnore]
        public string CollectionName
        {
            get => TmdbCollectionName;
            set => TmdbCollectionName = value;
        }

        public override double GetDefaultPrimaryImageAspectRatio()
        {
            // hack for tv plugins
            if (SourceType == SourceType.Channel)
            {
                return 0;
            }

            return 2.0 / 3;
        }

        /// <inheritdoc />
        public override UnratedItem GetBlockUnratedType()
        {
            return UnratedItem.Movie;
        }

        public MovieInfo GetLookupInfo()
        {
            var info = GetItemLookupInfo<MovieInfo>();

            if (!IsInMixedFolder)
            {
                var name = System.IO.Path.GetFileName(ContainingFolderPath);

                if (VideoType == VideoType.VideoFile || VideoType == VideoType.Iso)
                {
                    if (string.Equals(name, System.IO.Path.GetFileName(Path), StringComparison.OrdinalIgnoreCase))
                    {
                        // if the folder has the file extension, strip it
                        name = System.IO.Path.GetFileNameWithoutExtension(name);
                    }
                }

                info.Name = name;
            }

            if (!info.Year.HasValue)
            {
                var parsed = LibraryManager.ParseName(info.Name);
                if (parsed.Year.HasValue)
                {
                    info.Year = parsed.Year;
                }
                else if (!string.IsNullOrWhiteSpace(Path))
                {
                    var fileParsed = LibraryManager.ParseName(System.IO.Path.GetFileNameWithoutExtension(Path));
                    if (fileParsed.Year.HasValue)
                    {
                        info.Year = fileParsed.Year;
                    }
                }
            }

            return info;
        }

        /// <inheritdoc />
        public override bool BeforeMetadataRefresh(bool replaceAllMetadata)
        {
            var hasChanges = base.BeforeMetadataRefresh(replaceAllMetadata);

            if (!ProductionYear.HasValue)
            {
                var info = LibraryManager.ParseName(Name);
                var yearInName = info.Year;

                if (!yearInName.HasValue && !string.IsNullOrWhiteSpace(Path))
                {
                    info = LibraryManager.ParseName(System.IO.Path.GetFileNameWithoutExtension(Path));
                    yearInName = info.Year;
                }

                if (yearInName.HasValue)
                {
                    ProductionYear = yearInName;
                    hasChanges = true;
                }
                else
                {
                    // Try to get the year from the folder name
                    if (!IsInMixedFolder && !string.IsNullOrWhiteSpace(ContainingFolderPath))
                    {
                        info = LibraryManager.ParseName(System.IO.Path.GetFileName(ContainingFolderPath));

                        yearInName = info.Year;

                        if (yearInName.HasValue)
                        {
                            ProductionYear = yearInName;
                            hasChanges = true;
                        }
                    }
                }
            }

            return hasChanges;
        }
    }
}

