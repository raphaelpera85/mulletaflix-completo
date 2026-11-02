using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Emby.Naming.Common;
using MulletaFlix.Extensions;
using MulletaFlix.MediaEncoding.Keyframes;
using MediaBrowser.Controller.MediaEncoding;
using Microsoft.Extensions.Logging;
using Extractor = MulletaFlix.MediaEncoding.Keyframes.FfProbe.FfProbeKeyframeExtractor;

namespace MulletaFlix.MediaEncoding.Hls.Extractors;

/// <inheritdoc />
public class FfProbeKeyframeExtractor : IKeyframeExtractor
{
    private readonly IMediaEncoder _mediaEncoder;
    private readonly NamingOptions _namingOptions;
    private readonly ILogger<FfProbeKeyframeExtractor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FfProbeKeyframeExtractor"/> class.
    /// </summary>
    /// <param name="mediaEncoder">An instance of the <see cref="IMediaEncoder"/> interface.</param>
    /// <param name="namingOptions">An instance of <see cref="NamingOptions"/>.</param>
    /// <param name="logger">An instance of the <see cref="ILogger{FfprobeKeyframeExtractor}"/> interface.</param>
    public FfProbeKeyframeExtractor(IMediaEncoder mediaEncoder, NamingOptions namingOptions, ILogger<FfProbeKeyframeExtractor> logger)
    {
        _mediaEncoder = mediaEncoder;
        _namingOptions = namingOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsMetadataBased => false;

    /// <inheritdoc />
    public bool TryExtractKeyframes(Guid itemId, string filePath, [NotNullWhen(true)] out KeyframeData? keyframeData)
    {
        if (!_namingOptions.VideoFileExtensions.Contains(Path.GetExtension(filePath.AsSpan()), StringComparison.OrdinalIgnoreCase))
        {
            keyframeData = null;
            return false;
        }

        try
        {
            keyframeData = Extractor.GetKeyframeData(_mediaEncoder.ProbePath, filePath);
            return keyframeData.KeyframeTicks.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extracting keyframes from {FilePath} using ffprobe failed", filePath);
        }

        keyframeData = null;
        return false;
    }

    /// <inheritdoc />
    public async ValueTask<KeyframeData?> TryExtractKeyframesAsync(Guid itemId, string filePath, CancellationToken cancellationToken)
    {
        if (!_namingOptions.VideoFileExtensions.Contains(Path.GetExtension(filePath.AsSpan()), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var keyframeData = await Extractor.GetKeyframeDataAsync(_mediaEncoder.ProbePath, filePath, cancellationToken).ConfigureAwait(false);
            return keyframeData.KeyframeTicks.Count > 0 ? keyframeData : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extracting keyframes from {FilePath} using ffprobe failed", filePath);
            return null;
        }
    }
}

