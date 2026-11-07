using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.MediaEncoding.Keyframes;
using Microsoft.Extensions.Logging;
using Extractor = MulletaFlix.MediaEncoding.Keyframes.Matroska.MatroskaKeyframeExtractor;

namespace MulletaFlix.MediaEncoding.Hls.Extractors;

/// <inheritdoc />
public class MatroskaKeyframeExtractor : IKeyframeExtractor
{
    private readonly ILogger<MatroskaKeyframeExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MatroskaKeyframeExtractor"/> class.
    /// </summary>
    /// <param name="logger">An instance of the <see cref="ILogger{MatroskaKeyframeExtractor}"/> interface.</param>
    public MatroskaKeyframeExtractor(ILogger<MatroskaKeyframeExtractor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsMetadataBased => true;

    /// <inheritdoc />
    public bool TryExtractKeyframes(Guid itemId, string filePath, [NotNullWhen(true)] out KeyframeData? keyframeData)
    {
        if (!filePath.AsSpan().EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            keyframeData = null;
            return false;
        }

        try
        {
            keyframeData = Extractor.GetKeyframeData(filePath);
            return keyframeData.KeyframeTicks.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extracting keyframes from {FilePath} using matroska metadata failed", filePath);
        }

        keyframeData = null;
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<KeyframeData?> TryExtractKeyframesAsync(Guid itemId, string filePath, CancellationToken cancellationToken)
    {
        if (!filePath.AsSpan().EndsWith(".mkv", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var keyframeData = await Extractor.GetKeyframeDataAsync(filePath, cancellationToken).ConfigureAwait(false);
            return keyframeData.KeyframeTicks.Count > 0 ? keyframeData : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extracting keyframes from {FilePath} using matroska metadata failed", filePath);
            return null;
        }
    }
}

