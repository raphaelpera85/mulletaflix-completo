using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MulletaFlix.MediaEncoding.Keyframes.FfProbe;

/// <summary>
/// FfProbe based keyframe extractor.
/// </summary>
public static class FfProbeKeyframeExtractor
{
    /// <summary>
    /// Extracts the keyframes using the ffprobe executable at the specified path.
    /// </summary>
    /// <param name="ffProbePath">The path to the ffprobe executable.</param>
    /// <param name="filePath">The file path.</param>
    /// <returns>An instance of <see cref="KeyframeData"/>.</returns>
    public static KeyframeData GetKeyframeData(string ffProbePath, string filePath)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffProbePath,
                Arguments = string.Format(
                    CultureInfo.InvariantCulture,
                    "-fflags +genpts -v error -skip_frame nokey -show_entries format=duration -show_entries stream=duration -show_entries packet=pts_time,flags -select_streams v -of csv \"{0}\"",
                    filePath),

                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,

                WindowStyle = ProcessWindowStyle.Hidden,
                ErrorDialog = false,
            },
            EnableRaisingEvents = true
        };

        try
        {
            process.Start();
            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch
            {
                // We do not care if process priority setting fails
                // Ideally log a warning but this does not have a logger available
            }

            return ParseStream(process.StandardOutput);
        }
        catch (Exception)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
            }
            catch
            {
                // We do not care if this fails
            }

            throw;
        }
    }

    public static async Task<KeyframeData> GetKeyframeDataAsync(string ffProbePath, string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = ffProbePath,
                Arguments = string.Format(
                    CultureInfo.InvariantCulture,
                    "-fflags +genpts -v error -skip_frame nokey -show_entries format=duration -show_entries stream=duration -show_entries packet=pts_time,flags -select_streams v -of csv \"{0}\"",
                    filePath),
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                ErrorDialog = false,
            },
            EnableRaisingEvents = true
        };

        process.Start();
        try
        {
            try
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch
            {
                // Process priority is a best-effort hint.
            }

            var parseTask = ParseStreamAsync(process.StandardOutput, cancellationToken);
            await Task.WhenAll(process.WaitForExitAsync(cancellationToken), parseTask).ConfigureAwait(false);
            return await parseTask.ConfigureAwait(false);
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            catch
            {
                // Preserve the extraction or cancellation failure.
            }

            throw;
        }
    }

    internal static KeyframeData ParseStream(StreamReader reader)
    {
        var builder = new KeyframeDataBuilder();
        using (reader)
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                builder.Add(line.AsSpan());
            }
        }

        return builder.Build();
    }

    internal static async Task<KeyframeData> ParseStreamAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new KeyframeDataBuilder();
        using (reader)
        {
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                builder.Add(line.AsSpan());
            }
        }

        return builder.Build();
    }

    private sealed class KeyframeDataBuilder
    {
        private readonly List<long> _keyframes = new();
        private double _streamDuration;
        private double _formatDuration;

        public void Add(ReadOnlySpan<char> line)
        {
            if (line.IsEmpty)
            {
                return;
            }

            var firstComma = line.IndexOf(',');
            if (firstComma < 0)
            {
                return;
            }

            var lineType = line[..firstComma];
            var rest = line[(firstComma + 1)..];
            if (lineType.Equals("packet", StringComparison.OrdinalIgnoreCase))
            {
                var secondComma = rest.IndexOf(',');
                if (secondComma < 0)
                {
                    return;
                }

                var ptsTime = rest[..secondComma];
                var flags = rest[(secondComma + 1)..];
                if (flags.StartsWith("K_")
                    && double.TryParse(ptsTime, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var keyframe))
                {
                    _keyframes.Add(Convert.ToInt64(keyframe * TimeSpan.TicksPerSecond));
                }
            }
            else if (lineType.Equals("stream", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(rest, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var streamDuration))
            {
                _streamDuration = streamDuration;
            }
            else if (lineType.Equals("format", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(rest, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var formatDuration))
            {
                _formatDuration = formatDuration;
            }
        }

        public KeyframeData Build()
        {
            var duration = _streamDuration > 0 ? _streamDuration : _formatDuration;
            return new KeyframeData(TimeSpan.FromSeconds(duration).Ticks, _keyframes);
        }
    }
}
