using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.MediaEncoding.Keyframes.Matroska.Extensions;
using MulletaFlix.MediaEncoding.Keyframes.Matroska.Models;
using NEbml.Core;

namespace MulletaFlix.MediaEncoding.Keyframes.Matroska;

/// <summary>
/// The keyframe extractor for the matroska container.
/// </summary>
public static class MatroskaKeyframeExtractor
{
    /// <summary>
    /// Extracts the keyframes in ticks (scaled using the container timestamp scale) from the matroska container.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>An instance of <see cref="KeyframeData"/>.</returns>
    public static KeyframeData GetKeyframeData(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new EbmlReader(stream);

        var seekHead = reader.ReadSeekHead();
        // External lib does not support seeking backwards (yet)
        Info info;
        ulong videoTrackNumber;
        if (seekHead.InfoPosition < seekHead.TracksPosition)
        {
            info = reader.ReadInfo(seekHead.InfoPosition);
            videoTrackNumber = reader.FindFirstTrackNumberByType(seekHead.TracksPosition, MatroskaConstants.TrackTypeVideo);
        }
        else
        {
            videoTrackNumber = reader.FindFirstTrackNumberByType(seekHead.TracksPosition, MatroskaConstants.TrackTypeVideo);
            info = reader.ReadInfo(seekHead.InfoPosition);
        }

        var keyframes = new List<long>();
        reader.ReadAt(seekHead.CuesPosition);
        reader.EnterContainer();

        while (reader.FindElement(MatroskaConstants.CuePoint))
        {
            reader.EnterContainer();
            var hasVideoTrackPosition = false;
            // Mandatory element
            reader.FindElement(MatroskaConstants.CueTime);
            var cueTime = reader.ReadUInt();

            while (reader.FindElement(MatroskaConstants.CueTrackPositions))
            {
                reader.EnterContainer();
                if (reader.FindElement(MatroskaConstants.CuePointTrackNumber))
                {
                    hasVideoTrackPosition |= reader.ReadUInt() == videoTrackNumber;
                }

                reader.LeaveContainer();
            }

            if (hasVideoTrackPosition)
            {
                keyframes.Add(ScaleToTicks(cueTime, info.TimestampScale));
            }

            reader.LeaveContainer();
        }

        reader.LeaveContainer();

        var result = new KeyframeData(ScaleToTicks(info.Duration ?? 0, info.TimestampScale), keyframes);
        return result;
    }

    /// <summary>
    /// Asynchronously extracts keyframes from the Matroska container.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An instance of <see cref="KeyframeData"/>.</returns>
    public static async Task<KeyframeData> GetKeyframeDataAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        cancellationToken.ThrowIfCancellationRequested();

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.RandomAccess);

        return await AsyncMatroskaKeyframeReader.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
    }

    internal static Task<KeyframeData> GetKeyframeDataAsync(Stream stream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return AsyncMatroskaKeyframeReader.ReadAsync(stream, cancellationToken);
    }

    private static long ScaleToTicks(ulong unscaledValue, long timestampScale)
    {
        // TimestampScale is in nanoseconds, scale it to get the value in ticks, 1 tick == 100 ns
        return (long)unscaledValue * timestampScale / 100;
    }

    private static long ScaleToTicks(double unscaledValue, long timestampScale)
    {
        // TimestampScale is in nanoseconds, scale it to get the value in ticks, 1 tick == 100 ns
        return Convert.ToInt64(unscaledValue * timestampScale / 100);
    }
}

