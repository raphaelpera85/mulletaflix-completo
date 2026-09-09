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
        ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".webm", ".strm"
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
            pending.Dispose();
        }

        _scheduled.Clear();
        return Task.CompletedTask;
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs args)
    {
        var item = args.Item;
        if (item.IsFolder || string.IsNullOrWhiteSpace(item.Path) ||
            !IsPathWithinRoot(item.Path, GetNebulaDriveRoot()) ||
            !VideoExtensions.Contains(Path.GetExtension(item.Path)))
        {
            return;
        }

        var next = new CancellationTokenSource();
        _scheduled.AddOrUpdate(item.Id, next, (_, old) =>
        {
            old.Cancel();
            old.Dispose();
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
        if (!IsPathWithinRoot(strmPath, GetNebulaDriveRoot()) || !IsConfiguredStagePath(targetDirectory))
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
        await item.RefreshMetadata(cancellationToken).ConfigureAwait(false);
        await ExportAsync(item, targetDirectory, cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static void RemovePendingMarker(string targetDirectory)
    {
        TryDelete(Path.Combine(targetDirectory, PendingMarkerFileName));
    }

    private async Task ExportAsync(BaseItem item, string? stageDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stageDirectory))
        {
            var relativePath = Path.GetRelativePath(GetNebulaDriveRoot(), item.Path!);
            if (relativePath.StartsWith("..", StringComparison.Ordinal))
            {
                return;
            }

            var relativeDirectory = Path.GetDirectoryName(relativePath);
            if (string.IsNullOrWhiteSpace(relativeDirectory))
            {
                return;
            }

            stageDirectory = Path.Combine(GetStageRoot(), relativeDirectory);
        }

        Directory.CreateDirectory(stageDirectory);

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
        foreach (var root in roots)
        {
            if (Directory.Exists(root))
            {
                return root;
            }
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "NebulaStage");
        Directory.CreateDirectory(fallback);
        return fallback;
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
        _cts.Dispose();
        foreach (var pending in _scheduled.Values)
        {
            pending.Dispose();
        }
    }
}
