using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Books;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MulletaFlix.Api.Constants;
using MulletaFlix.Api.Controllers;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Xunit;

namespace MulletaFlix.Api.Tests.Controllers;

public class BookReaderControllerAuthorizationTests
{
    [Fact]
    public async Task GetStatus_DoesNotExposeBookOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = await fixture.Controller.GetStatus(fixture.BookId);

        Assert.IsType<NotFoundResult>(result.Result);
        fixture.ConversionService.Verify(
            service => service.GetStatusAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.BookId, fixture.User),
            Times.Once);
    }

    [Fact]
    public async Task GetEpub_DoesNotExposeBookOutsideRequestUsersLibrary()
    {
        var fixture = CreateController();

        var result = await fixture.Controller.GetEpub(fixture.BookId);

        Assert.IsType<NotFoundResult>(result);
        fixture.ConversionService.Verify(
            service => service.GetEpubStreamAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(fixture.BookId, fixture.User),
            Times.Once);
    }

    [Fact]
    public async Task GetStatus_AllowsBookVisibleToRequestUser()
    {
        var fixture = CreateController(bookIsVisible: true);

        var result = await fixture.Controller.GetStatus(fixture.BookId);

        Assert.Equal(BookReaderStatus.Unsupported.ToString(), result.Value?.Status);
        fixture.ConversionService.Verify(
            service => service.GetStatusAsync(fixture.BookId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatus_RejectsAuthenticatedPrincipalWithoutKnownUser()
    {
        var fixture = CreateController(userExists: false);

        var result = await fixture.Controller.GetStatus(fixture.BookId);

        Assert.IsType<UnauthorizedResult>(result.Result);
        fixture.LibraryManager.Verify(
            library => library.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<User>()),
            Times.Never);
    }

    private static ControllerFixture CreateController(bool bookIsVisible = false, bool userExists = true)
    {
        var user = new User(
            "reader",
            typeof(DefaultAuthenticationProvider).FullName!,
            typeof(DefaultPasswordResetProvider).FullName!);
        user.AddDefaultPermissions();
        user.AddDefaultPreferences();

        var bookId = Guid.NewGuid();
        var book = new Mock<Book>();
        book.Setup(item => item.IsVisibleStandalone(null)).Returns(true);
        book.Setup(item => item.IsVisibleStandalone(user)).Returns(bookIsVisible);

        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(user.Id)).Returns(userExists ? user : null);

        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById(bookId)).Returns(book.Object);
        libraryManager.Setup(library => library.GetItemById<BaseItem>(bookId, user))
            .Returns(bookIsVisible ? book.Object : null);

        var conversionService = new Mock<IBookConversionService>();
        conversionService.Setup(service => service.GetStatusAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BookReaderStatus.Unsupported);
        conversionService.Setup(service => service.GetEpubStreamAsync(bookId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((BookStreamResult?)null);

        var controller = new BookReaderController(
            conversionService.Object,
            libraryManager.Object,
            userManager.Object,
            Mock.Of<ILogger<BookReaderController>>());
        var identity = new ClaimsIdentity(
        new Claim[]
        {
            new Claim(InternalClaimTypes.UserId, user.Id.ToString("N")),
            new Claim(ClaimTypes.Name, user.Username)
        },
        "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return new ControllerFixture(controller, user, bookId, libraryManager, conversionService);
    }

    private sealed record ControllerFixture(
        BookReaderController Controller,
        User User,
        Guid BookId,
        Mock<ILibraryManager> LibraryManager,
        Mock<IBookConversionService> ConversionService);
}
