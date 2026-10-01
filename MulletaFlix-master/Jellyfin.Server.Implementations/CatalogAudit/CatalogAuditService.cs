using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.CatalogAudit;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.CatalogAudit;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Implementations.CatalogAudit;

/// <summary>
/// Deterministic catalog validation, inconsistency detection, and safe remediation service.
/// Operates without AI models, enforcing strict provenance, locked field preservation, and rollback.
/// </summary>
public class CatalogAuditService : ICatalogAuditService
{
    private static readonly Regex YearRegex = new(@"\b(19\d\d|20\d\d)\b", RegexOptions.Compiled);
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".webm" };
    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase) { ".epub", ".pdf", ".mobi", ".cbr", ".cbz", ".azw3" };

    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<CatalogAuditService> _logger;
    private readonly ConcurrentDictionary<Guid, CatalogAuditHistoryEntry> _history = new();

    public CatalogAuditService(
        ILibraryManager libraryManager,
        ILogger<CatalogAuditService> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public CatalogAuditReport RunAudit(CatalogAuditFilter? filter = null)
    {
        var query = new InternalItemsQuery
        {
            Recursive = true,
            IsFolder = false
        };

        var items = _libraryManager.GetItemList(query);
        var allInconsistencies = new List<CatalogInconsistency>();

        foreach (var item in items)
        {
            if (filter?.ItemType is not null && !string.Equals(item.GetType().Name, filter.ItemType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var itemInconsistencies = AnalyzeItem(item);
            foreach (var inc in itemInconsistencies)
            {
                if (filter?.Category is not null && !string.Equals(inc.Category.ToString(), filter.Category, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (filter?.Severity is not null && !string.Equals(inc.Severity.ToString(), filter.Severity, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (filter?.MinConfidence.HasValue == true && inc.Confidence < filter.MinConfidence.Value)
                {
                    continue;
                }

                allInconsistencies.Add(inc);
            }
        }

        return new CatalogAuditReport
        {
            GeneratedAt = DateTime.UtcNow,
            TotalItemsScanned = items.Count,
            Inconsistencies = allInconsistencies
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<CatalogInconsistency> AnalyzeItem(BaseItem item)
    {
        var list = new List<CatalogInconsistency>();
        var itemType = item.GetType().Name;
        var path = item.Path ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(path);

        // 1. Check Year Mismatch between file name and metadata
        if (!string.IsNullOrEmpty(fileName))
        {
            var match = YearRegex.Match(fileName);
            if (match.Success && int.TryParse(match.Value, out var fileYear))
            {
                if (item.ProductionYear.HasValue && item.ProductionYear.Value != fileYear)
                {
                    list.Add(new CatalogInconsistency
                    {
                        ItemId = item.Id,
                        ItemName = item.Name,
                        ItemType = itemType,
                        Path = path,
                        Category = CatalogInconsistencyCategory.YearMismatch,
                        Severity = InconsistencySeverity.Warning,
                        Field = "ProductionYear",
                        CurrentValue = item.ProductionYear.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ExpectedValue = fileYear.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Evidence = $"Path filename '{fileName}' indicates release year {fileYear}, but item metadata records {item.ProductionYear.Value}.",
                        Confidence = 0.90,
                        Source = "FileNameHeuristic"
                    });
                }
            }
        }

        // 2. Check Missing Provider IDs based on catalog type
        var isMovie = string.Equals(itemType, "Movie", StringComparison.OrdinalIgnoreCase);
        var isSeries = string.Equals(itemType, "Series", StringComparison.OrdinalIgnoreCase) || string.Equals(itemType, "Season", StringComparison.OrdinalIgnoreCase);
        var isBook = string.Equals(itemType, "Book", StringComparison.OrdinalIgnoreCase);

        if (isMovie)
        {
            var hasTmdb = !string.IsNullOrWhiteSpace(item.GetProviderId("Tmdb"));
            var hasImdb = !string.IsNullOrWhiteSpace(item.GetProviderId("Imdb"));
            if (!hasTmdb && !hasImdb)
            {
                list.Add(new CatalogInconsistency
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    ItemType = itemType,
                    Path = path,
                    Category = CatalogInconsistencyCategory.MissingProviderId,
                    Severity = InconsistencySeverity.Warning,
                    Field = "ProviderIds",
                    CurrentValue = null,
                    ExpectedValue = "Tmdb or Imdb",
                    Evidence = "Movie item lacks canonical external provider identifiers (neither TMDB nor IMDB ID found).",
                    Confidence = 0.95,
                    Source = "CatalogContract"
                });
            }
        }
        else if (isSeries)
        {
            var hasTvdb = !string.IsNullOrWhiteSpace(item.GetProviderId("Tvdb"));
            var hasTmdb = !string.IsNullOrWhiteSpace(item.GetProviderId("Tmdb"));
            if (!hasTvdb && !hasTmdb)
            {
                list.Add(new CatalogInconsistency
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    ItemType = itemType,
                    Path = path,
                    Category = CatalogInconsistencyCategory.MissingProviderId,
                    Severity = InconsistencySeverity.Warning,
                    Field = "ProviderIds",
                    CurrentValue = null,
                    ExpectedValue = "Tvdb or Tmdb",
                    Evidence = "Series item lacks canonical television provider identifiers (neither TVDB nor TMDB ID found).",
                    Confidence = 0.95,
                    Source = "CatalogContract"
                });
            }
        }
        else if (isBook)
        {
            var hasOl = !string.IsNullOrWhiteSpace(item.GetProviderId("OpenLibrary"));
            var hasIsbn = !string.IsNullOrWhiteSpace(item.GetProviderId("Isbn"));
            if (!hasOl && !hasIsbn)
            {
                list.Add(new CatalogInconsistency
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    ItemType = itemType,
                    Path = path,
                    Category = CatalogInconsistencyCategory.MissingProviderId,
                    Severity = InconsistencySeverity.Warning,
                    Field = "ProviderIds",
                    CurrentValue = null,
                    ExpectedValue = "OpenLibrary or Isbn",
                    Evidence = "Book item lacks bibliographic identifiers (neither OpenLibrary nor ISBN found).",
                    Confidence = 0.95,
                    Source = "CatalogContract"
                });
            }
        }

        // 3. Check Missing Images
        if (!item.HasImage(ImageType.Primary, 0))
        {
            list.Add(new CatalogInconsistency
            {
                ItemId = item.Id,
                ItemName = item.Name,
                ItemType = itemType,
                Path = path,
                Category = CatalogInconsistencyCategory.MissingPrimaryImage,
                Severity = InconsistencySeverity.Notice,
                Field = "PrimaryImage",
                CurrentValue = "Missing",
                ExpectedValue = "Present",
                Evidence = "Item does not have a primary cover/poster image attached.",
                Confidence = 1.0,
                Source = "ImageIndex"
            });
        }

        if ((isMovie || isSeries) && !item.HasImage(ImageType.Backdrop, 0))
        {
            list.Add(new CatalogInconsistency
            {
                ItemId = item.Id,
                ItemName = item.Name,
                ItemType = itemType,
                Path = path,
                Category = CatalogInconsistencyCategory.MissingBackdrop,
                Severity = InconsistencySeverity.Notice,
                Field = "BackdropImage",
                CurrentValue = "Missing",
                ExpectedValue = "Present",
                Evidence = "Item does not have a background fanart/backdrop image attached.",
                Confidence = 0.85,
                Source = "ImageIndex"
            });
        }

        // 4. Check Locked Fields / Provenance
        if (item.IsLocked || (item.LockedFields != null && item.LockedFields.Length > 0))
        {
            var lockedList = string.Join(", ", item.LockedFields ?? Array.Empty<MetadataField>());
            list.Add(new CatalogInconsistency
            {
                ItemId = item.Id,
                ItemName = item.Name,
                ItemType = itemType,
                Path = path,
                Category = CatalogInconsistencyCategory.LockedFieldConflict,
                Severity = InconsistencySeverity.Notice,
                Field = "LockedFields",
                CurrentValue = lockedList,
                ExpectedValue = "Preserved",
                Evidence = $"Item has locked metadata provenance: IsLocked={item.IsLocked}, LockedFields=[{lockedList}].",
                Confidence = 1.0,
                Source = "MetadataProtection"
            });
        }

        // 5. Check Media Type Mismatch
        var ext = Path.GetExtension(path);
        if (!string.IsNullOrEmpty(ext))
        {
            if (isBook && VideoExtensions.Contains(ext))
            {
                list.Add(new CatalogInconsistency
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    ItemType = itemType,
                    Path = path,
                    Category = CatalogInconsistencyCategory.MediaTypeMismatch,
                    Severity = InconsistencySeverity.Error,
                    Field = "MediaType",
                    CurrentValue = "Book",
                    ExpectedValue = "Video",
                    Evidence = $"Item is classified as Book but path has video container extension '{ext}'.",
                    Confidence = 0.99,
                    Source = "ContainerIntegrity"
                });
            }
            else if ((isMovie || isSeries) && DocumentExtensions.Contains(ext))
            {
                list.Add(new CatalogInconsistency
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    ItemType = itemType,
                    Path = path,
                    Category = CatalogInconsistencyCategory.MediaTypeMismatch,
                    Severity = InconsistencySeverity.Error,
                    Field = "MediaType",
                    CurrentValue = itemType,
                    ExpectedValue = "Book/Document",
                    Evidence = $"Item is classified as Video but path has document extension '{ext}'.",
                    Confidence = 0.99,
                    Source = "ContainerIntegrity"
                });
            }
        }

        return list;
    }

    /// <inheritdoc />
    public async Task<CatalogAuditHistoryEntry> ApplyFixAsync(CatalogAuditFixRequest request, string username, CancellationToken cancellationToken = default)
    {
        var item = _libraryManager.GetItemById(request.ItemId)
            ?? throw new KeyNotFoundException($"Item with ID '{request.ItemId}' not found.");

        string? previousValue = null;

        if (string.Equals(request.Field, "ProductionYear", StringComparison.OrdinalIgnoreCase))
        {
            if (request.PreserveLockedFields && (item.IsLocked || item.LockedFields.Contains(MetadataField.ProductionLocations)))
            {
                throw new InvalidOperationException("Cannot update ProductionYear on an item with locked fields when PreserveLockedFields is enabled.");
            }

            previousValue = item.ProductionYear?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (int.TryParse(request.ApprovedValue, System.Globalization.CultureInfo.InvariantCulture, out var newYear))
            {
                item.ProductionYear = newYear;
            }
            else
            {
                throw new ArgumentException($"Invalid ProductionYear value: '{request.ApprovedValue}'.");
            }
        }
        else if (string.Equals(request.Field, "Name", StringComparison.OrdinalIgnoreCase))
        {
            if (request.PreserveLockedFields && (item.IsLocked || item.LockedFields.Contains(MetadataField.Name)))
            {
                throw new InvalidOperationException("Cannot update Name on an item with locked Name when PreserveLockedFields is enabled.");
            }

            previousValue = item.Name;
            item.Name = request.ApprovedValue;
        }
        else if (request.Field.StartsWith("ProviderId:", StringComparison.OrdinalIgnoreCase))
        {
            var providerName = request.Field.Substring("ProviderId:".Length).Trim();
            if (string.IsNullOrEmpty(providerName))
            {
                throw new ArgumentException("ProviderId field must specify provider name (e.g. 'ProviderId:Tmdb').");
            }

            previousValue = item.GetProviderId(providerName);
            item.SetProviderId(providerName, request.ApprovedValue);
        }
        else
        {
            throw new NotSupportedException($"Remediation for field '{request.Field}' is not supported.");
        }

        await _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);

        var historyEntry = new CatalogAuditHistoryEntry
        {
            ItemId = item.Id,
            Field = request.Field,
            PreviousValue = previousValue,
            AppliedValue = request.ApprovedValue,
            AppliedAt = DateTime.UtcNow,
            AppliedBy = string.IsNullOrWhiteSpace(username) ? "Admin" : username
        };

        _history[historyEntry.Id] = historyEntry;
        _logger.LogInformation("Applied deterministic catalog fix for item {ItemId} on field {Field} by {User}", item.Id, request.Field, historyEntry.AppliedBy);

        return historyEntry;
    }

    /// <inheritdoc />
    public async Task<bool> RollbackFixAsync(CatalogAuditRollbackRequest request, CancellationToken cancellationToken = default)
    {
        if (!_history.TryRemove(request.HistoryEntryId, out var historyEntry))
        {
            return false;
        }

        var item = _libraryManager.GetItemById(historyEntry.ItemId);
        if (item is null)
        {
            return false;
        }

        if (string.Equals(historyEntry.Field, "ProductionYear", StringComparison.OrdinalIgnoreCase))
        {
            item.ProductionYear = int.TryParse(historyEntry.PreviousValue, out var prevYear) ? prevYear : null;
        }
        else if (string.Equals(historyEntry.Field, "Name", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrEmpty(historyEntry.PreviousValue))
            {
                item.Name = historyEntry.PreviousValue;
            }
        }
        else if (historyEntry.Field.StartsWith("ProviderId:", StringComparison.OrdinalIgnoreCase))
        {
            var providerName = historyEntry.Field.Substring("ProviderId:".Length).Trim();
            item.SetProviderId(providerName, historyEntry.PreviousValue);
        }

        await _libraryManager.UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Rolled back catalog fix {HistoryId} for item {ItemId} on field {Field}", request.HistoryEntryId, item.Id, historyEntry.Field);

        return true;
    }

    /// <inheritdoc />
    public IReadOnlyList<CatalogAuditHistoryEntry> GetHistory()
    {
        return _history.Values.OrderByDescending(h => h.AppliedAt).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<ProviderTermsInfo> GetProviderTerms()
    {
        return new List<ProviderTermsInfo>
        {
            new()
            {
                ProviderName = "OpenLibrary",
                Purpose = "Book and literary work metadata, ISBN, edition details and covers.",
                SupportedIdentifiers = "OpenLibrary Work ID (OL...W), Edition ID (OL...M), ISBN-10, ISBN-13",
                RateLimits = "100 requests per IP per minute. Mass harvesting or bulk scraping is strictly disallowed; bulk data dumps must be used for wholesale datasets.",
                AttributionRequired = "Open Library data is dedicated to the public domain under CC0 1.0 Universal. Attribution to openlibrary.org is encouraged.",
                TermsSummary = "Intended for individual bibliographic search and metadata enrichment. Must respect server load and use single targeted queries.",
                AllowsCommercialUse = true,
                RequiresApiKey = false
            },
            new()
            {
                ProviderName = "TheMovieDb (TMDB)",
                Purpose = "Feature films, TV series, actors, posters, backdrops and synopses.",
                SupportedIdentifiers = "TMDB ID, IMDB ID",
                RateLimits = "Concurrency limited per API client key. Automatic request bursting throttled.",
                AttributionRequired = "Requires standard TMDB attribution notice and logo in client application displays.",
                TermsSummary = "Free for non-commercial personal media server use. Must not alter or misrepresent rating or content advisories.",
                AllowsCommercialUse = false,
                RequiresApiKey = true
            },
            new()
            {
                ProviderName = "TheTVDB",
                Purpose = "Television series structure, seasons, episode numbering, and artwork.",
                SupportedIdentifiers = "TVDB Series ID, Episode ID",
                RateLimits = "Bearer token based authentication with subscription tier constraints.",
                AttributionRequired = "TheTVDB attribution notice recommended.",
                TermsSummary = "Personal server usage permitted under developer agreement.",
                AllowsCommercialUse = false,
                RequiresApiKey = true
            },
            new()
            {
                ProviderName = "Fanart.tv",
                Purpose = "High-definition logos, clearart, discart, and artistic backgrounds.",
                SupportedIdentifiers = "MusicBrainz ID, TVDB ID, TMDB ID",
                RateLimits = "Project API key required; aggressive client-side caching mandatory to prevent repeated asset calls.",
                AttributionRequired = "Fanart.tv community attribution appreciated.",
                TermsSummary = "Asset caching permitted; hotlinking directly to origins in high volume prohibited without local cache.",
                AllowsCommercialUse = false,
                RequiresApiKey = true
            }
        };
    }
}
