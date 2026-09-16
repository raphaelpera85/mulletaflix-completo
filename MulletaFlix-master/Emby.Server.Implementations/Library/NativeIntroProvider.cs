#nullable disable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.Branding;
using MulletaFlix.Database.Implementations.Entities;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Library;

/// <summary>
/// Provides a configured local intro without requiring an external plugin.
/// </summary>
public sealed class NativeIntroProvider : IIntroProvider
{
    private static readonly string[] SupportedExtensions = [".mkv", ".mp4", ".webm", ".mov", ".avi"];
    private readonly IServerConfigurationManager _configurationManager;
    private readonly IStrmPrebufferManager _prebufferManager;
    private readonly ILogger<NativeIntroProvider> _logger;
    private readonly IApplicationPaths _applicationPaths;

    public NativeIntroProvider(
        IServerConfigurationManager configurationManager,
        IStrmPrebufferManager prebufferManager,
        ILogger<NativeIntroProvider> logger,
        IApplicationPaths applicationPaths = null)
    {
        _configurationManager = configurationManager;
        _prebufferManager = prebufferManager;
        _logger = logger;
        _applicationPaths = applicationPaths;
    }

    public string Name => "MulletaFlix Native Intro";

    public async Task<IEnumerable<IntroInfo>> GetIntros(BaseItem item, User user)
    {
        var options = _configurationManager.GetConfiguration<BrandingOptions>("branding");
        if (options is not null && !options.IntroEnabled)
        {
            return [];
        }

        string introPath = null;
        if (!string.IsNullOrWhiteSpace(options?.IntroPath))
        {
            introPath = ResolveIntroPath(options.IntroPath);
        }

        if (introPath is null)
        {
            introPath = ResolveDefaultNativeIntro();
        }

        if (introPath is null)
        {
            _logger.LogDebug("No native intro video found or configured.");
            return [];
        }

        // Prevent recursion: don't play an intro before the intro item itself
        if (item is Video video && !string.IsNullOrWhiteSpace(video.Path))
        {
            if (string.Equals(video.Path, introPath, StringComparison.OrdinalIgnoreCase)
                || video.Path.EndsWith("mulletaflix_intro.mp4", StringComparison.OrdinalIgnoreCase))
            {
                return [];
            }
        }

        await _prebufferManager.PrepareAsync(item).ConfigureAwait(false);

        return [new IntroInfo { Path = introPath }];
    }

    private string ResolveDefaultNativeIntro()
    {
        // 1. Environment variable override
        var envPath = Environment.GetEnvironmentVariable("MulletaFlix_INTRO_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var resolvedEnv = ResolveIntroPath(envPath);
            if (resolvedEnv is not null)
            {
                return resolvedEnv;
            }
        }

        var candidatePaths = new List<string>();

        // 2. Base directory (App install directory: e.g. C:\Program Files\MulletaFlix\Server\media\mulletaflix_intro.mp4)
        var baseDir = AppContext.BaseDirectory;
        if (!string.IsNullOrWhiteSpace(baseDir))
        {
            candidatePaths.Add(Path.Combine(baseDir, "media", "mulletaflix_intro.mp4"));
            candidatePaths.Add(Path.Combine(baseDir, "mulletaflix_intro.mp4"));
        }

        // 3. ProgramData directory: e.g. C:\ProgramData\MulletaFlix\Server\media\mulletaflix_intro.mp4
        if (_applicationPaths is not null)
        {
            if (!string.IsNullOrWhiteSpace(_applicationPaths.ProgramDataPath))
            {
                candidatePaths.Add(Path.Combine(_applicationPaths.ProgramDataPath, "media", "mulletaflix_intro.mp4"));
                candidatePaths.Add(Path.Combine(_applicationPaths.ProgramDataPath, "mulletaflix_intro.mp4"));
            }

            if (!string.IsNullOrWhiteSpace(_applicationPaths.DataPath))
            {
                candidatePaths.Add(Path.Combine(_applicationPaths.DataPath, "media", "mulletaflix_intro.mp4"));
                candidatePaths.Add(Path.Combine(_applicationPaths.DataPath, "mulletaflix_intro.mp4"));
            }
        }

        // 4. Default user videos path on host for local development
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            candidatePaths.Add(Path.Combine(userProfile, "Videos", "mulletaflix_intro.mp4"));
        }
        candidatePaths.Add(@"d:\Users\Raphael\Videos\mulletaflix_intro.mp4");

        foreach (var candidate in candidatePaths)
        {
            if (File.Exists(candidate))
            {
                _logger.LogInformation("Resolved native MulletaFlix intro: {Path}", candidate);
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveIntroPath(string path)
    {
        if (File.Exists(path))
        {
            return SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) ? path : null;
        }

        if (!Directory.Exists(path))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(path)
            .Where(file => SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (files.Length == 0)
        {
            return null;
        }

        if (files.Length == 1)
        {
            return files[0];
        }

        return files[Random.Shared.Next(files.Length)];
    }
}

