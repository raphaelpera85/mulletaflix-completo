using System;
using System.Linq;
using MulletaFlix.Api.Extensions;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Api.ModelBinders;
using MulletaFlix.Data.Enums;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Extensions;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MulletaFlix.Api.Controllers;

/// <summary>
/// Filters controller.
/// </summary>
[Route("")]
[Authorize]
public class FilterController : BaseMulletaFlixApiController
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
    private readonly ILocalizationManager _localization;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterController"/> class.
    /// </summary>
    /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
    public FilterController(ILibraryManager libraryManager, IUserManager userManager, ILocalizationManager localization)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
        _localization = localization;
    }

    /// <summary>
    /// Gets legacy query filters.
    /// </summary>
    /// <param name="userId">Optional. User id.</param>
    /// <param name="parentId">Optional. Parent id.</param>
    /// <param name="includeItemTypes">Optional. If specified, results will be filtered based on item type. This allows multiple, comma delimited.</param>
    /// <param name="mediaTypes">Optional. Filter by MediaType. Allows multiple, comma delimited.</param>
    /// <response code="200">Legacy filters retrieved.</response>
    /// <returns>Legacy query filters.</returns>
    [HttpGet("Items/Filters")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<QueryFiltersLegacy> GetQueryFiltersLegacy(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? parentId,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] BaseItemKind[] includeItemTypes,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] MediaType[] mediaTypes)
    {
        userId = RequestHelpers.GetUserId(User, userId);
        if (!TryResolveFilterUser(userId, out var user))
        {
            return Unauthorized();
        }

        BaseItem? item = null;
        if (includeItemTypes.Length != 1
            || !(includeItemTypes[0] == BaseItemKind.Trailer
                 || includeItemTypes[0] == BaseItemKind.Program))
        {
            if (parentId.HasValue)
            {
                item = RequestHelpers.GetParentItem(_libraryManager, parentId, userId, user);
            }
            else
            {
                item = _libraryManager.GetParentItem(null, user?.Id);
            }
        }

        if (item is not Folder folder)
        {
            return new QueryFiltersLegacy();
        }

        var query = new InternalItemsQuery(user)
        {
            MediaTypes = mediaTypes,
            IncludeItemTypes = includeItemTypes,
            Recursive = true,
            EnableTotalRecordCount = false,
            AncestorIds = [folder.Id],
            DtoOptions = new DtoOptions
            {
                Fields = [],
                EnableImages = false,
                EnableUserData = false
            }
        };

        return _libraryManager.GetQueryFiltersLegacy(query);
    }

    /// <summary>
    /// Gets query filters.
    /// </summary>
    /// <param name="userId">Optional. User id.</param>
    /// <param name="parentId">Optional. Specify this to localize the search to a specific item or folder. Omit to use the root.</param>
    /// <param name="includeItemTypes">Optional. If specified, results will be filtered based on item type. This allows multiple, comma delimited.</param>
    /// <param name="isAiring">Optional. Is item airing.</param>
    /// <param name="isMovie">Optional. Is item movie.</param>
    /// <param name="isSports">Optional. Is item sports.</param>
    /// <param name="isKids">Optional. Is item kids.</param>
    /// <param name="isNews">Optional. Is item news.</param>
    /// <param name="isSeries">Optional. Is item series.</param>
    /// <param name="recursive">Optional. Search recursive.</param>
    /// <response code="200">Filters retrieved.</response>
    /// <returns>Query filters.</returns>
    [HttpGet("Items/Filters2")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<QueryFilters> GetQueryFilters(
        [FromQuery] Guid? userId,
        [FromQuery] Guid? parentId,
        [FromQuery, ModelBinder(typeof(CommaDelimitedCollectionModelBinder))] BaseItemKind[] includeItemTypes,
        [FromQuery] bool? isAiring,
        [FromQuery] bool? isMovie,
        [FromQuery] bool? isSports,
        [FromQuery] bool? isKids,
        [FromQuery] bool? isNews,
        [FromQuery] bool? isSeries,
        [FromQuery] bool? recursive)
    {
        userId = RequestHelpers.GetUserId(User, userId);
        if (!TryResolveFilterUser(userId, out var user))
        {
            return Unauthorized();
        }

        BaseItem? parentItem = null;
        if (includeItemTypes.Length == 1
            && (includeItemTypes[0] == BaseItemKind.Trailer
                || includeItemTypes[0] == BaseItemKind.Program))
        {
            parentItem = null;
        }
        else if (parentId.HasValue)
        {
            parentItem = RequestHelpers.GetParentItem(_libraryManager, parentId, userId, user);
            if (parentItem is null)
            {
                return new QueryFilters();
            }
        }

        var filters = new QueryFilters();
        var genreQuery = new InternalItemsQuery(user)
        {
            IncludeItemTypes = includeItemTypes,
            DtoOptions = new DtoOptions
            {
                Fields = Array.Empty<ItemFields>(),
                EnableImages = false,
                EnableUserData = false
            },
            IsAiring = isAiring,
            IsMovie = isMovie,
            IsSports = isSports,
            IsKids = isKids,
            IsNews = isNews,
            IsSeries = isSeries
        };

        if ((recursive ?? true) || parentItem is UserView || parentItem is ICollectionFolder)
        {
            genreQuery.AncestorIds = parentItem is null ? Array.Empty<Guid>() : new[] { parentItem.Id };
        }
        else
        {
            genreQuery.Parent = parentItem;
        }

        if (includeItemTypes.Length == 1
            && (includeItemTypes[0] == BaseItemKind.MusicAlbum
                || includeItemTypes[0] == BaseItemKind.MusicVideo
                || includeItemTypes[0] == BaseItemKind.MusicArtist
                || includeItemTypes[0] == BaseItemKind.Audio))
        {
            filters.Genres = _libraryManager.GetMusicGenres(genreQuery).Items.Select(i => new NameGuidPair
            {
                Name = i.Item.Name,
                Id = i.Item.Id
            }).ToArray();
        }
        else
        {
            filters.Genres = _libraryManager.GetGenres(genreQuery).Items.Select(i => new NameGuidPair
            {
                Name = i.Item.Name,
                Id = i.Item.Id
            }).ToArray();
        }

        if (includeItemTypes.Contains(BaseItemKind.Movie) || includeItemTypes.Contains(BaseItemKind.Series))
        {
            filters.AudioLanguages = _libraryManager
                .GetMediaStreamLanguages(MediaStreamType.Audio)
                .Select(language =>
                {
                    var culture = _localization.FindLanguageInfo(language);
                    return new NameValuePair
                    {
                        Name = culture is null ? language : $"{culture.DisplayName} ({language})",
                        Value = language
                    };
                })
                .OrderBy(l => l.Name)
                .ToArray();
            filters.SubtitleLanguages = _libraryManager
                .GetMediaStreamLanguages(MediaStreamType.Subtitle)
                .Select(language =>
                {
                    var culture = _localization.FindLanguageInfo(language);
                    return new NameValuePair
                    {
                        Name = culture is null ? language : $"{culture.DisplayName} ({language})",
                        Value = language
                    };
                })
                .OrderBy(l => l.Name)
                .ToArray();
        }

        return filters;
    }

    private bool TryResolveFilterUser(Guid? userId, out User? user)
    {
        user = null;
        if (userId.IsNullOrEmpty())
        {
            return User.GetIsApiKey();
        }

        user = _userManager.GetUserById(userId.Value);
        return user is not null;
    }
}
