using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Users;

public sealed class UserProfileImagePersistenceTests
{
    [Fact]
    public async Task ReplacingDeletedProfileImage_PersistsNewImageThroughDetachedUserUpdate()
    {
        var options = new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var userId = Guid.NewGuid();
        var user = new User("avatar-rollback", "test-authentication", "test-password-reset")
        {
            Id = userId,
            ProfileImage = new ImageInfo("avatars/original.png"),
        };

        int originalImageId;
        await using (var context = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance))
        {
            await context.Database.EnsureCreatedAsync();
            context.Users.Add(user);
            await context.SaveChangesAsync();
            originalImageId = user.ProfileImage!.Id;
        }

        await using (var context = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance))
        {
            context.Remove(user.ProfileImage!);
            await context.SaveChangesAsync();
            user.ProfileImage = null;
        }

        user.ProfileImage = new ImageInfo("avatars/restored.png");
        await using (var context = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance))
        {
            context.Update(user);
            await context.SaveChangesAsync();
        }

        await using (var context = new UsersDbContext(options, NullLogger<UsersDbContext>.Instance))
        {
            var restoredUser = await context.Users
                .Include(entity => entity.ProfileImage)
                .SingleAsync(entity => entity.Id == userId);

            Assert.NotNull(restoredUser.ProfileImage);
            Assert.True(restoredUser.ProfileImage.Id > 0);
            Assert.NotEqual(originalImageId, restoredUser.ProfileImage.Id);
            Assert.Equal("avatars/restored.png", restoredUser.ProfileImage.Path);
            Assert.Equal(userId, restoredUser.ProfileImage.UserId);
            Assert.False(await context.Set<ImageInfo>().AnyAsync(image => image.Id == originalImageId));
        }
    }
}
