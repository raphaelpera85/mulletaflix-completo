using System;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MulletaFlix.Api.Helpers;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Server.Implementations.Users;
using Moq;
using Xunit;

namespace MulletaFlix.Api.Tests.Helpers;

public class RequestHelpersParentAccessTests
{
    [Fact]
    public void GetParentItem_UsesUserScopedLookupForExplicitParent()
    {
        var user = CreateUser();
        var parentId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(parentId, user)).Returns((BaseItem?)null);

        var result = RequestHelpers.GetParentItem(libraryManager.Object, parentId, user.Id, user);

        Assert.Null(result);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(parentId, user), Times.Once);
        libraryManager.Verify(library => library.GetParentItem(It.IsAny<Guid?>(), It.IsAny<Guid?>()), Times.Never);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(parentId), Times.Never);
    }

    [Fact]
    public void GetParentItem_UsesUserRootWhenParentIsNotSpecified()
    {
        var user = CreateUser();
        var root = new Folder();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetParentItem(null, user.Id)).Returns(root);

        var result = RequestHelpers.GetParentItem(libraryManager.Object, null, user.Id, user);

        Assert.Same(root, result);
        libraryManager.Verify(library => library.GetParentItem(null, user.Id), Times.Once);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public void GetParentItem_FailsClosedWhenRequestedUserCannotBeResolved()
    {
        var parentId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();

        var result = RequestHelpers.GetParentItem(libraryManager.Object, parentId, userId, null);

        Assert.Null(result);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(It.IsAny<Guid>(), It.IsAny<User>()), Times.Never);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public void GetParentItem_FailsClosedForRootWhenRequestedUserCannotBeResolved()
    {
        var userId = Guid.NewGuid();
        var libraryManager = new Mock<ILibraryManager>();

        var result = RequestHelpers.GetParentItem(libraryManager.Object, null, userId, null);

        Assert.Null(result);
        libraryManager.Verify(library => library.GetParentItem(It.IsAny<Guid?>(), It.IsAny<Guid?>()), Times.Never);
    }

    [Fact]
    public void GetParentItem_AllowsUnscopedLookupForApiKeyWithoutUserId()
    {
        var parentId = Guid.NewGuid();
        var parent = new Folder();
        var libraryManager = new Mock<ILibraryManager>();
        libraryManager.Setup(library => library.GetItemById<BaseItem>(parentId, null)).Returns(parent);

        var result = RequestHelpers.GetParentItem(libraryManager.Object, parentId, Guid.Empty, null);

        Assert.Same(parent, result);
        libraryManager.Verify(library => library.GetItemById<BaseItem>(parentId, null), Times.Once);
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
