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
            !item.Path.StartsWith(GetNebulaDriveRoot(), StringComparison.OrdinalIgnoreCase) ||
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

        var stageRoot = GetStageRoot();
        var stageDirectory = Path.Combine(stageRoot, relativeDirectory);
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

    private static string GetImageFileName(BaseItem item, ImageType type, int index, string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".jpg";
        }

        if (type == ImageType.Primary && string.Equals(item.GetType().Name, "Episode", StringComparison.Ordinal))
        {
            return Path.GetFileNameWithoutExtension(item.Path) + "-thumb" + extension;
        }

        var baseName = type switch
        {
            ImageType.Primary => "poster",
            ImageType.Backdrop => index == 0 ? "fanart" : $"fanart{index}",
            ImageType.Thumb => "landscape",
            ImageType.Logo => "logo",
            ImageType.Art => "clearart",
            ImageType.Banner => "banner",
            ImageType.Disc => "disc",
            ImageType.Box => "box",
            ImageType.BoxRear => "back",
            _ => type.ToString().ToLowerInvariant()
        };

        return baseName + extension;
    }

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
        _cts.Cancel();
        _cts.Dispose();
        foreach (var pending in _scheduled.Values)
        {
            pending.Dispose();
        }
    }
}
