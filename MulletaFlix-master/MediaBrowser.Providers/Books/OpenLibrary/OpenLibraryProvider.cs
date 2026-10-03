using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Data.Enums;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.Providers.Books.OpenLibrary
{
    public class OpenLibraryProvider : IRemoteMetadataProvider<Book, BookInfo>, IRemoteImageProvider, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenLibraryProvider> _logger;

        public OpenLibraryProvider(IHttpClientFactory httpClientFactory, ILogger<OpenLibraryProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Name => "Open Library";

        public int Order => 0;

        public bool Supports(BaseItem item)
        {
            return item is Book;
        }

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            if (item is not Book)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var images = new List<RemoteImageInfo>();

            var openLibraryId = item.GetProviderId("OpenLibrary");
            if (!string.IsNullOrWhiteSpace(openLibraryId))
            {
                var metadata = await GetMetadataByOpenLibraryId(openLibraryId, cancellationToken).ConfigureAwait(false);
                AddRemoteImages(images, metadata);
            }

            var isbn = item.GetProviderId("ISBN");
            if (images.Count == 0 && !string.IsNullOrWhiteSpace(isbn))
            {
                var metadata = await GetMetadataByIsbn(isbn, cancellationToken).ConfigureAwait(false);
                AddRemoteImages(images, metadata);

                var cleanIsbn = isbn.Replace("-", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
                AddImageIfMissing(images, $"https://covers.openlibrary.org/b/isbn/{cleanIsbn}-L.jpg?default=false");
            }

            if (images.Count == 0 && !string.IsNullOrWhiteSpace(item.Name))
            {
                var metadata = await GetMetadataBySearch(item.Name, cancellationToken).ConfigureAwait(false);
                AddRemoteImages(images, metadata);
            }

            return images;
        }

        public async Task<MetadataResult<Book>> GetMetadata(BookInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Book>();
            var isbn = NormalizeIsbn(info.GetProviderId("ISBN") ?? ExtractIsbn(info.Name));
            var openLibraryId = ExtractOpenLibraryId(info.GetProviderId("OpenLibrary") ?? info.Name);

            if (!string.IsNullOrWhiteSpace(isbn))
            {
                result = await GetMetadataByIsbn(isbn, cancellationToken).ConfigureAwait(false);
                if (result.HasMetadata && !string.IsNullOrWhiteSpace(openLibraryId))
                {
                    var openLibraryMetadata = await GetMetadataByOpenLibraryId(openLibraryId, cancellationToken).ConfigureAwait(false);
                    MergeSupplementalMetadata(result, openLibraryMetadata);
                }

                if (result.HasMetadata)
                {
                    result.QueriedById = true;
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(openLibraryId))
            {
                result = await GetMetadataByOpenLibraryId(openLibraryId, cancellationToken).ConfigureAwait(false);
                if (result.HasMetadata)
                {
                    result.QueriedById = true;
                    return result;
                }
            }

            if (!string.IsNullOrWhiteSpace(info.Name))
            {
                result = await GetMetadataBySearch(info.Name, cancellationToken).ConfigureAwait(false);
            }

            return result;
        }

        private async Task<MetadataResult<Book>> GetMetadataByIsbn(string isbn, CancellationToken cancellationToken)
        {
            var cleanIsbn = NormalizeIsbn(isbn);
            var results = await GetSearchResultsByIsbn(cleanIsbn, cancellationToken).ConfigureAwait(false);
            var best = results.FirstOrDefault(result => string.Equals(
                NormalizeIsbn(result.GetProviderId("ISBN")), cleanIsbn, StringComparison.OrdinalIgnoreCase))
                ?? results.FirstOrDefault();
            if (best is null)
            {
                return new MetadataResult<Book>();
            }

            MetadataResult<Book> metadata;
            var editionId = best.GetProviderId("OpenLibrary");
            if (!string.IsNullOrWhiteSpace(editionId))
            {
                metadata = await GetMetadataByOpenLibraryId(editionId, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                metadata = new MetadataResult<Book>();
            }

            if (!metadata.HasMetadata)
            {
                metadata = CreateMetadataFromSearchResult(best);
            }

            if (metadata.HasMetadata && metadata.Item is not null)
            {
                metadata.Item.SetProviderId("ISBN", cleanIsbn);
                metadata.QueriedById = true;
                if (metadata.RemoteImages.Count == 0 && !string.IsNullOrWhiteSpace(best.ImageUrl))
                {
                    metadata.RemoteImages.Add((best.ImageUrl, ImageType.Primary));
                }
            }

            return metadata;
        }

        private async Task<MetadataResult<Book>> GetMetadataByOpenLibraryId(string olId, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Book>();
            var normalizedId = NormalizeOpenLibraryId(olId);
            var isWorkId = normalizedId.EndsWith('W');
            var url = isWorkId
                ? $"https://openlibrary.org/works/{Uri.EscapeDataString(normalizedId)}.json"
                : $"https://openlibrary.org/books/{Uri.EscapeDataString(normalizedId)}.json";

            try
            {
                using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                    .GetAsync(url, cancellationToken)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (isWorkId)
                {
                    using var document = JsonDocument.Parse(json);
                    PopulateMetadataFromWork(result, document.RootElement);
                    if (result.RemoteImages.Count == 0)
                    {
                        await AddEditionImagesFromWork(result, normalizedId, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    using var document = JsonDocument.Parse(json);
                    PopulateMetadata(result, document.RootElement);
                    await PopulateEditionAuthors(result, document.RootElement, cancellationToken).ConfigureAwait(false);
                }

                AddOpenLibraryFallbackImages(result, null, normalizedId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching OpenLibrary metadata by OLID {Id}", olId);
            }

            return result;
        }

        private async Task<MetadataResult<Book>> GetMetadataBySearch(string title, CancellationToken cancellationToken)
        {
            var results = await GetSearchResults(new BookInfo { Name = title }, cancellationToken).ConfigureAwait(false);
            var best = SelectBestTitleMatch(results, title);
            if (best is null)
            {
                return new MetadataResult<Book>();
            }

            MetadataResult<Book> metadata;

            if (best.ProviderIds.TryGetValue("OpenLibrary", out var openLibraryId))
            {
                metadata = await GetMetadataByOpenLibraryId(openLibraryId, cancellationToken).ConfigureAwait(false);

                if (best.ProviderIds.TryGetValue("ISBN", out var isbn))
                {
                    var isbnMetadata = await GetMetadataByIsbn(isbn, cancellationToken).ConfigureAwait(false);
                    MergeSupplementalMetadata(metadata, isbnMetadata);
                }
            }
            else if (best.ProviderIds.TryGetValue("ISBN", out var isbn))
            {
                metadata = await GetMetadataByIsbn(isbn, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                metadata = new MetadataResult<Book>();
            }

            if (metadata.HasMetadata
                && metadata.RemoteImages.Count == 0
                && !string.IsNullOrWhiteSpace(best.ImageUrl))
            {
                metadata.RemoteImages.Add((best.ImageUrl, ImageType.Primary));
            }

            return metadata;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(BookInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();
            var query = searchInfo.Name;

            var openLibraryId = ExtractOpenLibraryId(searchInfo.GetProviderId("OpenLibrary") ?? query);
            var isbn = NormalizeIsbn(searchInfo.GetProviderId("ISBN") ?? ExtractIsbn(query));

            if (!string.IsNullOrWhiteSpace(openLibraryId))
            {
                var metadata = await GetMetadataByOpenLibraryId(openLibraryId, cancellationToken).ConfigureAwait(false);
                if (metadata.HasMetadata && metadata.Item is not null)
                {
                    AddMetadataSearchResult(results, metadata, openLibraryId);
                }
            }

            if (!string.IsNullOrWhiteSpace(isbn))
            {
                var isbnResults = await GetSearchResultsByIsbn(isbn, cancellationToken).ConfigureAwait(false);
                foreach (var item in isbnResults)
                {
                    AddSearchResultIfMissing(results, item);
                }
            }

            if (results.Count > 0)
            {
                return results;
            }

            if (!string.IsNullOrWhiteSpace(searchInfo.SeriesName))
            {
                query = $"{searchInfo.SeriesName} {query}";
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return results;
            }

            try
            {
                foreach (var searchQuery in BuildSearchQueries(query))
                {
                    var url = $"https://openlibrary.org/search.json?title={Uri.EscapeDataString(searchQuery)}&fields=key,title,author_name,first_publish_year,isbn,cover_edition_key,cover_i&limit=10";
                    using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                        .GetAsync(url, cancellationToken)
                        .ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var searchResult = JsonSerializer.Deserialize<OpenLibrarySearchResult>(json);

                    if (searchResult?.Docs is null)
                    {
                        continue;
                    }

                    AddSearchDocuments(results, searchResult.Docs);

                    if (results.Count > 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching OpenLibrary for {Query}", query);
            }

            return results;
        }

        private async Task<List<RemoteSearchResult>> GetSearchResultsByIsbn(string isbn, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(isbn))
            {
                return results;
            }

            try
            {
                var url = $"https://openlibrary.org/search.json?isbn={Uri.EscapeDataString(isbn)}&fields=key,title,author_name,first_publish_year,isbn,cover_edition_key,cover_i&limit=10";
                using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                    .GetAsync(url, cancellationToken)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var searchResult = JsonSerializer.Deserialize<OpenLibrarySearchResult>(json);
                if (searchResult?.Docs is not null)
                {
                    AddSearchDocuments(results, searchResult.Docs, isbn);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching OpenLibrary for ISBN {Isbn}", isbn);
            }

            return results;
        }

        private void AddSearchDocuments(ICollection<RemoteSearchResult> results, IEnumerable<OpenLibraryDoc> docs, string? preferredIsbn = null)
        {
            foreach (var doc in docs)
            {
                if (string.IsNullOrWhiteSpace(doc.Title))
                {
                    continue;
                }

                var item = new RemoteSearchResult
                {
                    Name = doc.Title,
                    SearchProviderName = Name,
                    ProductionYear = doc.FirstPublishYear > 0 ? doc.FirstPublishYear : null
                };

                var matchingIsbn = doc.Isbn?.FirstOrDefault(candidate =>
                    string.Equals(NormalizeIsbn(candidate), NormalizeIsbn(preferredIsbn), StringComparison.OrdinalIgnoreCase));
                var selectedIsbn = matchingIsbn ?? doc.Isbn?.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(selectedIsbn))
                {
                    item.SetProviderId("ISBN", selectedIsbn);
                }

                if (!string.IsNullOrWhiteSpace(doc.CoverEditionKey))
                {
                    item.SetProviderId("OpenLibrary", doc.CoverEditionKey);
                    item.ImageUrl = $"https://covers.openlibrary.org/b/olid/{doc.CoverEditionKey}-M.jpg";
                }
                else if (doc.CoverId > 0)
                {
                    item.ImageUrl = $"https://covers.openlibrary.org/b/id/{doc.CoverId}-M.jpg";
                }
                else if (!string.IsNullOrWhiteSpace(selectedIsbn))
                {
                    item.ImageUrl = $"https://covers.openlibrary.org/b/isbn/{selectedIsbn}-M.jpg";
                }

                if (!string.IsNullOrWhiteSpace(doc.Key) && string.IsNullOrWhiteSpace(item.GetProviderId("OpenLibrary")))
                {
                    item.SetProviderId("OpenLibrary", doc.Key.Replace("/works/", string.Empty, StringComparison.Ordinal));
                }

                AddSearchResultIfMissing(results, item);
            }
        }

        private void AddMetadataSearchResult(ICollection<RemoteSearchResult> results, MetadataResult<Book> metadata, string openLibraryId)
        {
            var book = metadata.Item!;
            var item = new RemoteSearchResult
            {
                Name = book.Name,
                ProductionYear = book.ProductionYear,
                SearchProviderName = Name,
                ImageUrl = metadata.RemoteImages.FirstOrDefault(image => image.Type == ImageType.Primary).Url
            };

            item.SetProviderId("OpenLibrary", book.GetProviderId("OpenLibrary") ?? openLibraryId);
            var isbn = book.GetProviderId("ISBN");
            if (!string.IsNullOrWhiteSpace(isbn))
            {
                item.SetProviderId("ISBN", isbn);
            }

            AddSearchResultIfMissing(results, item);
        }

        private static MetadataResult<Book> CreateMetadataFromSearchResult(RemoteSearchResult searchResult)
        {
            var result = new MetadataResult<Book>
            {
                Item = new Book
                {
                    Name = searchResult.Name,
                    ProductionYear = searchResult.ProductionYear
                },
                HasMetadata = !string.IsNullOrWhiteSpace(searchResult.Name)
            };

            foreach (var (key, value) in searchResult.ProviderIds)
            {
                result.Item.SetProviderId(key, value);
            }

            if (!string.IsNullOrWhiteSpace(searchResult.ImageUrl))
            {
                result.RemoteImages.Add((searchResult.ImageUrl, ImageType.Primary));
            }

            return result;
        }

        private static string NormalizeIsbn(string? isbn)
        {
            return string.IsNullOrWhiteSpace(isbn)
                ? string.Empty
                : Regex.Replace(isbn, @"[^0-9Xx]", string.Empty, RegexOptions.CultureInvariant).ToUpperInvariant();
        }

        private static string? ExtractIsbn(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var candidate = NormalizeIsbn(value);
            return candidate.Length is 10 or 13 ? candidate : null;
        }

        private static string? ExtractOpenLibraryId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = Regex.Match(value, @"OL\d+[MW]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Value.ToUpperInvariant() : null;
        }

        private static RemoteSearchResult? SelectBestTitleMatch(IEnumerable<RemoteSearchResult> results, string title)
        {
            var candidates = results.ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            var requested = NormalizeTitleForMatching(title);
            var requestedTokens = requested.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet(StringComparer.Ordinal);
            if (requestedTokens.Count == 0)
            {
                return candidates.FirstOrDefault();
            }

            return candidates
                .Select(result =>
                {
                    var candidate = NormalizeTitleForMatching(result.Name ?? string.Empty);
                    var candidateTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .ToHashSet(StringComparer.Ordinal);
                    var overlap = requestedTokens.Count(token => candidateTokens.Contains(token));
                    var score = (2d * overlap) / (requestedTokens.Count + candidateTokens.Count);
                    if (string.Equals(requested, candidate, StringComparison.Ordinal))
                    {
                        score += 2d;
                    }
                    else if (requested.Contains(candidate, StringComparison.Ordinal)
                        || candidate.Contains(requested, StringComparison.Ordinal))
                    {
                        score += 1d;
                    }

                    return (Result: result, Score: score);
                })
                .OrderByDescending(candidate => candidate.Score)
                .Select(candidate => candidate.Result)
                .FirstOrDefault();
        }

        private static string NormalizeTitleForMatching(string value)
        {
            var withoutDiacritics = RemoveDiacritics(value).ToLowerInvariant();
            var alphanumeric = Regex.Replace(withoutDiacritics, @"[^\p{L}\p{N}]+", " ", RegexOptions.CultureInvariant);
            return Regex.Replace(alphanumeric, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
        }

        private static IEnumerable<string> BuildSearchQueries(string query)
        {
            var clean = NormalizeSearchText(query);
            var withoutDiacritics = RemoveDiacritics(clean);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Library filenames often contain a collection/edition prefix, for example
            // "D&D 3.5 - Livro Cityscape". Search the human book title from the end first.
            var segments = Regex.Split(query, @"\s*[-–—]\s*", RegexOptions.CultureInvariant);
            for (var index = segments.Length - 1; index >= 0; index--)
            {
                var candidate = segments[index].Trim();
                candidate = Regex.Replace(candidate, @"^(?:livro|book|ebook)\s+", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
                if (candidate.Length > 2 && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (var candidate in new[] { query, clean, withoutDiacritics })
            {
                if (!string.IsNullOrWhiteSpace(candidate) && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }

            foreach (var part in clean.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 2)
                {
                    if (seen.Add(part)) yield return part;
                    var normalizedPart = RemoveDiacritics(part);
                    if (seen.Add(normalizedPart)) yield return normalizedPart;
                }
            }

            var punctuationFree = Regex.Replace(withoutDiacritics, @"[^\p{L}\p{N}\s]+", " ", RegexOptions.CultureInvariant);
            punctuationFree = Regex.Replace(punctuationFree, @"\s{2,}", " ", RegexOptions.CultureInvariant).Trim();
            if (!string.IsNullOrWhiteSpace(punctuationFree))
            {
                if (seen.Add(punctuationFree)) yield return punctuationFree;
            }
        }

        private static string NormalizeSearchText(string value)
        {
            var normalized = Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant).Trim();
            normalized = Regex.Replace(normalized, @"\s*[-–—]\s*", " ", RegexOptions.CultureInvariant).Trim();
            return normalized;
        }

        private static string RemoveDiacritics(string text)
        {
            var normalized = text.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        private static void AddSearchResultIfMissing(ICollection<RemoteSearchResult> results, RemoteSearchResult item)
        {
            var openLibraryId = item.GetProviderId("OpenLibrary");
            var isbn = item.GetProviderId("ISBN");
            if (results.Any(existing =>
                    (!string.IsNullOrWhiteSpace(openLibraryId) && string.Equals(existing.GetProviderId("OpenLibrary"), openLibraryId, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(isbn) && string.Equals(existing.GetProviderId("ISBN"), isbn, StringComparison.OrdinalIgnoreCase))
                    || string.Equals(existing.Name, item.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            results.Add(item);
        }

        private void PopulateMetadata(MetadataResult<Book> result, JsonElement bookEntry)
        {
            result.Item = new Book();
            result.HasMetadata = true;

            if (bookEntry.TryGetProperty("title", out var title))
            {
                result.Item.Name = title.GetString() ?? string.Empty;
            }

            if (bookEntry.TryGetProperty("subtitle", out var subtitle))
            {
                var sub = subtitle.GetString();
                if (!string.IsNullOrWhiteSpace(sub))
                {
                    result.Item.Name = $"{result.Item.Name}: {sub}";
                }
            }

            if (bookEntry.TryGetProperty("publish_date", out var pubDate))
            {
                var dateStr = pubDate.GetString();
                if (!string.IsNullOrWhiteSpace(dateStr))
                {
                    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    {
                        result.Item.PremiereDate = date;
                        result.Item.ProductionYear = date.Year;
                    }
                    else if (int.TryParse(dateStr, out var year))
                    {
                        result.Item.ProductionYear = year;
                    }
                }
            }

            if (bookEntry.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array)
            {
                foreach (var author in authors.EnumerateArray())
                {
                    if (author.ValueKind == JsonValueKind.Object && author.TryGetProperty("name", out var authorName))
                    {
                        var name = authorName.GetString();
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.AddPerson(new PersonInfo { Name = name, Type = PersonKind.Author });
                        }
                    }
                }
            }

            if (bookEntry.TryGetProperty("publishers", out var publishers) && publishers.ValueKind == JsonValueKind.Array)
            {
                foreach (var publisher in publishers.EnumerateArray())
                {
                    var publisherName = publisher.ValueKind == JsonValueKind.Object && publisher.TryGetProperty("name", out var publisherObjectName)
                        ? publisherObjectName.GetString()
                        : publisher.ValueKind == JsonValueKind.String ? publisher.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(publisherName))
                    {
                        result.Item.AddStudio(publisherName);
                    }
                }
            }

            if (bookEntry.TryGetProperty("subjects", out var subjects) && subjects.ValueKind == JsonValueKind.Array)
            {
                foreach (var subject in subjects.EnumerateArray())
                {
                    var subjectName = subject.ValueKind == JsonValueKind.Object && subject.TryGetProperty("name", out var subjectObjectName)
                        ? subjectObjectName.GetString()
                        : subject.ValueKind == JsonValueKind.String ? subject.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(subjectName))
                    {
                        result.Item.AddGenre(subjectName);
                    }
                }
            }

            if (bookEntry.TryGetProperty("number_of_pages", out var pages))
            {
                result.Item.RunTimeTicks = TimeSpan.FromMinutes(pages.GetInt32()).Ticks;
            }

            if (bookEntry.TryGetProperty("identifiers", out var identifiers))
            {
                SetIdentifierFromArray(result.Item, identifiers, "isbn_13", "ISBN");
                SetIdentifierFromArray(result.Item, identifiers, "isbn_10", "ISBN");
                SetIdentifierFromArray(result.Item, identifiers, "openlibrary", "OpenLibrary");
                SetIdentifierFromArray(result.Item, identifiers, "google", "GoogleBooks");
            }

            SetIdentifierFromArray(result.Item, bookEntry, "isbn_13", "ISBN");
            SetIdentifierFromArray(result.Item, bookEntry, "isbn_10", "ISBN");

            if (bookEntry.TryGetProperty("key", out var bookKey) && bookKey.ValueKind == JsonValueKind.String)
            {
                var id = ExtractOpenLibraryId(bookKey.GetString());
                if (!string.IsNullOrWhiteSpace(id)) result.Item.SetProviderId("OpenLibrary", id);
            }

            if (bookEntry.TryGetProperty("cover", out var cover))
            {
                if (cover.TryGetProperty("large", out var largeCover))
                {
                    var coverUrl = largeCover.GetString();
                    if (!string.IsNullOrWhiteSpace(coverUrl))
                    {
                        result.RemoteImages.Add((coverUrl, ImageType.Primary));
                    }
                }
                else if (cover.TryGetProperty("medium", out var mediumCover))
                {
                    var coverUrl = mediumCover.GetString();
                    if (!string.IsNullOrWhiteSpace(coverUrl))
                    {
                        result.RemoteImages.Add((coverUrl, ImageType.Primary));
                    }
                }
            }

            if (result.RemoteImages.Count == 0 && bookEntry.TryGetProperty("covers", out var covers) && covers.ValueKind == JsonValueKind.Array)
            {
                var coverId = covers.EnumerateArray()
                    .Where(value => value.ValueKind == JsonValueKind.Number)
                    .Select(value => value.GetInt32())
                    .FirstOrDefault(id => id > 0);
                if (coverId > 0)
                {
                    result.RemoteImages.Add(($"https://covers.openlibrary.org/b/id/{coverId}-L.jpg", ImageType.Primary));
                }
            }
        }

        private static void SetIdentifierFromArray(Book item, JsonElement source, string propertyName, string providerName)
        {
            if (source.TryGetProperty(propertyName, out var values)
                && values.ValueKind == JsonValueKind.Array
                && values.GetArrayLength() > 0
                && values[0].ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(values[0].GetString())
                && string.IsNullOrWhiteSpace(item.GetProviderId(providerName)))
            {
                item.SetProviderId(providerName, values[0].GetString()!);
            }
        }

        private async Task PopulateEditionAuthors(MetadataResult<Book> result, JsonElement edition, CancellationToken cancellationToken)
        {
            if (result.Item is null
                || !edition.TryGetProperty("authors", out var authors)
                || authors.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var authorIds = authors.EnumerateArray()
                .Where(author => author.ValueKind == JsonValueKind.Object
                    && author.TryGetProperty("key", out var key)
                    && key.ValueKind == JsonValueKind.String)
                .Select(author => Regex.Match(author.GetProperty("key").GetString() ?? string.Empty, @"OL\d+A", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                .Where(match => match.Success)
                .Select(match => match.Value.ToUpperInvariant())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var authorId in authorIds)
            {
                try
                {
                    using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                        .GetAsync($"https://openlibrary.org/authors/{Uri.EscapeDataString(authorId!)}.json", cancellationToken)
                        .ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
                    if (document.RootElement.TryGetProperty("name", out var name)
                        && !string.IsNullOrWhiteSpace(name.GetString()))
                    {
                        result.AddPerson(new PersonInfo { Name = name.GetString()!, Type = PersonKind.Author });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Unable to load OpenLibrary author {AuthorId}", authorId);
                }
            }
        }

        private void PopulateMetadataFromWork(MetadataResult<Book> result, JsonElement workEntry)
        {
            result.Item = new Book();
            result.HasMetadata = true;

            if (workEntry.TryGetProperty("key", out var key))
            {
                var openLibraryId = key.GetString()?.Replace("/works/", string.Empty, StringComparison.Ordinal);
                if (!string.IsNullOrWhiteSpace(openLibraryId))
                {
                    result.Item.SetProviderId("OpenLibrary", openLibraryId);
                }
            }

            if (workEntry.TryGetProperty("title", out var title))
            {
                result.Item.Name = title.GetString() ?? string.Empty;
            }

            if (workEntry.TryGetProperty("description", out var description))
            {
                if (description.ValueKind == JsonValueKind.String)
                {
                    var value = description.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Item.Overview = value;
                    }
                }
                else if (description.ValueKind == JsonValueKind.Object
                    && description.TryGetProperty("value", out var descriptionValue))
                {
                    var value = descriptionValue.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        result.Item.Overview = value;
                    }
                }
            }

            if (workEntry.TryGetProperty("first_publish_date", out var firstPublishDate))
            {
                var dateStr = firstPublishDate.GetString();
                if (!string.IsNullOrWhiteSpace(dateStr)
                    && DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    result.Item.PremiereDate = date;
                    result.Item.ProductionYear = date.Year;
                }
            }

            if (workEntry.TryGetProperty("subjects", out var subjects))
            {
                foreach (var subject in subjects.EnumerateArray())
                {
                    var subjectName = subject.GetString();
                    if (!string.IsNullOrWhiteSpace(subjectName))
                    {
                        result.Item.AddGenre(subjectName);
                    }
                }
            }

            if (workEntry.TryGetProperty("covers", out var covers) && covers.ValueKind == JsonValueKind.Array)
            {
                var coverId = covers.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Number)
                    .Select(item => item.GetInt32())
                    .FirstOrDefault(id => id > 0);

                if (coverId > 0)
                {
                    result.RemoteImages.Add(($"https://covers.openlibrary.org/b/id/{coverId}-L.jpg", ImageType.Primary));
                }
            }
        }

        private async Task AddEditionImagesFromWork(MetadataResult<Book> result, string workId, CancellationToken cancellationToken)
        {
            var url = $"https://openlibrary.org/works/{Uri.EscapeDataString(workId)}/editions.json?limit=25";

            try
            {
                using var response = await _httpClientFactory.CreateClient(NamedClient.Default)
                    .GetAsync(url, cancellationToken)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                using var document = JsonDocument.Parse(json);
                if (!document.RootElement.TryGetProperty("entries", out var entries)
                    || entries.ValueKind != JsonValueKind.Array)
                {
                    return;
                }

                foreach (var edition in entries.EnumerateArray())
                {
                    AddEditionImages(result, edition);

                    if (result.RemoteImages.Count > 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error fetching OpenLibrary editions for work {Id}", workId);
            }
        }

        private static void AddEditionImages(MetadataResult<Book> result, JsonElement edition)
        {
            if (edition.TryGetProperty("covers", out var covers) && covers.ValueKind == JsonValueKind.Array)
            {
                var coverId = covers.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Number)
                    .Select(item => item.GetInt32())
                    .FirstOrDefault(id => id > 0);

                if (coverId > 0)
                {
                    AddRemoteImageIfMissing(result, $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg");
                    return;
                }
            }

            if (edition.TryGetProperty("cover_i", out var coverIdElement)
                && coverIdElement.ValueKind == JsonValueKind.Number)
            {
                var coverId = coverIdElement.GetInt32();
                if (coverId > 0)
                {
                    AddRemoteImageIfMissing(result, $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg");
                    return;
                }
            }

            if (edition.TryGetProperty("isbn_13", out var isbn13)
                && isbn13.ValueKind == JsonValueKind.Array
                && isbn13.GetArrayLength() > 0)
            {
                var isbn = isbn13[0].GetString();
                if (!string.IsNullOrWhiteSpace(isbn))
                {
                    result.Item?.SetProviderId("ISBN", isbn);
                }
            }
            else if (edition.TryGetProperty("isbn_10", out var isbn10)
                && isbn10.ValueKind == JsonValueKind.Array
                && isbn10.GetArrayLength() > 0)
            {
                var isbn = isbn10[0].GetString();
                if (!string.IsNullOrWhiteSpace(isbn))
                {
                    result.Item?.SetProviderId("ISBN", isbn);
                }
            }
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(url, cancellationToken);
        }

        private void AddRemoteImages(ICollection<RemoteImageInfo> images, MetadataResult<Book> metadata)
        {
            foreach (var remoteImage in metadata.RemoteImages.Where(image => image.Type == ImageType.Primary))
            {
                AddImageIfMissing(images, remoteImage.Url);
            }
        }

        private void AddImageIfMissing(ICollection<RemoteImageInfo> images, string? url)
        {
            if (string.IsNullOrWhiteSpace(url)
                || images.Any(image => string.Equals(image.Url, url, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            images.Add(new RemoteImageInfo
            {
                ProviderName = Name,
                Url = url,
                ThumbnailUrl = url.Replace("-L.jpg", "-M.jpg", StringComparison.OrdinalIgnoreCase),
                Type = ImageType.Primary
            });
        }

        private static void AddOpenLibraryFallbackImages(MetadataResult<Book> result, string? isbn, string? openLibraryId)
        {
            if (result.RemoteImages.Count > 0)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(openLibraryId)
                && !openLibraryId.EndsWith('W'))
            {
                AddRemoteImageIfMissing(result, $"https://covers.openlibrary.org/b/olid/{Uri.EscapeDataString(openLibraryId)}-L.jpg?default=false");
            }

            if (!string.IsNullOrWhiteSpace(isbn))
            {
                AddRemoteImageIfMissing(result, $"https://covers.openlibrary.org/b/isbn/{Uri.EscapeDataString(isbn)}-L.jpg?default=false");
            }
        }

        private static void AddRemoteImageIfMissing(MetadataResult<Book> result, string url)
        {
            if (!result.RemoteImages.Any(image => string.Equals(image.Url, url, StringComparison.OrdinalIgnoreCase)))
            {
                result.RemoteImages.Add((url, ImageType.Primary));
            }
        }

        private static void MergeSupplementalMetadata(MetadataResult<Book> target, MetadataResult<Book> source)
        {
            if (!target.HasMetadata || !source.HasMetadata || target.Item is null || source.Item is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(target.Item.Overview))
            {
                target.Item.Overview = source.Item.Overview;
            }

            target.Item.PremiereDate ??= source.Item.PremiereDate;
            target.Item.ProductionYear ??= source.Item.ProductionYear;

            foreach (var providerId in source.Item.ProviderIds)
            {
                if (!target.Item.ProviderIds.ContainsKey(providerId.Key))
                {
                    target.Item.SetProviderId(providerId.Key, providerId.Value);
                }
            }

            foreach (var genre in source.Item.Genres)
            {
                target.Item.AddGenre(genre);
            }

            foreach (var studio in source.Item.Studios)
            {
                target.Item.AddStudio(studio);
            }

            foreach (var person in source.People ?? [])
            {
                target.AddPerson(person);
            }

            foreach (var image in source.RemoteImages)
            {
                if (!target.RemoteImages.Any(existing => string.Equals(existing.Url, image.Url, StringComparison.OrdinalIgnoreCase)))
                {
                    target.RemoteImages.Add(image);
                }
            }
        }

        private static string NormalizeOpenLibraryId(string olId)
        {
            return ExtractOpenLibraryId(olId) ?? olId.Trim();
        }

        private class OpenLibrarySearchResult
        {
            [JsonPropertyName("docs")]
            public OpenLibraryDoc[]? Docs { get; set; }
        }

        private class OpenLibraryDoc
        {
            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("key")]
            public string? Key { get; set; }

            [JsonPropertyName("author_name")]
            public string[]? AuthorName { get; set; }

            [JsonPropertyName("first_publish_year")]
            public int FirstPublishYear { get; set; }

            [JsonPropertyName("isbn")]
            public string[]? Isbn { get; set; }

            [JsonPropertyName("cover_edition_key")]
            public string? CoverEditionKey { get; set; }

            [JsonPropertyName("cover_i")]
            public int CoverId { get; set; }
        }
    }
}
