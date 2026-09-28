using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;

namespace MulletaFlix.Server.Migrations.Routines;

/// <summary>
/// Removes the bundled Serilog count limit so the configured log-retention task controls age.
/// </summary>
[MulletaFlixMigration("2026-09-28T12:00:00", nameof(AlignLogRetention))]
public class AlignLogRetention : IAsyncMigrationRoutine
{
    private readonly IApplicationPaths _applicationPaths;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlignLogRetention"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    public AlignLogRetention(IApplicationPaths applicationPaths)
    {
        _applicationPaths = applicationPaths;
    }

    /// <inheritdoc />
    public Task PerformAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configPath = Path.Combine(_applicationPaths.ConfigurationDirectoryPath, "logging.default.json");
        if (!File.Exists(configPath))
        {
            return Task.CompletedTask;
        }

        var config = JsonNode.Parse(File.ReadAllText(configPath));
        if (config is null || !RemoveFileSinkCountLimits(config))
        {
            return Task.CompletedTask;
        }

        File.WriteAllText(configPath, config.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return Task.CompletedTask;
    }

    private static bool RemoveFileSinkCountLimits(JsonNode node)
    {
        var changed = false;
        if (node is JsonObject obj)
        {
            if (obj["Name"]?.GetValue<string>() == "File"
                && obj["Args"] is JsonObject args
                && args.TryGetPropertyValue("retainedFileCountLimit", out var countLimit)
                && countLimit is not null)
            {
                args["retainedFileCountLimit"] = null;
                changed = true;
            }

            foreach (var child in obj)
            {
                if (child.Value is not null)
                {
                    changed |= RemoveFileSinkCountLimits(child.Value);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child is not null)
                {
                    changed |= RemoveFileSinkCountLimits(child);
                }
            }
        }

        return changed;
    }
}
