using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MediaBrowser.Model.Plugins;
using Microsoft.Extensions.Logging;

namespace Emby.Server.Implementations.Plugins;

/// <summary>
/// Installs bundled third-party plugins into the server data directory before plugin discovery.
/// </summary>
public static class BundledPluginInstaller
{
    private const string FileTransformationId = "5e87cc92-571a-4d8d-8d98-d2d4147f9f90";
    private const string FileTransformationVersion = "3.0.1.0";
    private const string BundledFileTransformationFolder = "FileTransformation_3.0.1.0";
    private const string InstalledFileTransformationFolder = "MulletaFlix-FileTransformation_3.0.1.0";

    /// <summary>
    /// Ensures the compatible File Transformation plugin shipped with the server is installed.
    /// Existing equal or newer plugin versions are preserved.
    /// </summary>
    /// <param name="applicationDirectory">Directory containing the server executable.</param>
    /// <param name="pluginsDirectory">Server data plugins directory.</param>
    /// <param name="serverVersion">Version of the running server used to reject incompatible installed plugins.</param>
    /// <param name="logger">Logger used to report installation status.</param>
    public static void EnsureFileTransformationInstalled(string applicationDirectory, string pluginsDirectory, Version serverVersion, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(serverVersion);

        var bundledDirectory = Path.Combine(applicationDirectory, "bundled-plugins", BundledFileTransformationFolder);
        var manifestPath = Path.Combine(bundledDirectory, "meta.json");
        var pluginAssembly = Path.Combine(bundledDirectory, "Jellyfin.Plugin.FileTransformation.dll");
        if (!File.Exists(manifestPath) || !File.Exists(pluginAssembly))
        {
            logger.LogDebug("Bundled File Transformation plugin payload is not present; skipping installation.");
            return;
        }

        try
        {
            if (HasCompatibleInstalledVersion(pluginsDirectory, serverVersion))
            {
                logger.LogDebug("A compatible File Transformation plugin is already installed.");
                return;
            }

            Directory.CreateDirectory(pluginsDirectory);
            var destination = Path.Combine(pluginsDirectory, InstalledFileTransformationFolder);
            if (Directory.Exists(destination))
            {
                logger.LogWarning("Bundled File Transformation destination already exists; leaving it untouched: {Path}", destination);
                return;
            }

            var temporaryDirectory = Path.Combine(pluginsDirectory, ".FileTransformation_" + Guid.NewGuid().ToString("N"));
            try
            {
                CopyPluginDirectory(bundledDirectory, temporaryDirectory);
                Directory.Move(temporaryDirectory, destination);
                logger.LogInformation("Installed bundled File Transformation plugin version {Version}.", FileTransformationVersion);
            }
            finally
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, recursive: true);
                }
            }
        }
#pragma warning disable CA1031 // A bundled optional plugin must not prevent the server from starting.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Unable to install bundled File Transformation plugin. The server will continue startup.");
        }
    }

    private static bool HasCompatibleInstalledVersion(string pluginsDirectory, Version serverVersion)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            return false;
        }

        var bundledVersion = Version.Parse(FileTransformationVersion);
        foreach (var pluginDirectory in Directory.EnumerateDirectories(pluginsDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var manifestPath = Path.Combine(pluginDirectory, "meta.json");
                if (!File.Exists(manifestPath))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
                var root = document.RootElement;
                if (!root.TryGetProperty("guid", out var id)
                    || !Guid.TryParse(id.GetString(), out var pluginId)
                    || !pluginId.ToString().Equals(FileTransformationId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetAbi = root.TryGetProperty("targetAbi", out var targetAbiValue)
                    && Version.TryParse(targetAbiValue.GetString(), out var parsedTargetAbi)
                        ? parsedTargetAbi
                        : new Version(0, 0, 0, 1);
                if (targetAbi > serverVersion)
                {
                    continue;
                }

                if (root.TryGetProperty("version", out var version)
                    && Version.TryParse(version.GetString(), out var installedVersion)
                    && installedVersion >= bundledVersion)
                {
                    return true;
                }
            }
            catch (JsonException)
            {
                // Ignore unrelated or malformed plugin manifests.
            }
            catch (IOException)
            {
                // Ignore a manifest that is concurrently being updated.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore plugin folders the current service account cannot inspect.
            }
            catch (InvalidOperationException)
            {
                // Ignore manifests with values of unexpected JSON types.
            }
        }

        return false;
    }

    private static void CopyPluginDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly)
                     .Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }
}
