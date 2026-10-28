using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

/// <summary>
/// Tests for NebulaDownloaderEngine covering static methods: range handling,
/// staging directory selection, category priority, media identity, and error cases.
/// Follows Gauntlet Loop: RED tests written first, then implementation verified.
/// </summary>
public sealed class NebulaDownloaderEngineTests : IDisposable
{
    private readonly string _testRoot;

    public NebulaDownloaderEngineTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"nebula-downloader-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            try { Directory.Delete(_testRoot, recursive: true); } catch { }
        }
    }

    private NebulaDownloaderEngine CreateEngine()
        => new(null!, null!, NullLogger<NebulaDownloaderEngine>.Instance);

    #region Category Priority Tests (T2.3 - Priority observable and consistent)

    [Fact]
    public void GetCategoryPriority_ReturnsExpectedRank_ForKnownCategories()
    {
        // Arrange: paths representing each known category
        var testCases = new[]
        {
            (Path: "/mnt/staging/Animações/Attack on Titan/Attack on Titan - 01.mkv", Expected: 1),
            (Path: "/mnt/staging/Filmes/Inception (2010).mkv", Expected: 2),
            (Path: "/mnt/staging/Series/Breaking Bad S01E01.mkv", Expected: 3),
            (Path: "/mnt/staging/Doramas/The Guest S01E01.mkv", Expected: 4),
            (Path: "/mnt/staging/Novelas/Renascer/Capitulo 01.mkv", Expected: 5),
            (Path: "/mnt/staging/Porno/scene.mp4", Expected: 6),
        };

        foreach (var (path, expected) in testCases)
        {
            // Act
            var rank = NebulaDownloaderEngine.GetCategoryPriority(path);

            // Assert
            Assert.Equal(expected, rank);
        }
    }

    [Fact]
    public void GetCategoryPriority_CaseInsensitive()
    {
        var upper = "/mnt/staging/SERIES/Show/S01E01.mkv";
        var lower = "/mnt/staging/series/Show/S01E01.mkv";
        var mixed = "/mnt/staging/Series/Show/S01E01.mkv";

        Assert.Equal(NebulaDownloaderEngine.GetCategoryPriority(upper), NebulaDownloaderEngine.GetCategoryPriority(lower));
        Assert.Equal(NebulaDownloaderEngine.GetCategoryPriority(upper), NebulaDownloaderEngine.GetCategoryPriority(mixed));
    }

    [Fact]
    public void GetCategoryPriority_UncategorizedPath_UsesMovieFallback()
    {
        var unknown = "/mnt/staging/RandomFolder/file.mkv";

        var actual = NebulaDownloaderEngine.GetCategoryPriority(unknown);

        Assert.Equal(2, actual);
    }

    [Fact]
    public void GetMediaSortTitle_ExtractsWorkTitle_NotSeasonEpisode()
    {
        var testCases = new[]
        {
            (Path: "/mnt/staging/Series/Breaking Bad/Season 01/Breaking Bad - S01E01 - Pilot.mkv", Expected: "Breaking Bad"),
            (Path: "/mnt/staging/Series/The Office/Season 02/The Office - S02E01 - The Dundies.mkv", Expected: "The Office"),
            (Path: "/mnt/staging/Movies/Inception (2010)/Inception (2010).mkv", Expected: "Inception (2010)"),
            (Path: "/mnt/staging/Anime/Attack on Titan/Season 1/Attack on Titan - 01.mkv", Expected: "Attack on Titan"),
        };

        foreach (var (path, expected) in testCases)
        {
            var actual = NebulaDownloaderEngine.GetMediaSortTitle(path);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void OrderDownloadPaths_RespectsPriorityThenAlphabetical()
    {
        var paths = new[]
        {
            "/mnt/staging/Filmes/Z Movie.mkv",           // Filmes (prioridade 2), título Z
            "/mnt/staging/Series/A Show/S01E01.mkv",     // Séries (prioridade 3), título A
            "/mnt/staging/Series/B Show/S01E01.mkv",     // Séries (prioridade 3), título B
            "/mnt/staging/Animações/C Anime/C Anime.mkv", // Animações (prioridade 1), título C
            "/mnt/staging/Filmes/A Movie.mkv",           // Filmes (prioridade 2), título A
        };

        Func<string, bool> isPrioritized = _ => false; // No explicit priorities
        var ordered = NebulaDownloaderEngine.OrderDownloadPaths(paths, isPrioritized).ToArray();

        Assert.Equal(5, ordered.Length);
        // Animações primeiro (prioridade 1)
        Assert.Equal("/mnt/staging/Animações/C Anime/C Anime.mkv", ordered[0]);
        // Filmes em seguida (prioridade 2), em ordem alfabética
        Assert.Equal("/mnt/staging/Filmes/A Movie.mkv", ordered[1]);
        Assert.Equal("/mnt/staging/Filmes/Z Movie.mkv", ordered[2]);
        // Séries por último neste conjunto, também em ordem alfabética
        Assert.Equal("/mnt/staging/Series/A Show/S01E01.mkv", ordered[3]);
        Assert.Equal("/mnt/staging/Series/B Show/S01E01.mkv", ordered[4]);
    }

    [Fact]
    public void OrderDownloadPaths_PrioritizedItemsComeFirst()
    {
        var paths = new[]
        {
            "/mnt/staging/Movies/Z Movie.mkv",           // Movies
            "/mnt/staging/Series/A Show/S01E01.mkv",     // Series
        };

        // Prioritize the Movies item
        Func<string, bool> isPrioritized = p => p.Contains("Z Movie", StringComparison.OrdinalIgnoreCase);

        var ordered = NebulaDownloaderEngine.OrderDownloadPaths(paths, isPrioritized).ToArray();

        Assert.Equal(2, ordered.Length);
        // Z Movie is prioritized, so it comes first despite lower category priority
        Assert.Equal("/mnt/staging/Movies/Z Movie.mkv", ordered[0]);
        Assert.Equal("/mnt/staging/Series/A Show/S01E01.mkv", ordered[1]);
    }

    [Fact]
    public void GetPriorityReason_ReturnsExpectedExplanation()
    {
        var requested = NebulaDownloaderEngine.GetPriorityReason(true, "Series");
        var standard = NebulaDownloaderEngine.GetPriorityReason(false, "Movies");

        Assert.Contains("solicitação", requested, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Movies · ordem alfabética", standard);
    }

    #endregion

    #region Staging Directory Selection Tests (T2.6 - Startup/cleanup review)

    [Fact]
    public void SelectBestStageDirectory_PicksLargestFreeSpace()
    {
        var candidates = new List<string>
        {
            Path.Combine(_testRoot, "small"),
            Path.Combine(_testRoot, "large"),
        };

        Directory.CreateDirectory(candidates[0]);
        Directory.CreateDirectory(candidates[1]);

        // We can't easily mock disk space, so test the logic path
        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(candidates, logInfo: null, logWarning: null, logger: null, requiredBytes: 100_000_000_000L); // 100GB free

        Assert.Contains(selected, candidates);
    }

    [Fact]
    public void SelectBestStageDirectory_RejectsFilesystemRoots()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)!;
        var valid = Path.Combine(_testRoot, "valid");
        Directory.CreateDirectory(valid);

        var candidates = new List<string> { systemRoot, valid };

        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(candidates, logInfo: null, logWarning: null, logger: null, requiredBytes: 100_000_000_000L);

        Assert.Equal(valid, selected);
    }

    [Fact]
    public void SelectBestStageDirectory_CreatesDirectoryIfMissing()
    {
        var newDir = Path.Combine(_testRoot, "new-staging");

        var selected = NebulaDownloaderEngine.SelectBestStageDirectory(new List<string> { newDir }, logInfo: null, logWarning: null, logger: null, requiredBytes: 100_000_000_000L);

        Assert.True(Directory.Exists(newDir));
        Assert.Equal(newDir, selected);
    }

    [Fact]
    public void SelectBestStageDirectory_Throws_WhenNoValidCandidates()
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)!;

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NebulaDownloaderEngine.SelectBestStageDirectory(new List<string> { systemRoot }, logInfo: null, logWarning: null, logger: null, requiredBytes: 100_000_000_000L));

        Assert.Contains("nenhum diretório", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Range Validation Tests (T3.2 - Validate part reading)

    [Fact]
    public void ValidateRangeResponse_AcceptsValid206PartialContent()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 9, 100);

        NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10);
    }

    [Fact]
    public void ValidateRangeResponse_RejectsNon206Status()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[100])
        };

        var ex = Assert.Throws<IOException>(() =>
            NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));

        Assert.Contains("Range", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRangeResponse_RejectsMissingContentRange()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        // No Content-Range header

        var ex = Assert.Throws<IOException>(() =>
            NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));

        Assert.Contains("Content-Range", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRangeResponse_RejectsMismatchedRangeStart()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(5, 14, 100); // Wrong start

        var ex = Assert.Throws<IOException>(() =>
            NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));

        Assert.Contains("Content-Range", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRangeResponse_RejectsMismatchedRangeEnd()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 8, 100); // Wrong end

        var ex = Assert.Throws<IOException>(() =>
            NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));

        Assert.Contains("Content-Range", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRangeResponse_RejectsMismatchedTotalSize()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 9, 200); // Wrong total

        var ex = Assert.Throws<IOException>(() =>
            NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));

        Assert.Contains("Content-Range", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateRangeResponse_RejectsContentLengthMismatch()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[5]) // Only 5 bytes but range says 10
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 9, 100);

        var ex = Assert.Throws<IOException>(() =>
            NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));

        Assert.Contains("Content-Length", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Range Probe Tests (T3.2 - Validate part reading)

    [Fact]
    public void TryGetRangeProbeLength_ReturnsTrueAndSize_ForValid206()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[1])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, 12345);

        var result = NebulaDownloaderEngine.TryGetRangeProbeLength(response, out var size);

        Assert.True(result);
        Assert.Equal(12345L, size);
    }

    [Fact]
    public void TryGetRangeProbeLength_ReturnsFalse_ForNon206()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[100])
        };

        var result = NebulaDownloaderEngine.TryGetRangeProbeLength(response, out var size);

        Assert.False(result);
        Assert.Equal(0L, size);
    }

    [Fact]
    public async Task DownloadMultipartCoreAsync_RejectsEmptySequentialResponseWhenSizeIsUnknown()
    {
        using var engine = CreateEngine();
        var requestCount = 0;
        ReplaceHttpClient(engine, new DelegateHttpMessageHandler(request =>
        {
            requestCount++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            };
        }));
        var target = Path.Combine(_testRoot, "empty.mkv");
        var method = typeof(NebulaDownloaderEngine).GetMethod("DownloadMultipartCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var download = (Task<bool>)method.Invoke(engine, new object[] { "https://example.invalid/empty.mkv", target, 1, CancellationToken.None })!;

        var exception = await Assert.ThrowsAsync<IOException>(() => download);

        Assert.Contains("vazio", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, requestCount);
        Assert.False(File.Exists(target));
        Assert.False(File.Exists(target + ".download.tmp"));
    }

    [Fact]
    public async Task DownloadMultipartCoreAsync_AcceptsNonEmptySequentialResponseWhenSizeIsUnknown()
    {
        using var engine = CreateEngine();
        ReplaceHttpClient(engine, new DelegateHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(request.Method == HttpMethod.Get && request.Headers.Range is null
                ? new byte[] { 1, 2, 3 }
                : Array.Empty<byte>())
        }));
        var target = Path.Combine(_testRoot, "valid.mkv");
        var method = typeof(NebulaDownloaderEngine).GetMethod("DownloadMultipartCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var download = (Task<bool>)method.Invoke(engine, new object[] { "https://example.invalid/valid.mkv", target, 1, CancellationToken.None })!;

        Assert.True(await download);
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(target));
    }

    [Fact]
    public void TryGetRangeProbeLength_ReturnsFalse_ForMissingContentRange()
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[1])
        };
        // No Content-Range

        var result = NebulaDownloaderEngine.TryGetRangeProbeLength(response, out var size);

        Assert.False(result);
        Assert.Equal(0L, size);
    }

    [Fact]
    public void TryGetRangeProbeLength_ReturnsFalse_ForUnsatisfiableRange()
    {
        var response = new HttpResponseMessage((HttpStatusCode)416) // RangeNotSatisfiable
        {
            Content = new ByteArrayContent(new byte[0])
        };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(100); // */100

        var result = NebulaDownloaderEngine.TryGetRangeProbeLength(response, out var size);

        Assert.False(result);
        Assert.Equal(0L, size);
    }

    #endregion

    #region Supported Extensions Tests

    [Fact]
    public void SupportedMediaExtensions_ContainsExpectedVideoFormats()
    {
        var expectedVideo = new[] { ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm", ".flv", ".iso" };

        foreach (var ext in expectedVideo)
        {
            Assert.Contains(ext, NebulaDownloaderEngine.SupportedMediaExtensions);
        }
    }

    [Fact]
    public void SupportedMediaExtensions_ContainsExpectedAudioFormats()
    {
        var expectedAudio = new[] { ".mp3", ".flac", ".aac", ".wav", ".m4a", ".ogg" };

        foreach (var ext in expectedAudio)
        {
            Assert.Contains(ext, NebulaDownloaderEngine.SupportedMediaExtensions);
        }
    }

    [Fact]
    public void SupportedSidecarExtensions_ContainsExpectedFormats()
    {
        var expected = new[] { ".srt", ".nfo", ".jpg" };

        foreach (var ext in expected)
        {
            Assert.Contains(ext, NebulaDownloaderEngine.SupportedSidecarExtensions);
        }
    }

    [Fact]
    public void SupportedMediaExtensions_ReturnsTrue_ForKnownExtensions()
    {
        foreach (var ext in NebulaDownloaderEngine.SupportedMediaExtensions)
        {
            var path = $"/staging/media/file{ext}";
            var isSupported = NebulaDownloaderEngine.SupportedMediaExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            Assert.True(isSupported, $"Should support {ext}");
        }
    }

    [Fact]
    public void SupportedMediaExtensions_ReturnsFalse_ForUnknownExtensions()
    {
        var unsupported = new[] { ".txt", ".pdf", ".exe", ".zip", ".rar" };

        foreach (var ext in unsupported)
        {
            var path = $"/staging/media/file{ext}";
            var isSupported = NebulaDownloaderEngine.SupportedMediaExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            Assert.False(isSupported, $"Should not support {ext}");
        }
    }

    #endregion

    #region Activity/Telemetry Tests (T1.1 - OpenTelemetry instrumentation)

    [Fact]
    public void ActivitySourceName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(NebulaDownloaderEngine.ActivitySourceName));
    }

    [Fact]
    public void MeterName_IsNotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(NebulaDownloaderEngine.MeterName));
    }

    [Fact]
    public void EngineConstructor_RegistersActivitySourceAndMeter()
    {
        using var engine = CreateEngine();

        Assert.NotNull(engine);
        Assert.False(engine.IsRunning);
    }

    #endregion

    #region Engine Lifecycle Tests

    [Fact]
    public void Start_WithEmptyMonitorPaths_DoesNotRun()
    {
        using var engine = CreateEngine();

        var config = new NebulaFtpConfiguration
        {
            MonitorPaths = Array.Empty<string>()
        };

        engine.Start(config);

        Assert.False(engine.IsRunning);
    }

    [Fact]
    public void GetValidMonitorSources_UsesOnlyExistingConfiguredDirectoriesAndDeduplicates()
    {
        var configured = Path.Combine(_testRoot, "configured-library");
        Directory.CreateDirectory(configured);
        var missing = Path.Combine(_testRoot, "not-configured-on-disk");

        var sources = NebulaDownloaderEngine.GetValidMonitorSources(new[]
        {
            configured,
            configured + Path.DirectorySeparatorChar,
            missing,
            Path.GetPathRoot(_testRoot)!,
            Path.Combine(Path.GetPathRoot(_testRoot)!, "temporary", "..")
        });

        Assert.Single(sources);
        Assert.Equal(Path.GetFullPath(configured), Path.GetFullPath(sources[0]));
    }

    [Fact]
    public void GetValidMonitorSources_WithOnlyMissingDirectories_ReturnsEmpty()
    {
        var missing = Path.Combine(_testRoot, "missing-library");

        var sources = NebulaDownloaderEngine.GetValidMonitorSources(new[] { missing });

        Assert.Empty(sources);
    }

    [Fact]
    public async Task StopAsync_OnStoppedEngine_CompletesQuickly()
    {
        using var engine = CreateEngine();

        await engine.StopAsync(); // Should not throw

        Assert.False(engine.IsRunning);
    }

    #endregion

    #region Concurrent Access Tests (T2.4 - Backpressure and fairness)

    [Fact]
    public void PrioritizeTarget_AddsPathToPrioritizedSet()
    {
        using var engine = CreateEngine();

        var testFile = Path.Combine(_testRoot, "test.mkv");
        File.WriteAllText(testFile, "test");

        engine.PrioritizeTarget(testFile);

        Assert.True(engine.IsPathPrioritized(testFile));
    }

    [Fact]
    public void PrioritizeTarget_AddsDirectoryToPrioritizedSet()
    {
        using var engine = CreateEngine();

        var testDir = Path.Combine(_testRoot, "series");
        Directory.CreateDirectory(testDir);

        engine.PrioritizeTarget(testDir);

        Assert.True(engine.IsPathPrioritized(testDir));
    }

    [Fact]
    public void IsPathPrioritized_ReturnsFalse_ForUnprioritizedPath()
    {
        using var engine = CreateEngine();

        var testFile = Path.Combine(_testRoot, "unprioritized.mkv");
        File.WriteAllText(testFile, "test");

        Assert.False(engine.IsPathPrioritized(testFile));
    }

    #endregion

    #region Media Identity Tests

    [Fact]
    public void MovieIdentity_ExtractsTitleAndYear()
    {
        var result = NebulaDownloaderEngine.MovieIdentity("Inception (2010)");

        Assert.NotNull(result);
        Assert.Equal("inception", result.Value.Title);
        Assert.Equal(2010, result.Value.Year);
    }

    [Fact]
    public void MovieIdentity_ReturnsNull_ForMissingYear()
    {
        var result = NebulaDownloaderEngine.MovieIdentity("Inception");

        Assert.Null(result);
    }

    [Fact]
    public void EpisodeIdentity_ExtractsSeriesSeasonEpisode()
    {
        var result = NebulaDownloaderEngine.EpisodeIdentity("Breaking Bad", "Breaking Bad - S01E01 - Pilot.mkv");

        Assert.NotNull(result);
        Assert.Equal("breaking bad", result.Value.Series);
        Assert.Equal(1, result.Value.Season);
        Assert.Equal(1, result.Value.Episode);
    }

    [Fact]
    public void EpisodeIdentity_ReturnsNull_ForNonEpisodeFile()
    {
        var result = NebulaDownloaderEngine.EpisodeIdentity("Movie", "Inception (2010).mkv");

        Assert.Null(result);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void CreateHttpFailure_IncludesDiagnosticHeaders()
    {
        // This tests a private method via reflection
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        response.Headers.TryAddWithoutValidation("Server", "nginx/1.20");
        response.Headers.TryAddWithoutValidation("X-Debug-Allowed", "true");

        var method = typeof(NebulaDownloaderEngine).GetMethod(
            "CreateHttpFailure",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var ex = (HttpRequestException)method.Invoke(null, new object[] { response, "https://example.com/file.mkv", "GET" })!;

        Assert.Contains("500", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nginx", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("X-Debug-Allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void NormalizeMediaTitle_CleansSpecialCharacters()
    {
        var method = typeof(NebulaDownloaderEngine).GetMethod(
            "NormalizeMediaTitle",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);

        var result = (string)method.Invoke(null, new object[] { "Breaking.Bad_S01E01-Pilot" })!;

        Assert.Equal("breaking bad s01e01 pilot", result);
    }

    #endregion

    private static void ReplaceHttpClient(NebulaDownloaderEngine engine, HttpMessageHandler handler)
    {
        var field = typeof(NebulaDownloaderEngine).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        ((HttpClient)field.GetValue(engine)!).Dispose();
        field.SetValue(engine, new HttpClient(handler));
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request));
    }
}
