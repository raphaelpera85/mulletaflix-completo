using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Devices;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Data;
using Moq;
using MulletaFlix.Database.Implementations.Enums;
using MulletaFlix.Plugin.GetAvatar.Configuration;
using MulletaFlix.Plugin.GetAvatar.Controllers;
using MulletaFlix.Plugin.GetAvatar.Services;
using Xunit;
using GetAvatarPlugin = MulletaFlix.Plugin.GetAvatar.Plugin;
using ImageInfo = MulletaFlix.Database.Implementations.Entities.ImageInfo;
using User = MulletaFlix.Database.Implementations.Entities.User;

namespace MulletaFlix.Api.Tests.Controllers;

[Collection(GetAvatarPluginTestCollection.Name)]
public sealed class AvatarControllerUserAvatarAuthorizationTests
{
    [Fact]
    public async Task SaveAvatarAsync_WhenConfigurationPersistenceFails_RollsBackFileAndCatalogEntry()
    {
        using var context = new AvatarTestContext();
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new IOException("simulated configuration write failure"));
        var service = context.CreateAvatarService();

        await Assert.ThrowsAsync<IOException>(() => service.SaveAvatarAsync("new-avatar.png", [4, 5, 6]));

        Assert.Single(GetAvatarPlugin.Config.AvailableAvatars!);
        Assert.Equal("test-avatar", GetAvatarPlugin.Config.AvailableAvatars![0].Id);
        Assert.Equal(["test-avatar.png"], Directory.GetFiles(context.AvatarDirectory).Select(Path.GetFileName));
    }

    [Fact]
    public void OpenAvatarRead_WhenConfiguredFileNameEscapesAvatarDirectory_ReturnsNull()
    {
        using var context = new AvatarTestContext();
        var outsideFile = Path.Combine(context.Root, "outside-avatar.png");
        File.WriteAllBytes(outsideFile, [9, 8, 7]);
        var escapedFileName = Path.GetRelativePath(context.AvatarDirectory, outsideFile);
        GetAvatarPlugin.Config.AvailableAvatars = [new AvatarInfo { Id = "outside-avatar", FileName = escapedFileName }];
        var service = context.CreateAvatarService();

        var result = service.OpenAvatarRead("outside-avatar");

        Assert.Null(result);
        Assert.True(File.Exists(outsideFile));
    }

    [Fact]
    public void DeleteAvatar_WhenConfiguredFileNameEscapesAvatarDirectory_PreservesOutsideFileAndCatalogEntry()
    {
        using var context = new AvatarTestContext();
        var outsideFile = Path.Combine(context.Root, "outside-avatar.png");
        File.WriteAllBytes(outsideFile, [9, 8, 7]);
        var avatar = new AvatarInfo
        {
            Id = "outside-avatar",
            FileName = Path.GetRelativePath(context.AvatarDirectory, outsideFile)
        };
        var mapping = new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = "outside-avatar" };
        GetAvatarPlugin.Config.AvailableAvatars = [avatar];
        GetAvatarPlugin.Config.UserAvatars = [mapping];
        var service = context.CreateAvatarService();

        var deleted = service.DeleteAvatar("outside-avatar");

        Assert.False(deleted);
        Assert.True(File.Exists(outsideFile));
        Assert.Contains(avatar, GetAvatarPlugin.Config.AvailableAvatars!);
        Assert.Contains(mapping, GetAvatarPlugin.Config.UserAvatars!);
    }

    [Fact]
    public void DeleteAvatar_WhenConfiguredFileIsMissing_RemovesStaleCatalogEntryAndMapping()
    {
        using var context = new AvatarTestContext();
        var avatar = new AvatarInfo { Id = "stale-avatar", FileName = "missing-avatar.png" };
        var mapping = new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = avatar.Id };
        GetAvatarPlugin.Config.AvailableAvatars = [avatar];
        GetAvatarPlugin.Config.UserAvatars = [mapping];
        var service = context.CreateAvatarService();

        Assert.True(service.DeleteAvatar(avatar.Id));

        Assert.Empty(GetAvatarPlugin.Config.AvailableAvatars!);
        Assert.Empty(GetAvatarPlugin.Config.UserAvatars!);
        Assert.False(File.Exists(Path.Combine(context.AvatarDirectory, avatar.FileName)));
    }

    [Fact]
    public void DeleteAvatar_WhenConfigurationPersistenceFails_PreservesFileCatalogAndMappings()
    {
        using var context = new AvatarTestContext();
        var avatar = new AvatarInfo { Id = "persist-failure-avatar", FileName = "persist-failure-avatar.png" };
        var mapping = new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = avatar.Id };
        var avatarPath = Path.Combine(context.AvatarDirectory, avatar.FileName);
        var previousConfiguration = new byte[] { 60, 111, 108, 100, 62 };
        File.WriteAllBytes(avatarPath, [5, 6, 7]);
        File.WriteAllBytes(GetAvatarPlugin.Instance!.ConfigurationFilePath, previousConfiguration);
        GetAvatarPlugin.Config.AvailableAvatars = [avatar];
        GetAvatarPlugin.Config.UserAvatars = [mapping];
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((_, path) => File.WriteAllBytes(path, [1, 2, 3]))
            .Throws(new IOException("simulated configuration write failure"));
        var service = context.CreateAvatarService();

        Assert.False(service.DeleteAvatar(avatar.Id));

        Assert.Equal([5, 6, 7], File.ReadAllBytes(avatarPath));
        Assert.Contains(avatar, GetAvatarPlugin.Config.AvailableAvatars!);
        Assert.Contains(mapping, GetAvatarPlugin.Config.UserAvatars!);
        Assert.Equal(previousConfiguration, File.ReadAllBytes(GetAvatarPlugin.Instance!.ConfigurationFilePath));
    }

    [Fact]
    public void OpenAvatarRead_WhenConfiguredFileIsSymlinkToOutside_ReturnsNull()
    {
        using var context = new AvatarTestContext();
        var outsideFile = Path.Combine(context.Root, "outside-avatar.png");
        var link = Path.Combine(context.AvatarDirectory, "linked-avatar.png");
        File.WriteAllBytes(outsideFile, [9, 8, 7]);
        try
        {
            File.CreateSymbolicLink(link, outsideFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {exception.GetType().Name}.");
        }

        GetAvatarPlugin.Config.AvailableAvatars = [new AvatarInfo { Id = "linked-avatar", FileName = Path.GetFileName(link) }];
        var service = context.CreateAvatarService();

        using var result = service.OpenAvatarRead("linked-avatar");
        Assert.Null(result);
        Assert.True(File.Exists(link));
        Assert.Equal([9, 8, 7], File.ReadAllBytes(outsideFile));
    }

    [Fact]
    public void DeleteAvatar_WhenConfiguredFileIsSymlinkToOutside_PreservesLinkAndCatalog()
    {
        using var context = new AvatarTestContext();
        var outsideFile = Path.Combine(context.Root, "outside-avatar.png");
        var link = Path.Combine(context.AvatarDirectory, "linked-avatar.png");
        File.WriteAllBytes(outsideFile, [9, 8, 7]);
        try
        {
            File.CreateSymbolicLink(link, outsideFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {exception.GetType().Name}.");
        }

        var avatar = new AvatarInfo { Id = "linked-avatar", FileName = Path.GetFileName(link) };
        var mapping = new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = avatar.Id };
        GetAvatarPlugin.Config.AvailableAvatars = [avatar];
        GetAvatarPlugin.Config.UserAvatars = [mapping];
        var service = context.CreateAvatarService();

        Assert.False(service.DeleteAvatar(avatar.Id));
        Assert.True(File.Exists(link));
        Assert.Equal([9, 8, 7], File.ReadAllBytes(outsideFile));
        Assert.Contains(avatar, GetAvatarPlugin.Config.AvailableAvatars!);
        Assert.Contains(mapping, GetAvatarPlugin.Config.UserAvatars!);
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenConfigurationPersistenceFails_RestoresProfileFileAndMapping()
    {
        using var context = new AvatarTestContext();
        var profilePath = context.CreateExistingProfile(context.TargetUser);
        var uniqueProfilePath = Path.Combine(Path.GetDirectoryName(profilePath)!, "profile-0123456789abcdef0123456789abcdef.png");
        File.Move(profilePath, uniqueProfilePath);
        context.TargetUser.ProfileImage = new ImageInfo(uniqueProfilePath);
        profilePath = uniqueProfilePath;
        var previousImage = context.TargetUser.ProfileImage;
        var previousConfiguration = new byte[] { 60, 111, 108, 100, 62 };
        File.WriteAllBytes(GetAvatarPlugin.Instance!.ConfigurationFilePath, previousConfiguration);
        GetAvatarPlugin.Config.UserAvatars =
        [
            new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = "previous-avatar" }
        ];
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((_, path) => File.WriteAllBytes(path, [1, 2, 3]))
            .Throws(new IOException("simulated configuration write failure"));
        var service = context.CreateAvatarService(context.TargetUser);

        var exception = await Assert.ThrowsAsync<IOException>(() => service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar"));

        Assert.Equal("simulated configuration write failure", exception.Message);
        Assert.NotSame(previousImage, context.TargetUser.ProfileImage);
        Assert.Equal(previousImage!.Path, context.TargetUser.ProfileImage!.Path);
        Assert.Equal([10, 20, 30, 40], await File.ReadAllBytesAsync(profilePath, TestContext.Current.CancellationToken));
        Assert.Single(GetAvatarPlugin.Config.UserAvatars!);
        Assert.Equal("previous-avatar", GetAvatarPlugin.Config.UserAvatars![0].AvatarId);
        Assert.Equal(["profile-0123456789abcdef0123456789abcdef.png"], Directory.GetFiles(Path.GetDirectoryName(profilePath)!).Select(Path.GetFileName));
        Assert.Equal(previousConfiguration, await File.ReadAllBytesAsync(
            GetAvatarPlugin.Instance!.ConfigurationFilePath,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetUserAvatarAsync_RemovesPreviousUniqueProfileImageAfterSuccessfulReplacement()
    {
        using var context = new AvatarTestContext();
        var profileDirectory = Path.Combine(context.ConfigurationManager.Object.ApplicationPaths.UserConfigurationDirectoryPath, context.TargetUser.Username);
        Directory.CreateDirectory(profileDirectory);
        var previousPath = Path.Combine(profileDirectory, "profile-0123456789abcdef0123456789abcdef.png");
        await File.WriteAllBytesAsync(previousPath, [10, 20, 30, 40], TestContext.Current.CancellationToken);
        context.TargetUser.ProfileImage = new ImageInfo(previousPath);
        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .Callback(() => context.TargetUser.ProfileImage = null)
            .Returns(Task.CompletedTask);

        await service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");

        Assert.NotNull(context.TargetUser.ProfileImage);
        Assert.NotEqual(previousPath, context.TargetUser.ProfileImage.Path);
        Assert.True(File.Exists(context.TargetUser.ProfileImage.Path));
        Assert.False(File.Exists(previousPath));
    }

    [Fact]
    public async Task SetUserAvatarAsync_DoesNotDeleteSameProfilePathWithDifferentCaseOnWindows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Case-insensitive profile path behavior is specific to Windows.");

        using var context = new AvatarTestContext();
        var profileDirectory = Path.Combine(context.ConfigurationManager.Object.ApplicationPaths.UserConfigurationDirectoryPath, context.TargetUser.Username);
        Directory.CreateDirectory(profileDirectory);
        var previousPath = Path.Combine(profileDirectory, "profile.PNG");
        await File.WriteAllBytesAsync(previousPath, [10, 20, 30, 40], TestContext.Current.CancellationToken);
        context.TargetUser.ProfileImage = new ImageInfo(previousPath);
        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .Callback(() => context.TargetUser.ProfileImage = null)
            .Returns(Task.CompletedTask);

        await service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");

        var savedPath = Path.Combine(profileDirectory, "profile.png");
        Assert.True(File.Exists(savedPath));
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(savedPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenImageSaveFails_PreservesConcurrentConfigurationWrite()
    {
        using var context = new AvatarTestContext();
        var configurationPath = GetAvatarPlugin.Instance!.ConfigurationFilePath;
        var originalConfiguration = new byte[] { 60, 111, 108, 100, 62 };
        var updatedConfiguration = new byte[] { 60, 110, 101, 119, 62 };
        File.WriteAllBytes(configurationPath, originalConfiguration);
        var imageSaveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowImageSaveToFail = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = context.CreateAvatarService(context.TargetUser);
        context.ProviderManager!
            .Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns<Stream, string, string>(async (_, _, _) =>
            {
                imageSaveEntered.TrySetResult();
                await allowImageSaveToFail.Task;
                throw new IOException("simulated profile image write failure");
            });
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Callback<object, string>((_, path) => File.WriteAllBytes(path, updatedConfiguration));

        var assignment = service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");
        await imageSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var controller = context.CreateController(context.TargetUser).Controller;
        controller.UpdateSettings(new MulletaFlix.Plugin.GetAvatar.Controllers.PluginSettingsRequest { EnableAutoAssign = true });
        allowImageSaveToFail.TrySetResult();

        await Assert.ThrowsAsync<IOException>(() => assignment);

        Assert.True(GetAvatarPlugin.Config.EnableAutoAssign);
        Assert.Equal(updatedConfiguration, await File.ReadAllBytesAsync(configurationPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenSaveFails_DoesNotRestoreExternalProfileFileSnapshot()
    {
        using var context = new AvatarTestContext();
        Directory.CreateDirectory(context.AvatarDirectory);
        var externalProfilePath = Path.Combine(context.AvatarDirectory, "external-profile.png");
        var concurrentBytes = new byte[] { 91, 92, 93 };
        await File.WriteAllBytesAsync(externalProfilePath, [1, 2, 3], TestContext.Current.CancellationToken);
        context.TargetUser.ProfileImage = new ImageInfo(externalProfilePath);
        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .Callback(() => context.TargetUser.ProfileImage = null)
            .Returns(Task.CompletedTask);
        context.ProviderManager!
            .Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<Stream, string, string>((_, _, _) => File.WriteAllBytes(externalProfilePath, concurrentBytes))
            .ThrowsAsync(new IOException("simulated image save failure"));

        await Assert.ThrowsAsync<IOException>(() => service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar"));

        Assert.Equal(concurrentBytes, await File.ReadAllBytesAsync(externalProfilePath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenPreviousMappingCollectionIsNull_RestoresAnEmptyCollection()
    {
        using var context = new AvatarTestContext();
        context.CreateExistingProfile(context.TargetUser);
        GetAvatarPlugin.Config.UserAvatars = null!;
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new IOException("simulated configuration write failure"));
        var service = context.CreateAvatarService(context.TargetUser);

        await Assert.ThrowsAsync<IOException>(() => service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar"));

        Assert.NotNull(GetAvatarPlugin.Config.UserAvatars);
        Assert.Empty(GetAvatarPlugin.Config.UserAvatars);
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenClearingExistingProfileFails_DoesNotReinsertExistingImage()
    {
        using var context = new AvatarTestContext();
        var profilePath = context.CreateExistingProfile(context.TargetUser);
        var previousImage = context.TargetUser.ProfileImage;
        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .ThrowsAsync(new IOException("simulated profile removal failure"));

        await Assert.ThrowsAsync<IOException>(() => service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar"));

        Assert.Same(previousImage, context.TargetUser.ProfileImage);
        Assert.Equal([10, 20, 30, 40], await File.ReadAllBytesAsync(profilePath, TestContext.Current.CancellationToken));
        context.UserManager.Verify(manager => manager.UpdateUserAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenProfileRemovalMutatesThenThrows_RestoresPersistedProfileMetadata()
    {
        using var context = new AvatarTestContext();
        var profilePath = context.CreateExistingProfile(context.TargetUser);
        var previousImage = context.TargetUser.ProfileImage;
        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .Returns(() =>
            {
                File.Delete(profilePath);
                context.TargetUser.ProfileImage = null;
                throw new IOException("simulated failure after profile removal committed");
            });
        context.UserManager
            .Setup(manager => manager.UpdateUserAsync(context.TargetUser))
            .Returns(Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<IOException>(
            () => service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar"));

        Assert.Equal("simulated failure after profile removal committed", exception.Message);
        Assert.NotSame(previousImage, context.TargetUser.ProfileImage);
        Assert.Equal(profilePath, context.TargetUser.ProfileImage!.Path);
        Assert.Equal(0, context.TargetUser.ProfileImage.Id);
        Assert.Equal([10, 20, 30, 40], await File.ReadAllBytesAsync(profilePath, TestContext.Current.CancellationToken));
        context.UserManager.Verify(manager => manager.GetUserById(context.TargetUser.Id), Times.Exactly(2));
        context.UserManager.Verify(manager => manager.UpdateUserAsync(context.TargetUser), Times.Once);
    }

    [Fact]
    public async Task SetUserAvatarAsync_WhenUserDisappearsDuringFailedProfileRemoval_DoesNotReinsertUser()
    {
        using var context = new AvatarTestContext();
        var profilePath = context.CreateExistingProfile(context.TargetUser);
        var previousImage = context.TargetUser.ProfileImage;
        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!
            .SetupSequence(manager => manager.GetUserById(context.TargetUser.Id))
            .Returns(context.TargetUser)
            .Returns((User?)null);
        context.UserManager
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .Returns(() =>
            {
                File.Delete(profilePath);
                context.TargetUser.ProfileImage = null;
                throw new IOException("simulated failure after profile removal committed");
            });

        await Assert.ThrowsAsync<IOException>(
            () => service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar"));

        Assert.Same(previousImage, context.TargetUser.ProfileImage);
        Assert.Equal([10, 20, 30, 40], await File.ReadAllBytesAsync(profilePath, TestContext.Current.CancellationToken));
        context.UserManager.Verify(manager => manager.UpdateUserAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task SetUserAvatarAsync_SerializesConcurrentProfileChanges()
    {
        using var context = new AvatarTestContext();
        context.CreateExistingProfile(context.TargetUser);
        var service = context.CreateAvatarService(context.TargetUser);
        var firstClearEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstClearToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clearCount = 0;
        context.UserManager!
            .Setup(manager => manager.ClearProfileImageAsync(context.TargetUser))
            .Returns(async () =>
            {
                var call = Interlocked.Increment(ref clearCount);
                if (call == 1)
                {
                    firstClearEntered.SetResult();
                    await allowFirstClearToFinish.Task;
                }

                if (context.TargetUser.ProfileImage is not null && File.Exists(context.TargetUser.ProfileImage.Path))
                {
                    File.Delete(context.TargetUser.ProfileImage.Path);
                }

                context.TargetUser.ProfileImage = null;
            });

        var firstUpdate = service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");
        await firstClearEntered.Task;
        var secondUpdate = service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");

        Assert.False(secondUpdate.IsCompleted);
        Assert.Equal(1, Volatile.Read(ref clearCount));

        allowFirstClearToFinish.SetResult();
        await Task.WhenAll(firstUpdate, secondUpdate);

        Assert.Equal(2, Volatile.Read(ref clearCount));
        Assert.Single(GetAvatarPlugin.Config.UserAvatars!);
        Assert.Equal("test-avatar", GetAvatarPlugin.Config.UserAvatars![0].AvatarId);
    }

    [Fact]
    public async Task RemoveUserAvatarAsync_WaitsForConcurrentProfileAssignment()
    {
        using var context = new AvatarTestContext();
        context.CreateExistingProfile(context.TargetUser);
        var service = context.CreateAvatarService(context.TargetUser);
        var imageSaveEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowImageSaveToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.ProviderManager!
            .Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns<Stream, string, string>(async (_, _, path) =>
            {
                imageSaveEntered.TrySetResult();
                await allowImageSaveToFinish.Task;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using var destination = File.Create(path);
                await destination.WriteAsync(new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);
            });

        var assignment = service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");
        await imageSaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var removal = service.RemoveUserAvatarAsync(context.TargetUser.Id);
        var removalWaitedForAssignment = !removal.IsCompleted;

        allowImageSaveToFinish.TrySetResult();
        await assignment;
        var removed = await removal;

        Assert.True(removalWaitedForAssignment);
        Assert.True(removed);
        Assert.Null(context.TargetUser.ProfileImage);
        Assert.Empty(GetAvatarPlugin.Config.UserAvatars!);
    }

    [Fact]
    public async Task ValidateUserAvatarsAsync_WaitsForConcurrentProfileAssignmentWhenMappingIsStale()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.ProfileImage = new ImageInfo(Path.Combine(context.Root, "missing-profile.png"));
        GetAvatarPlugin.Config.UserAvatars =
        [
            new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = "missing-avatar" }
        ];
        var service = context.CreateAvatarService(context.TargetUser);
        var profileClearEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowProfileClearToFinish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        context.UserManager!
            .Setup(manager => manager.UpdateUserAsync(context.TargetUser))
            .Returns(() =>
            {
                profileClearEntered.TrySetResult();
                return allowProfileClearToFinish.Task;
            });

        var validation = service.ValidateUserAvatarsAsync();
        await profileClearEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var assignment = service.SetUserAvatarAsync(context.TargetUser.Id, "test-avatar");
        var assignmentWaitedForValidation = !assignment.IsCompleted;

        allowProfileClearToFinish.TrySetResult();
        await Task.WhenAll(validation, assignment);

        Assert.True(assignmentWaitedForValidation);
        Assert.NotNull(context.TargetUser.ProfileImage);
        Assert.True(File.Exists(context.TargetUser.ProfileImage!.Path));
        Assert.Single(GetAvatarPlugin.Config.UserAvatars!);
        Assert.Equal("test-avatar", GetAvatarPlugin.Config.UserAvatars![0].AvatarId);
    }

    [Fact]
    public async Task RemoveUserAvatarAsync_DoesNotDeleteProfileFileOutsideUserDirectory()
    {
        using var context = new AvatarTestContext();
        var outsideProfilePath = Path.Combine(context.Root, "outside-profile.png");
        await File.WriteAllBytesAsync(outsideProfilePath, [9, 8, 7], TestContext.Current.CancellationToken);
        context.TargetUser.ProfileImage = new ImageInfo(outsideProfilePath);
        var service = context.CreateAvatarService(context.TargetUser);

        var removed = await service.RemoveUserAvatarAsync(context.TargetUser.Id);

        Assert.True(removed);
        Assert.Null(context.TargetUser.ProfileImage);
        Assert.True(File.Exists(outsideProfilePath));
    }

    [Fact]
    public void CleanOrphanedProfileImages_DoesNotFollowSymlinkedUserDirectory()
    {
        using var context = new AvatarTestContext();
        var usersDirectory = Path.Combine(context.Root, "users");
        var outsideDirectory = Path.Combine(context.Root, "outside-user-data");
        Directory.CreateDirectory(usersDirectory);
        Directory.CreateDirectory(outsideDirectory);
        var outsideProfilePath = Path.Combine(outsideDirectory, "profile_orphan.png");
        File.WriteAllBytes(outsideProfilePath, [3, 2, 1]);
        var userDataPath = Path.Combine(usersDirectory, context.TargetUser.Id.ToString("N"));
        try
        {
            Directory.CreateSymbolicLink(userDataPath, outsideDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Assert.Skip($"Creating a symbolic link is unavailable in this environment: {exception.GetType().Name}.");
        }

        var service = context.CreateAvatarService(context.TargetUser);
        context.UserManager!.Setup(manager => manager.GetUsers()).Returns([context.TargetUser]);

        var deletedCount = service.CleanOrphanedProfileImages();

        Assert.Equal(0, deletedCount);
        Assert.True(File.Exists(outsideProfilePath));
    }

    [Fact]
    public async Task AssignRandomAvatarAsync_WhenConfigurationPersistenceFails_PreservesExistingMapping()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.ProfileImage = new ImageInfo(Path.Combine(context.Root, "missing-profile.png"));
        GetAvatarPlugin.Config.UserAvatars =
        [
            new UserAvatarMapping { UserId = context.TargetUser.Id.ToString(), AvatarId = "previous-avatar" }
        ];
        context.Serializer
            .Setup(xml => xml.SerializeToFile(It.IsAny<object>(), It.IsAny<string>()))
            .Throws(new IOException("simulated configuration write failure"));
        var service = context.CreateAvatarService(context.TargetUser);

        await Assert.ThrowsAsync<IOException>(() => service.AssignRandomAvatarAsync(context.TargetUser, forceAssign: true));

        Assert.Equal(Path.Combine(context.Root, "missing-profile.png"), context.TargetUser.ProfileImage!.Path);
        Assert.Single(GetAvatarPlugin.Config.UserAvatars!);
        Assert.Equal("previous-avatar", GetAvatarPlugin.Config.UserAvatars![0].AvatarId);
    }

    [Fact]
    public void AnonymousLocalRequest_CanReadPublicUserAvatar_AndDisablesCaching()
    {
        using var context = new AvatarTestContext();
        var (controller, networkManager, _) = context.CreateController(context.TargetUser);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        var image = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Once);
        image.FileStream.Dispose();
    }

    [Fact]
    public async Task UserAvatarRoute_ReturnsImageThroughHttpPipelineWithoutCaching()
    {
        using var context = new AvatarTestContext();
        var userManager = new Mock<IUserManager>();
        userManager.Setup(manager => manager.GetUserById(context.TargetUser.Id)).Returns(context.TargetUser);
        var avatarService = context.CreateAvatarService(context.TargetUser);
        var networkManager = new Mock<INetworkManager>();
        networkManager.Setup(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>())).Returns(true);
        var deviceManager = new Mock<IDeviceManager>();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AvatarController).Assembly);
        builder.Services.AddHttpClient();
        builder.Services.AddTransient<OnlinePackService>();
        builder.Services.AddSingleton(avatarService);
        builder.Services.AddSingleton(userManager.Object);
        builder.Services.AddSingleton(context.ConfigurationManager.Object);
        builder.Services.AddSingleton(networkManager.Object);
        builder.Services.AddSingleton(deviceManager.Object);
        builder.Services.AddAuthorization();

        await using var application = builder.Build();
        application.UseRouting();
        application.UseAuthorization();
        application.MapControllers();
        await application.StartAsync(TestContext.Current.CancellationToken);

        using var client = application.GetTestClient();
        using var response = await client.GetAsync(
            $"/GetAvatar/UserAvatar/{context.TargetUser.Id}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal([1, 2, 3], await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        userManager.Verify(manager => manager.GetUserById(context.TargetUser.Id), Times.Once);
    }

    [Theory]
    [InlineData(PermissionKind.IsHidden)]
    [InlineData(PermissionKind.IsDisabled)]
    public void AnonymousRequest_CannotReadHiddenOrDisabledUserAvatar(PermissionKind permission)
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(permission, true);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void AnonymousRemoteRequest_RequiresRemoteAccessPermission()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.EnableRemoteAccess, false);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser, localNetwork: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Once);
    }

    [Fact]
    public void AnonymousRequest_RequiresTargetDeviceAccess()
    {
        using var context = new AvatarTestContext();
        var principal = CreatePrincipal(deviceId: "blocked-device");
        var (controller, networkManager, deviceManager) = context.CreateController(
            context.TargetUser,
            principal,
            deviceAccess: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
        deviceManager.Verify(manager => manager.CanAccessDevice(context.TargetUser, "blocked-device"), Times.Once);
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void AuthenticatedOwner_CanReadOwnHiddenAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var principal = CreatePrincipal(userId: context.TargetUser.Id);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser, principal, caller: context.TargetUser);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void Administrator_CanReadHiddenUserAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var administrator = CreateUser();
        administrator.SetPermission(PermissionKind.IsAdministrator, true);
        var principal = CreatePrincipal(userId: administrator.Id);
        var (controller, networkManager, _) = context.CreateController(
            context.TargetUser,
            principal,
            caller: administrator);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void ApiKey_CanReadHiddenUserAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var principal = CreatePrincipal(apiKey: true);
        var (controller, networkManager, _) = context.CreateController(context.TargetUser, principal);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
    }

    [Fact]
    public void AuthenticatedOtherUser_CannotReadHiddenProfileAvatar()
    {
        using var context = new AvatarTestContext();
        context.TargetUser.SetPermission(PermissionKind.IsHidden, true);
        var caller = CreateUser();
        var principal = CreatePrincipal(userId: caller.Id);
        var (controller, _, _) = context.CreateController(context.TargetUser, principal, caller: caller);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void BeforeSetup_PublicTargetRemainsVisibleWithoutNetworkOrDeviceChecks()
    {
        using var context = new AvatarTestContext();
        var principal = CreatePrincipal(deviceId: "setup-device");
        var (controller, networkManager, deviceManager) = context.CreateController(
            context.TargetUser,
            principal,
            startupWizardCompleted: false,
            localNetwork: false,
            deviceAccess: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        using var image = Assert.IsType<FileStreamResult>(result).FileStream;
        networkManager.Verify(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>()), Times.Never);
        deviceManager.Verify(manager => manager.CanAccessDevice(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void MissingTargetUser_IsNotFound()
    {
        using var context = new AvatarTestContext();
        var (controller, _, _) = context.CreateController(context.TargetUser, includeTarget: false);

        var result = controller.GetUserAvatar(context.TargetUser.Id.ToString());

        Assert.IsType<NotFoundResult>(result);
    }

    private static ClaimsPrincipal CreatePrincipal(Guid? userId = null, bool apiKey = false, string? deviceId = null)
    {
        var claims = new List<Claim>();
        if (userId.HasValue)
        {
            claims.Add(new Claim("MulletaFlix-UserId", userId.Value.ToString()));
        }

        if (apiKey)
        {
            claims.Add(new Claim("MulletaFlix-IsApiKey", "true"));
        }

        if (deviceId is not null)
        {
            claims.Add(new Claim("MulletaFlix-DeviceId", deviceId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, userId.HasValue || apiKey ? "test" : null));
    }

    private static User CreateUser()
    {
        var user = new User("avatar-test-user", "test-auth-provider", "test-password-reset-provider")
        {
            Id = Guid.NewGuid()
        };
        user.SetPermission(PermissionKind.IsHidden, false);
        user.SetPermission(PermissionKind.IsDisabled, false);
        user.SetPermission(PermissionKind.EnableRemoteAccess, true);
        return user;
    }

    private sealed class AvatarTestContext : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "GetAvatarUserAuthorization", Guid.NewGuid().ToString("N"));
        private readonly List<AvatarService> _services = new();
        private readonly Mock<IServerApplicationPaths> _applicationPaths = new();
        private readonly Mock<IServerConfigurationManager> _configurationManager = new();
        private readonly Mock<IXmlSerializer> _serializer = new();
        private GetAvatarPlugin? _previousPlugin;

        public AvatarTestContext()
        {
            Directory.CreateDirectory(_root);
            _applicationPaths.SetupGet(paths => paths.PluginConfigurationsPath).Returns(_root);
            _applicationPaths.SetupGet(paths => paths.PluginsPath).Returns(_root);
            _applicationPaths.SetupGet(paths => paths.DataPath).Returns(_root);
            _applicationPaths.SetupGet(paths => paths.UserConfigurationDirectoryPath).Returns(Path.Combine(_root, "users"));
            _configurationManager.SetupGet(manager => manager.ApplicationPaths).Returns(_applicationPaths.Object);
            _configurationManager.SetupGet(manager => manager.Configuration).Returns(new ServerConfiguration { IsStartupWizardCompleted = true });

            var avatarFile = Path.Combine(_root, "GetAvatar", "avatars", "test-avatar.png");
            Directory.CreateDirectory(Path.GetDirectoryName(avatarFile)!);
            File.WriteAllBytes(avatarFile, [1, 2, 3]);

            var previousPlugin = GetAvatarPlugin.Instance;
            _serializer
                .Setup(xml => xml.DeserializeFromFile(typeof(PluginConfiguration), It.IsAny<string>()))
                .Returns(new PluginConfiguration());
            _ = new GetAvatarPlugin(_applicationPaths.Object, _serializer.Object, NullLogger<GetAvatarPlugin>.Instance);
            _previousPlugin = previousPlugin;
            GetAvatarPlugin.Config.AvailableAvatars = [new AvatarInfo { Id = "test-avatar", FileName = "test-avatar.png" }];

            TargetUser = CreateUser();
            GetAvatarPlugin.Config.UserAvatars = [new UserAvatarMapping { UserId = TargetUser.Id.ToString(), AvatarId = "test-avatar" }];
        }

        public User TargetUser { get; }

        public Mock<IXmlSerializer> Serializer => _serializer;

        public Mock<IServerConfigurationManager> ConfigurationManager => _configurationManager;

        public Mock<IUserManager>? UserManager { get; private set; }

        public Mock<IProviderManager>? ProviderManager { get; private set; }

        public string AvatarDirectory => Path.Combine(_root, "GetAvatar", "avatars");

        public string Root => _root;

        public string CreateExistingProfile(User user)
        {
            var profileDirectory = Path.Combine(_applicationPaths.Object.UserConfigurationDirectoryPath, user.Username);
            Directory.CreateDirectory(profileDirectory);
            var profilePath = Path.Combine(profileDirectory, "profile.png");
            File.WriteAllBytes(profilePath, [10, 20, 30, 40]);
            user.ProfileImage = new ImageInfo(profilePath);
            return profilePath;
        }

        public AvatarService CreateAvatarService(User? target = null)
        {
            var userManager = new Mock<IUserManager>();
            UserManager = userManager;
            userManager
                .Setup(manager => manager.AcquireProfileImageUpdateLockAsync(It.IsAny<Guid>()))
                .ReturnsAsync(Mock.Of<IDisposable>());
            if (target is not null)
            {
                userManager.Setup(manager => manager.GetUserById(target.Id)).Returns(target);
                userManager
                    .Setup(manager => manager.ClearProfileImageAsync(target))
                    .Returns(() =>
                    {
                        if (target.ProfileImage is not null && File.Exists(target.ProfileImage.Path))
                        {
                            File.Delete(target.ProfileImage.Path);
                        }

                        target.ProfileImage = null;
                        return Task.CompletedTask;
                    });
            }

            var providerManager = new Mock<IProviderManager>();
            ProviderManager = providerManager;
            providerManager
                .Setup(manager => manager.SaveImage(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns<Stream, string, string>((source, _, path) =>
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    using var destination = File.Create(path);
                    source.CopyTo(destination);
                    return Task.CompletedTask;
                });

            var service = new AvatarService(
                userManager.Object,
                providerManager.Object,
                _applicationPaths.Object,
                NullLogger<AvatarService>.Instance,
                _configurationManager.Object);
            _services.Add(service);
            return service;
        }

        public (AvatarController Controller, Mock<INetworkManager> NetworkManager, Mock<IDeviceManager> DeviceManager) CreateController(
            User target,
            ClaimsPrincipal? principal = null,
            User? caller = null,
            bool startupWizardCompleted = true,
            bool localNetwork = true,
            bool deviceAccess = true,
            bool includeTarget = true)
        {
            _configurationManager.SetupGet(manager => manager.Configuration)
                .Returns(new ServerConfiguration { IsStartupWizardCompleted = startupWizardCompleted });

            var userManager = new Mock<IUserManager>();
            if (includeTarget)
            {
                userManager.Setup(manager => manager.GetUserById(target.Id)).Returns(target);
            }

            if (caller is not null)
            {
                userManager.Setup(manager => manager.GetUserById(caller.Id)).Returns(caller);
            }

            var networkManager = new Mock<INetworkManager>();
            networkManager.Setup(manager => manager.IsInLocalNetwork(It.IsAny<IPAddress>())).Returns(localNetwork);
            var deviceManager = new Mock<IDeviceManager>();
            deviceManager.Setup(manager => manager.CanAccessDevice(It.IsAny<User>(), It.IsAny<string>())).Returns(deviceAccess);
            var avatarService = new AvatarService(
                userManager.Object,
                Mock.Of<IProviderManager>(),
                _applicationPaths.Object,
                NullLogger<AvatarService>.Instance,
                _configurationManager.Object);
            _services.Add(avatarService);

            var controller = new AvatarController(
                avatarService,
                null!,
                userManager.Object,
                _configurationManager.Object,
                networkManager.Object,
                deviceManager.Object,
                NullLogger<AvatarController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = principal ?? new ClaimsPrincipal(new ClaimsIdentity())
                    }
                }
            };
            controller.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.2");
            return (controller, networkManager, deviceManager);
        }

        public void Dispose()
        {
            foreach (var service in _services)
            {
                service.Dispose();
            }

            var property = typeof(GetAvatarPlugin).GetProperty(nameof(GetAvatarPlugin.Instance));
            property!.SetValue(null, _previousPlugin);
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
