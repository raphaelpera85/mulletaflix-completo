using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Attributes;
using MulletaFlix.Api.Helpers;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.MediaEncoding;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// The hls segment controller.
/// </summary>
[Route("")]
[ApiExplorerSettings(IgnoreApi = true)]
public class HlsSegmentController : BaseMulletaFlixApiController
{
    private readonly IFileSystem _fileSystem;
    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly ITranscodeManager _transcodeManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="HlsSegmentController"/> class.
    /// </summary>
    /// <param name="fileSystem">Instance of the <see cref="IFileSystem"/> interface.</param>
    /// <param name="serverConfigurationManager">Instance of the <see cref="IServerConfigurationManager"/> interface.</param>
    /// <param name="transcodeManager">Instance of the <see cref="ITranscodeManager"/> interface.</param>
    public HlsSegmentController(
        IFileSystem fileSystem,
        IServerConfigurationManager serverConfigurationManager,
        ITranscodeManager transcodeManager)
    {
        _fileSystem = fileSystem;
        _serverConfigurationManager = serverConfigurationManager;
        _transcodeManager = transcodeManager;
    }

    /// <summary>
    /// Gets the specified audio segment for an audio item.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="segmentId">The segment id.</param>
    /// <response code="200">Hls audio segment returned.</response>
    /// <returns>A <see cref="FileStreamResult"/> containing the audio stream.</returns>
    // Can't require authentication just yet due to seeing some requests come from Chrome without full query string
    // [Authenticated]
    [HttpGet("Audio/{itemId}/hls/{segmentId}/stream.mp3", Name = "GetHlsAudioSegmentLegacyMp3")]
    [HttpGet("Audio/{itemId}/hls/{segmentId}/stream.aac", Name = "GetHlsAudioSegmentLegacyAac")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesAudioFile]
    [SuppressMessage("Microsoft.Performance", "CA1801:ReviewUnusedParameters", MessageId = "itemId", Justification = "Required for ServiceStack")]
    public ActionResult GetHlsAudioSegmentLegacy([FromRoute, Required] string itemId, [FromRoute, Required] string segmentId)
    {
        // TODO: Deprecate with new iOS app
        var extension = Path.GetExtension(Request.Path.Value.AsSpan()).ToString();
        if (!IsSafeHlsPathSegment(segmentId)
            || (!extension.Equals(".mp3", StringComparison.OrdinalIgnoreCase)
                && !extension.Equals(".aac", StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest("Invalid segment.");
        }

        var transcodePath = _serverConfigurationManager.GetTranscodePath();
        if (!TryResolveTranscodeFilePath(transcodePath, string.Concat(segmentId, extension), out var file))
        {
            return BadRequest("Invalid segment.");
        }

        return FileStreamResponseHelpers.GetStaticFileResult(file, MimeTypes.GetMimeType(file));
    }

    /// <summary>
    /// Gets a hls video playlist.
    /// </summary>
    /// <param name="itemId">The video id.</param>
    /// <param name="playlistId">The playlist id.</param>
    /// <response code="200">Hls video playlist returned.</response>
    /// <returns>A <see cref="FileStreamResult"/> containing the playlist.</returns>
    [HttpGet("Videos/{itemId}/hls/{playlistId}/stream.m3u8")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesPlaylistFile]
    [SuppressMessage("Microsoft.Performance", "CA1801:ReviewUnusedParameters", MessageId = "itemId", Justification = "Required for ServiceStack")]
    public ActionResult GetHlsPlaylistLegacy([FromRoute, Required] string itemId, [FromRoute, Required] string playlistId)
    {
        if (!IsSafeHlsPathSegment(playlistId))
        {
            return BadRequest("Invalid segment.");
        }

        var transcodePath = _serverConfigurationManager.GetTranscodePath();
        if (!TryResolveTranscodeFilePath(transcodePath, string.Concat(playlistId, ".m3u8"), out var file)
            || !Path.GetExtension(file.AsSpan()).Equals(".m3u8", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid segment.");
        }

        return GetFileResult(file, file);
    }

    /// <summary>
    /// Stops an active encoding.
    /// </summary>
    /// <param name="deviceId">The device id of the client requesting. Used to stop encoding processes when needed.</param>
    /// <param name="playSessionId">The play session id.</param>
    /// <response code="204">Encoding stopped successfully.</response>
    /// <returns>A <see cref="NoContentResult"/> indicating success.</returns>
    [HttpDelete("Videos/ActiveEncodings")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult StopEncodingProcess(
        [FromQuery, Required] string deviceId,
        [FromQuery, Required] string playSessionId)
    {
        var authenticatedDeviceId = User.GetDeviceId();
        if (string.IsNullOrWhiteSpace(authenticatedDeviceId)
            || !string.Equals(deviceId, authenticatedDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        _transcodeManager.KillTranscodingJobs(authenticatedDeviceId, playSessionId, _ => true);
        return NoContent();
    }

    /// <summary>
    /// Gets a hls video segment.
    /// </summary>
    /// <param name="itemId">The item id.</param>
    /// <param name="playlistId">The playlist id.</param>
    /// <param name="segmentId">The segment id.</param>
    /// <param name="segmentContainer">The segment container.</param>
    /// <response code="200">Hls video segment returned.</response>
    /// <response code="404">Hls segment not found.</response>
    /// <returns>A <see cref="FileStreamResult"/> containing the video segment.</returns>
    // Can't require authentication just yet due to seeing some requests come from Chrome without full query string
    // [Authenticated]
    [HttpGet("Videos/{itemId}/hls/{playlistId}/{segmentId}.{segmentContainer}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesVideoFile]
    [SuppressMessage("Microsoft.Performance", "CA1801:ReviewUnusedParameters", MessageId = "itemId", Justification = "Required for ServiceStack")]
    public ActionResult GetHlsVideoSegmentLegacy(
        [FromRoute, Required] string itemId,
        [FromRoute, Required] string playlistId,
        [FromRoute, Required] string segmentId,
        [FromRoute, Required] string segmentContainer)
    {
        if (!IsSafeHlsPathSegment(segmentId)
            || !IsSafeHlsPathSegment(playlistId)
            || !IsSafeHlsPathSegment(segmentContainer))
        {
            return BadRequest("Invalid segment.");
        }

        var transcodeFolderPath = _serverConfigurationManager.GetTranscodePath();
        if (!TryResolveTranscodeFilePath(transcodeFolderPath, string.Concat(segmentId, ".", segmentContainer), out var file))
        {
            return BadRequest("Invalid segment.");
        }

        var normalizedPlaylistId = playlistId;

        // Direct fast path: the legacy playlist for this segment is always written as
        // "{playlistId}.m3u8" in the transcode folder (see GetHlsPlaylistLegacy above, which
        // builds the exact same path). Try that first instead of enumerating the entire
        // transcode directory (which can contain segments/playlists for every concurrent
        // transcoding job) on every single segment request. Only fall back to the old
        // directory scan if the expected file is missing, e.g. a differently-named artifact
        // from an older/alternate encoder path.
        string? playlistPath = TryResolveTranscodeFilePath(transcodeFolderPath, normalizedPlaylistId + ".m3u8", out var directPlaylistPath)
            && System.IO.File.Exists(directPlaylistPath)
                ? directPlaylistPath
                : null;

        if (playlistPath is null)
        {
            var filePaths = _fileSystem.GetFilePaths(transcodeFolderPath);
            // Add . to start of segment container for future use.
            var segmentExtension = segmentContainer.Insert(0, ".");
            foreach (var path in filePaths)
            {
                var pathExtension = Path.GetExtension(path);
                if ((string.Equals(pathExtension, segmentExtension, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(pathExtension, ".m3u8", StringComparison.OrdinalIgnoreCase))
                    && path.Contains(normalizedPlaylistId, StringComparison.OrdinalIgnoreCase))
                {
                    playlistPath = path;
                    break;
                }
            }
        }

        return playlistPath is null
            ? NotFound("Hls segment not found.")
            : GetFileResult(file, playlistPath);
    }

    private static bool IsSafeHlsPathSegment(string? segment)
    {
        if (string.IsNullOrWhiteSpace(segment)
            || segment is "." or ".."
            || !string.Equals(Path.GetFileName(segment), segment, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in segment)
        {
            if (char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveTranscodeFilePath(string transcodePath, string fileName, out string fullPath)
    {
        fullPath = string.Empty;
        if (!IsSafeHlsPathSegment(fileName))
        {
            return false;
        }

        try
        {
            var fullTranscodePath = Path.GetFullPath(transcodePath);
            var candidatePath = Path.GetFullPath(Path.Combine(fullTranscodePath, fileName));
            var relativePath = Path.GetRelativePath(fullTranscodePath, candidatePath);
            if (Path.IsPathRooted(relativePath)
                || string.Equals(relativePath, ".", StringComparison.Ordinal)
                || string.Equals(relativePath, "..", StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
                || relativePath.AsSpan().IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) >= 0)
            {
                return false;
            }

            if (IsReparsePoint(candidatePath))
            {
                return false;
            }

            fullPath = candidatePath;
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (System.IO.File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private ActionResult GetFileResult(string path, string playlistPath)
    {
        var transcodingJob = _transcodeManager.OnTranscodeBeginRequest(playlistPath, TranscodingJobType.Hls);

        Response.OnCompleted(() =>
        {
            if (transcodingJob is not null)
            {
                _transcodeManager.OnTranscodeEndRequest(transcodingJob);
            }

            return Task.CompletedTask;
        });

        return FileStreamResponseHelpers.GetStaticFileResult(path, MimeTypes.GetMimeType(path));
    }
}
