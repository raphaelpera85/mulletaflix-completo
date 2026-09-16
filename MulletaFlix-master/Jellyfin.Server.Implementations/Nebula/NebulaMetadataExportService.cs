#pragma warning disable CA1707

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Exports metadata from recognized Nebula items on N: into the upload staging tree.
/// </summary>
public sealed class NebulaMetadataExportService : IHostedService, IDisposable
{
    internal const string PendingMarkerFileName = ".nebula-metadata-pending";

    internal static readonly HashSet<string> MetadataSidecarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".nfo", ".jpg", ".jpeg", ".png", ".webp", ".avif", ".gif", ".bmp",
        ".tif", ".tiff", ".jpe", ".jif", ".jfif", ".jfi"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".webm", ".strm"
    };

    private static readonly ImageType[] ExportedImageTypes =
    [
        ImageType.Primary, ImageType.Backdrop, ImageType.Logo, ImageType.Art,
        ImageType.Banner, ImageType.Thumb, ImageType.Disc, ImageType.Box, ImageType.BoxRear
    ];

    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IServerConfigurationManager _configurationManager;
    private readonly ILogger<NebulaMetadataExportService> _logger;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _scheduled = new();
    private readonly CancellationTokenSource _cts = new();
    private int _disposed;
    private int _rootCancellationDisposed;

    public NebulaMetadataExportService(
        ILibraryManager libraryManager,
        IProviderManager providerManager,
        IServerConfigurationManager configurationManager,
        ILogger<NebulaMetadataExportService> logger)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _configurationManager = configurationManager;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        _cts.Cancel();
        foreach (var pending in _scheduled.Values)
        {
            pending.Cancel();
        }
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs args)
    {
        var item = args.Item;
        if (item.IsFolder || string.IsNullOrWhiteSpace(item.Path) ||
            !IsConfiguredNebulaSourcePath(item.Path) ||
            !VideoExtensions.Contains(Path.GetExtension(item.Path)))
        {
            return;
        }

        var next = new CancellationTokenSource();
        _scheduled.AddOrUpdate(item.Id, next, (_, old) =>
        {
            old.Cancel();
            return next;
        });

        _ = ExportAfterRecognitionAsync(item, next);
    }

    private async Task ExportAfterRecognitionAsync(BaseItem item, CancellationTokenSource scheduled)
    {
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, scheduled.Token);
            await Task.Delay(TimeSpan.FromSeconds(10), linked.Token).ConfigureAwait(false);
            await ExportAsync(item, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested || scheduled.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-METADATA] Falha ao exportar metadados de {Path}", item.Path);
        }
        finally
        {
            _scheduled.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(item.Id, scheduled));
            scheduled.Dispose();
            DisposeRootCancellationSourceIfIdle();
        }
    }

    private async Task ExportAsync(BaseItem item, CancellationToken cancellationToken)
        => await ExportAsync(item, stageDirectory: null, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Refreshes the item recognized by Jellyfin for a STRM path and exports its
    /// metadata sidecars to the directory that will receive the downloaded media.
    /// </summary>
    /// <param name="strmPath">The recognized STRM path.</param>
    /// <param name="targetDirectory">The staging directory for the media.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the item was found and exported.</returns>
    public async Task<bool> PrepareForDownloadAsync(
        string strmPath,
        string targetDirectory,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfiguredNebulaSourcePath(strmPath) || !IsConfiguredStagePath(targetDirectory))
        {
            _logger.LogWarning("[NEBULA-METADATA] Preparação ignorada fora das raízes permitidas: STRM={StrmPath}, destino={TargetDirectory}", strmPath, targetDirectory);
            return false;
        }

        var item = _libraryManager.FindByPath(strmPath, isFolder: false);
        if (item is null || item.IsFolder)
        {
            return false;
        }

        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, PendingMarkerFileName), "pending");
        try
        {
            await item.RefreshMetadata(cancellationToken).ConfigureAwait(false);
            await ExportAsync(item, targetDirectory, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch
        {
            // Never leave a stale marker behind when recognition/export did not
            // complete. A stale marker would block every sidecar in this folder.
            RemovePendingMarker(targetDirectory);
            throw;
        }
    }

    internal static void RemovePendingMarker(string targetDirectory)
    {
        TryDelete(Path.Combine(targetDirectory, PendingMarkerFileName));
    }

    internal static void ReleasePendingMarkerForMedia(string mediaPath)
    {
        if (IsMediaPayloadPath(mediaPath))
        {
            RemovePendingMarker(Path.GetDirectoryName(mediaPath) ?? string.Empty);
        }
    }

    private async Task ExportAsync(BaseItem item, string? stageDirectory, CancellationToken cancellationToken)
    {
        var automaticExport = string.IsNullOrWhiteSpace(stageDirectory);
        if (string.IsNullOrWhiteSpace(stageDirectory))
        {
            var sourceRoot = GetConfiguredNebulaSourceRoot(item.Path!) ?? GetNebulaDriveRoot();
            var relativePath = Path.GetRelativePath(sourceRoot, item.Path!);
            var routedDirectory = GetAutomaticStageRelativeDirectory(relativePath, Path.GetFileName(item.Path));
            if (string.IsNullOrWhiteSpace(routedDirectory))
            {
                return;
            }

            // Automatic recognition and the downloader must resolve to the same
            // staging directory. Otherwise the recognition event can leave an
            // orphan NFO/cover tree outside the directory containing the media.
            stageDirectory = Path.Combine(GetStageRoot(), routedDirectory);
        }

        Directory.CreateDirectory(stageDirectory);

        // Recognition can happen before the downloader reaches this item. Keep
        // sidecars in staging until the actual media payload is present, so the
        // watcher cannot upload metadata without its corresponding video.
        if (automaticExport)
        {
            EnsurePendingMarkerWhenMediaIsMissing(stageDirectory);
        }

        await ExportNfoAsync(item, stageDirectory, cancellationToken).ConfigureAwait(false);

        foreach (var imageType in ExportedImageTypes)
        {
            var index = 0;
            foreach (var image in item.GetImages(imageType))
            {
                if (File.Exists(image.Path))
                {
                    var targetName = GetImageFileName(item, imageType, index, image.Path);
                    await CopyAtomicallyAsync(image.Path, Path.Combine(stageDirectory, targetName), cancellationToken).ConfigureAwait(false);
                }

                index++;
            }
        }

        _logger.LogInformation("[NEBULA-METADATA] Sidecars exportados para {Directory}", stageDirectory);
    }

    private static void EnsurePendingMarkerWhenMediaIsMissing(string stageDirectory)
    {
        if (Directory.EnumerateFiles(stageDirectory).Any(IsMediaPayloadPath))
        {
            return;
        }

        File.WriteAllText(Path.Combine(stageDirectory, PendingMarkerFileName), "pending");
    }

    internal static bool IsPathWithinRoot(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return fullPath.Equals(fullRoot, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private async Task ExportNfoAsync(BaseItem item, string stageDirectory, CancellationToken cancellationToken)
    {
        var options = _libraryManager.GetLibraryOptions(item);
        var saver = _providerManager.GetMetadataSavers(item, options, includeDisabled: true)
            .OfType<IMetadataFileSaver>()
            .FirstOrDefault(s => string.Equals(s.Name, "Nfo", StringComparison.OrdinalIgnoreCase));
        if (saver is null)
        {
            _logger.LogDebug("[NEBULA-METADATA] Nenhum NFO saver aplicável para {Path}", item.Path);
            return;
        }

        var nfoName = Path.GetFileName(saver.GetSavePath(item));
        if (string.IsNullOrWhiteSpace(nfoName))
        {
            return;
        }

        var target = Path.Combine(stageDirectory, nfoName);
        await saver.SaveAsync(item, target, cancellationToken).ConfigureAwait(false);
    }

    internal static string GetImageFileName(BaseItem item, ImageType type, int index, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        if (type == ImageType.Primary && string.Equals(item.GetType().Name, "Episode", StringComparison.Ordinal))
        {
            var episodeSuffix = index > 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            return Path.GetFileNameWithoutExtension(item.Path) + "-thumb" + episodeSuffix + extension;
        }

        var baseName = type switch
        {
            ImageType.Primary => "poster",
            ImageType.Backdrop => "fanart",
            ImageType.Thumb => "landscape",
            ImageType.Logo => "logo",
            ImageType.Art => "clearart",
            ImageType.Banner => "banner",
            ImageType.Disc => "disc",
            ImageType.Box => "box",
            ImageType.BoxRear => "back",
            _ => type.ToString().ToLowerInvariant()
        };

        var suffix = index > 0 ? index.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        return baseName + suffix + extension;
    }

    internal static bool IsMetadataSidecarPath(string path)
        => !string.IsNullOrWhiteSpace(path)
            && MetadataSidecarExtensions.Contains(Path.GetExtension(path));

    internal static bool IsMediaPayloadPath(string path)
        => !string.IsNullOrWhiteSpace(path)
            && !string.Equals(Path.GetExtension(path), ".strm", StringComparison.OrdinalIgnoreCase)
            && VideoExtensions.Contains(Path.GetExtension(path));

    internal static bool IsUploadablePath(string path)
        => IsMediaPayloadPath(path) || IsMetadataSidecarPath(path);

    internal static bool IsOrphanPendingMarkerDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return true;
        }

        try
        {
            return !Directory.EnumerateFiles(directory).Any(IsMediaPayloadPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static string? GetAutomaticStageRelativeDirectory(string relativePath, string mediaFileName)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || IsRelativePathOutsideRoot(relativePath)
            || string.IsNullOrWhiteSpace(mediaFileName))
        {
            return null;
        }

        var relativeDirectory = Path.GetDirectoryName(relativePath) ?? string.Empty;
        return NebulaUploadEngine.RouteMediaRelativeDirectory(relativeDirectory, mediaFileName);
    }

    private static bool IsRelativePathOutsideRoot(string relativePath)
        => relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);

    private string GetNebulaDriveRoot()
    {
        var config = _configurationManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var drive = string.IsNullOrWhiteSpace(config?.DriveLetter) ? "N:" : config.DriveLetter.TrimEnd('\\', '/');
        return drive + "\\";
    }

    private string GetStageRoot()
    {
        var config = _configurationManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var roots = config?.StagePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray() ?? Array.Empty<string>();
        return NebulaDownloaderEngine.SelectBestStageDirectory(roots, logger: _logger);
    }

    private bool IsConfiguredStagePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var config = _configurationManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var roots = config?.StagePaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray() ?? Array.Empty<string>();
        if (roots.Length == 0)
        {
            roots = [Path.Combine(AppContext.BaseDirectory, "NebulaStage")];
        }

        return roots.Any(root => IsPathWithinRoot(path, root));
    }

    private bool IsConfiguredNebulaSourcePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // STRM files can live on a local monitor source (for example
        // D:\midias) even when the optional mapped Nebula drive is N:.
        // Metadata preparation must validate both locations; otherwise the
        // downloader finds the file but can never prepare its metadata.
        var config = _configurationManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var sources = config?.MonitorPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray() ?? Array.Empty<string>();
        if (sources.Any(root => IsPathWithinRoot(path, root)))
        {
            return true;
        }

        return IsPathWithinRoot(path, GetNebulaDriveRoot());
    }

    private string? GetConfiguredNebulaSourceRoot(string path)
    {
        var config = _configurationManager.GetConfiguration<NebulaFtpConfiguration>("nebulaftp");
        var roots = config?.MonitorPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray() ?? Array.Empty<string>();
        return roots
            .Where(root => IsPathWithinRoot(path, root))
            .OrderByDescending(root => root.Length)
            .FirstOrDefault();
    }

    private static async Task CopyAtomicallyAsync(string source, string target, CancellationToken cancellationToken)
    {
        var temp = target + ".uploading";
        try
        {
            await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, true))
            await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 128 * 1024, true))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temp, target, true);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cts.Cancel();
        foreach (var pending in _scheduled.Values)
        {
            pending.Cancel();
        }

        DisposeRootCancellationSourceIfIdle();
    }

    private void DisposeRootCancellationSourceIfIdle()
    {
        if (_disposed != 0 && _scheduled.IsEmpty && Interlocked.Exchange(ref _rootCancellationDisposed, 1) == 0)
        {
            _cts.Dispose();
        }
    }
}
