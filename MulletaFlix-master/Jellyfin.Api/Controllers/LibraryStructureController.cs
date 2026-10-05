using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Api.ModelBinders;
using MulletaFlix.Api.Models.LibraryStructureDto;
using MediaBrowser.Common.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// The library structure controller.
/// </summary>
[Route("Library/VirtualFolders")]
[Authorize(Policy = Policies.FirstTimeSetupOrElevated)]
public class LibraryStructureController : BaseMulletaFlixApiController
{
    private readonly IServerApplicationPaths _appPaths;
    private readonly IServerConfigurationManager _serverConfigurationManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILibraryMonitor _libraryMonitor;
    private readonly ILocalizationManager _localizationManager;
    private readonly ILogger<LibraryStructureController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryStructureController"/> class.
    /// </summary>
    /// <param name="serverConfigurationManager">Instance of <see cref="IServerConfigurationManager"/> interface.</param>
    /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/> interface.</param>
    /// <param name="libraryMonitor">Instance of <see cref="ILibraryMonitor"/> interface.</param>
    /// <param name="localizationManager">Instance of <see cref="ILocalizationManager"/> interface.</param>
    /// <param name="logger">Logger.</param>
    public LibraryStructureController(
        IServerConfigurationManager serverConfigurationManager,
        ILibraryManager libraryManager,
        ILibraryMonitor libraryMonitor,
        ILocalizationManager localizationManager,
        ILogger<LibraryStructureController> logger)
    {
        _serverConfigurationManager = serverConfigurationManager;
        _appPaths = serverConfigurationManager.ApplicationPaths;
        _libraryManager = libraryManager;
        _libraryMonitor = libraryMonitor;
        _localizationManager = localizationManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets all virtual folders.
    /// </summary>
    /// <response code="200">Virtual folders retrieved.</response>
    /// <returns>An <see cref="IEnumerable{VirtualFolderInfo}"/> with the virtual folders.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<VirtualFolderInfo>> GetVirtualFolders()
    {
        return _libraryManager.GetVirtualFolders(true);
    }

    /// <summary>
    /// Adds a virtual folder.
    /// </summary>
    /// <param name="name">The name of the virtual folder.</param>
    /// <param name="collectionType">The type of the collection.</param>
    /// <param name="paths">The paths of the virtual folder.</param>
    /// <param name="libraryOptionsDto">The library options.</param>
    /// <param name="refreshLibrary">Whether to refresh the library.</param>
    /// <response code="204">Folder added.</response>
    /// <returns>A <see cref="NoContentResult"/>.</returns>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> AddVirtualFolder(
        [FromQuery]
        [RegularExpression(@"^(?:\S(?:.*\S)?)$", ErrorMessage = "Library name cannot be empty or have leading/trailing spaces.")]
        string name,
        [FromQuery] CollectionTypeOptions? collectionType,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] string[] paths,
        [FromBody] AddVirtualFolderDto? libraryOptionsDto,
        [FromQuery] bool refreshLibrary = false)
    {
        var libraryOptions = libraryOptionsDto?.LibraryOptions ?? new LibraryOptions();
        ApplyLibraryDefaults(libraryOptions);

        if (paths is not null && paths.Length > 0)
        {
            libraryOptions.PathInfos = Array.ConvertAll(paths, i => new MediaPathInfo(i));
        }

        await _libraryManager.AddVirtualFolder(name, collectionType, libraryOptions, refreshLibrary).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Removes a virtual folder.
    /// </summary>
    /// <param name="name">The name of the folder.</param>
    /// <param name="refreshLibrary">Whether to refresh the library.</param>
    /// <response code="204">Folder removed.</response>
    /// <response code="404">Folder not found.</response>
    /// <returns>A <see cref="NoContentResult"/>.</returns>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RemoveVirtualFolder(
        [FromQuery] string name,
        [FromQuery] bool refreshLibrary = false)
    {
        // TODO: refactor! this relies on an FileNotFound exception to return NotFound when attempting to remove a library that does not exist.
        await _libraryManager.RemoveVirtualFolder(name, refreshLibrary).ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>
    /// Renames a virtual folder.
    /// </summary>
    /// <param name="name">The name of the virtual folder.</param>
    /// <param name="newName">The new name.</param>
    /// <param name="refreshLibrary">Whether to refresh the library.</param>
    /// <response code="204">Folder renamed.</response>
    /// <response code="404">Library doesn't exist.</response>
    /// <response code="409">Library already exists.</response>
    /// <returns>A <see cref="NoContentResult"/> on success, a <see cref="NotFoundResult"/> if the library doesn't exist, a <see cref="ConflictResult"/> if the new name is already taken.</returns>
    /// <exception cref="ArgumentNullException">The new name may not be null.</exception>
    [HttpPost("Name")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult RenameVirtualFolder(
        [FromQuery] string? name,
        [FromQuery] string? newName,
        [FromQuery] bool refreshLibrary = false)
    {
        if (!IsSinglePathSegment(name))
        {
            return BadRequest("Library name must be a single directory name.");
        }

        if (!IsSinglePathSegment(newName))
        {
            return BadRequest("New library name must be a single directory name.");
        }

        var rootFolderPath = _appPaths.DefaultUserViewsPath;

        var currentPath = Path.Combine(rootFolderPath, name);
        var newPath = Path.Combine(rootFolderPath, newName);

        if (!IsPathWithinRoot(rootFolderPath, currentPath) || !IsPathWithinRoot(rootFolderPath, newPath))
        {
            return BadRequest("Library names must resolve below the default user views directory.");
        }

        if (!Directory.Exists(currentPath))
        {
            return NotFound("The media collection does not exist.");
        }

        if (!string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(newPath))
        {
            return Conflict($"The media library already exists at {newPath}.");
        }

        var operationLease = LibraryBackgroundOperationGate.TryAcquire();
        if (operationLease is null)
        {
            Response.Headers.RetryAfter = "1";
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        // Revalidate after acquiring the operation gate so queued filesystem changes
        // cannot rely solely on the earlier request validation.
        if (!IsPathWithinRoot(rootFolderPath, currentPath) || !IsPathWithinRoot(rootFolderPath, newPath))
        {
            operationLease.Dispose();
            return BadRequest("Library names must resolve below the default user views directory.");
        }

        if (!Directory.Exists(currentPath))
        {
            operationLease.Dispose();
            return NotFound("The media collection does not exist.");
        }

        if (!string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(newPath))
        {
            operationLease.Dispose();
            return Conflict($"The media library already exists at {newPath}.");
        }

        try
        {
            _libraryMonitor.Stop();
            // Changing capitalization. Handle windows case insensitivity
            if (string.Equals(currentPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                var tempPath = Path.Combine(
                    rootFolderPath,
                    Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
                Directory.Move(currentPath, tempPath);
                currentPath = tempPath;
            }

            Directory.Move(currentPath, newPath);
        }
        finally
        {
            try
            {
                CollectionFolder.OnCollectionFolderChange();
            }
            finally
            {
                StartBackgroundLibraryOperation(
                    async () =>
                    {
                        if (refreshLibrary)
                        {
                            await _libraryManager.ValidateTopLibraryFolders(CancellationToken.None, true).ConfigureAwait(false);
                            var newLib = _libraryManager.GetUserRootFolder().Children.FirstOrDefault(f => f.Path.Equals(newPath, StringComparison.OrdinalIgnoreCase));
                            if (newLib is CollectionFolder folder)
                            {
                                _libraryManager.ClearIgnoreRuleCache();
                                foreach (var child in folder.GetPhysicalFolders())
                                {
                                    await child.RefreshMetadata(CancellationToken.None).ConfigureAwait(false);
                                    await child.ValidateChildren(new Progress<double>(), CancellationToken.None).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                _libraryManager.ClearIgnoreRuleCache();
                                // We don't know if this one can be validated individually, trigger a new validation
                                await _libraryManager.ValidateMediaLibrary(new Progress<double>(), CancellationToken.None).ConfigureAwait(false);
                            }

                            _libraryManager.ClearIgnoreRuleCache();
                        }
                        else
                        {
                            // Need to add a delay here or directory watchers may still pick up the changes
                            await Task.Delay(1000).ConfigureAwait(false);
                            _libraryMonitor.Start();
                        }
                    },
                    operationLease,
                    "rename virtual folder");
            }
        }

        return NoContent();
    }

    /// <summary>
    /// Add a media path to a library.
    /// </summary>
    /// <param name="mediaPathDto">The media path dto.</param>
    /// <param name="refreshLibrary">Whether to refresh the library.</param>
    /// <returns>A <see cref="NoContentResult"/>.</returns>
    /// <response code="204">Media path added.</response>
    /// <exception cref="ArgumentNullException">The name of the library may not be empty.</exception>
    [HttpPost("Paths")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult AddMediaPath(
        [FromBody, Required] MediaPathDto mediaPathDto,
        [FromQuery] bool refreshLibrary = false)
    {
        var operationLease = LibraryBackgroundOperationGate.TryAcquire();
        if (operationLease is null)
        {
            Response.Headers.RetryAfter = "1";
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        try
        {
            _libraryMonitor.Stop();
            var mediaPath = mediaPathDto.PathInfo ?? new MediaPathInfo(mediaPathDto.Path ?? throw new ArgumentException("PathInfo and Path can't both be null."));

            _libraryManager.AddMediaPath(mediaPathDto.Name, mediaPath);
        }
        finally
        {
            StartBackgroundLibraryOperation(
                async () =>
                {
                    if (refreshLibrary)
                    {
                        await _libraryManager.ValidateMediaLibrary(new Progress<double>(), CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        // Need to add a delay here or directory watchers may still pick up the changes
                        await Task.Delay(1000).ConfigureAwait(false);
                        _libraryMonitor.Start();
                    }
                },
                operationLease,
                "add media path");
        }

        return NoContent();
    }

    /// <summary>
    /// Updates a media path.
    /// </summary>
    /// <param name="mediaPathRequestDto">The name of the library and path infos.</param>
    /// <returns>A <see cref="NoContentResult"/>.</returns>
    /// <response code="204">Media path updated.</response>
    /// <exception cref="ArgumentNullException">The name of the library may not be empty.</exception>
    [HttpPost("Paths/Update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult UpdateMediaPath([FromBody, Required] UpdateMediaPathRequestDto mediaPathRequestDto)
    {
        if (string.IsNullOrWhiteSpace(mediaPathRequestDto.Name))
        {
            throw new ArgumentNullException(nameof(mediaPathRequestDto), "Name must not be null or empty");
        }

        _libraryManager.UpdateMediaPath(mediaPathRequestDto.Name, mediaPathRequestDto.PathInfo);
        return NoContent();
    }

    /// <summary>
    /// Remove a media path.
    /// </summary>
    /// <param name="name">The name of the library.</param>
    /// <param name="path">The path to remove.</param>
    /// <param name="refreshLibrary">Whether to refresh the library.</param>
    /// <returns>A <see cref="NoContentResult"/>.</returns>
    /// <response code="204">Media path removed.</response>
    /// <exception cref="ArgumentException">The name of the library and path may not be empty.</exception>
    [HttpDelete("Paths")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult RemoveMediaPath(
        [FromQuery] string name,
        [FromQuery] string path,
        [FromQuery] bool refreshLibrary = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var operationLease = LibraryBackgroundOperationGate.TryAcquire();
        if (operationLease is null)
        {
            Response.Headers.RetryAfter = "1";
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        try
        {
            _libraryMonitor.Stop();
            _libraryManager.RemoveMediaPath(name, path);
        }
        finally
        {
            StartBackgroundLibraryOperation(
                async () =>
                {
                    if (refreshLibrary)
                    {
                        await _libraryManager.ValidateMediaLibrary(new Progress<double>(), CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        // Need to add a delay here or directory watchers may still pick up the changes
                        await Task.Delay(1000).ConfigureAwait(false);
                        _libraryMonitor.Start();
                    }
                },
                operationLease,
                "remove media path");
        }

        return NoContent();
    }

    /// <summary>
    /// Update library options.
    /// </summary>
    /// <param name="request">The library name and options.</param>
    /// <response code="204">Library updated.</response>
    /// <response code="404">Item not found.</response>
    /// <returns>A <see cref="NoContentResult"/>.</returns>
    [HttpPost("LibraryOptions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult UpdateLibraryOptions(
        [FromBody] UpdateLibraryOptionsDto request)
    {
        var item = _libraryManager.GetItemById<CollectionFolder>(request.Id);
        if (item is null)
        {
            var rawItem = _libraryManager.GetItemById(request.Id);
            if (rawItem is CollectionFolder colFolder)
            {
                item = colFolder;
            }
            else
            {
                item = _libraryManager.GetUserRootFolder().Children.OfType<CollectionFolder>().FirstOrDefault(f => f.Id == request.Id);
            }
        }

        if (item is null)
        {
            return NotFound();
        }

        var libraryOptions = request.LibraryOptions ?? new LibraryOptions();
        ApplyLibraryDefaults(libraryOptions);

        LibraryOptions options = item.GetLibraryOptions();
        foreach (var mediaPath in libraryOptions.PathInfos)
        {
            if (options.PathInfos.Any(i => i.Path == mediaPath.Path))
            {
                continue;
            }

            _libraryManager.CreateShortcut(item.Path, mediaPath);
        }

        item.UpdateLibraryOptions(libraryOptions);
        return NoContent();
    }

    internal static bool IsPathWithinRoot(string rootPath, string candidatePath)
    {
        try
        {
            var fullRootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
            var fullCandidatePath = Path.GetFullPath(candidatePath);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            var volumeRoot = Path.GetPathRoot(fullRootPath);
            if (volumeRoot is null || ContainsReparsePoint(volumeRoot, fullRootPath))
            {
                return false;
            }

            var relativePath = Path.GetRelativePath(fullRootPath, fullCandidatePath);
            if (relativePath == "."
                || Path.IsPathRooted(relativePath)
                || relativePath == ".."
                || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, comparison)
                || relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, comparison))
            {
                return false;
            }

            return !ContainsReparsePoint(fullRootPath, fullCandidatePath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ContainsReparsePoint(string rootPath, string candidatePath)
    {
        var currentPath = rootPath;
        foreach (var segment in Path.GetRelativePath(rootPath, candidatePath).Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            try
            {
                if ((System.IO.File.GetAttributes(currentPath) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }
            catch (FileNotFoundException)
            {
                // A not-yet-created destination is valid; no later component can exist beneath it.
                return false;
            }
            catch (DirectoryNotFoundException)
            {
                // A not-yet-created destination is valid; no later component can exist beneath it.
                return false;
            }
        }

        return false;
    }

    internal static bool IsSinglePathSegment(string? name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name != "."
            && name != ".."
            && name.IndexOf('\0', StringComparison.Ordinal) < 0
            && !Path.IsPathRooted(name)
            && name.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, '/', '\\' }) < 0;
    }

    private void ApplyLibraryDefaults(LibraryOptions options)
    {
        options.EnableRealtimeMonitor = true;

        if (string.IsNullOrWhiteSpace(options.MetadataCountryCode))
        {
            options.MetadataCountryCode = _serverConfigurationManager.Configuration.MetadataCountryCode;
        }

        if (string.IsNullOrWhiteSpace(options.PreferredMetadataLanguage))
        {
            var serverLang = _serverConfigurationManager.Configuration.PreferredMetadataLanguage;
            options.PreferredMetadataLanguage = !string.IsNullOrWhiteSpace(serverLang)
                ? serverLang
                : _localizationManager.GetDefaultMetadataLanguage(options.MetadataCountryCode);
        }
    }

    private void StartBackgroundLibraryOperation(Func<Task> operation, IDisposable operationLease, string operationName)
    {
        try
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await operation().ConfigureAwait(false);
                }
#pragma warning disable CA1031 // Keep background library operations from escaping without releasing admission.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    _logger.LogError(ex, "Background library operation {OperationName} failed.", operationName);
                    try
                    {
                        _libraryMonitor.Start();
                    }
#pragma warning disable CA1031 // Log monitor recovery failures while always releasing the admission lease.
                    catch (Exception restartException)
#pragma warning restore CA1031
                    {
                        _logger.LogError(restartException, "Unable to restart the library monitor after {OperationName} failed.", operationName);
                    }
                }
                finally
                {
                    operationLease.Dispose();
                }
            });
        }
#pragma warning disable CA1031 // A scheduler failure must not permanently hold the library admission gate.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            operationLease.Dispose();
            _logger.LogError(ex, "Unable to schedule background library operation {OperationName}.", operationName);
        }
    }
}
