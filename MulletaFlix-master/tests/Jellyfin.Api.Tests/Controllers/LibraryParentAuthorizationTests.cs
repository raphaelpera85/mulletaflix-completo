using System;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class LibraryParentAuthorizationTests
{
    public static TheoryData<Type, string> ParentScopedActions => new()
    {
        { typeof(GenresController), "GetGenres" },
        { typeof(ArtistsController), "GetArtists" },
        { typeof(ArtistsController), "GetAlbumArtists" },
        { typeof(ItemsController), "GetItems" },
        { typeof(MusicGenresController), "GetMusicGenres" },
        { typeof(StudiosController), "GetStudios" },
        { typeof(YearsController), "GetYears" }
    };

    [Theory]
    [MemberData(nameof(ParentScopedActions))]
    public async Task Action_ReturnsNotFoundForParentOutsideRequestUsersLibrary(Type controllerType, string actionName)
    {
        var user = CreateUser();
        var parentId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(parentId, user)).Returns((BaseItem?)null);
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(user);

        var controller = CreateController(controllerType, libraryManager.Object, userManager.Object);
        var identity = new ClaimsIdentity(
            [
                new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")),
                new Claim(InternalClaimTypes.IsApiKey, bool.FalseString),
                new Claim(ClaimTypes.Name, user.Username)
            ],
            "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var action = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public)!;
        var parameters = action.GetParameters();
        var arguments = parameters.Select(CreateDefaultArgument).ToArray();
        arguments[Array.FindIndex(parameters, parameter => parameter.Name == "userId")] = user.Id;
        arguments[Array.FindIndex(parameters, parameter => parameter.Name == "parentId")] = parentId;

        var invocation = action.Invoke(controller, arguments);
        if (invocation is Task task)
        {
            await task;
            invocation = task.GetType().GetProperty("Result")!.GetValue(task);
        }

        var actionResult = invocation!.GetType().GetProperty("Result")!.GetValue(invocation);
        Assert.IsType<NotFoundResult>(actionResult);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(parentId, user), Times.Once);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(parentId), Times.Never);
        libraryManager.Verify(library => library.GetParentItem(parentId, user.Id), Times.Never);
    }

    private static object? CreateDefaultArgument(ParameterInfo parameter)
    {
        if (parameter.ParameterType.IsArray)
        {
            return Array.CreateInstance(parameter.ParameterType.GetElementType()!, 0);
        }

        if (parameter.ParameterType.IsValueType)
        {
            return Activator.CreateInstance(parameter.ParameterType);
        }

        return null;
    }

    private static ControllerBase CreateController(Type controllerType, ILibraryManager libraryManager, IUserManager userManager)
    {
        var dtoService = Mock.Of<IDtoService>();
        if (controllerType == typeof(GenresController))
        {
            return new GenresController(userManager, libraryManager, dtoService);
        }

        if (controllerType == typeof(ArtistsController))
        {
            return new ArtistsController(libraryManager, userManager, dtoService);
        }

        if (controllerType == typeof(ItemsController))
        {
            return new ItemsController(
                userManager,
                libraryManager,
                Mock.Of<ILocalizationManager>(),
                dtoService,
                NullLogger<ItemsController>.Instance,
                Mock.Of<ISessionManager>(),
                Mock.Of<IUserDataManager>(),
                new MemoryCache(new MemoryCacheOptions()));
        }

        if (controllerType == typeof(MusicGenresController))
        {
            return new MusicGenresController(libraryManager, userManager, dtoService);
        }

        if (controllerType == typeof(StudiosController))
        {
            return new StudiosController(libraryManager, userManager, dtoService);
        }

        if (controllerType == typeof(YearsController))
        {
            return new YearsController(libraryManager, userManager, dtoService);
        }

        throw new ArgumentOutOfRangeException(nameof(controllerType));
    }

    private static User CreateUser()
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();
        return user;
    }
}
