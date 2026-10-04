using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using MulletaFlix.Data;
using MulletaFlix.Database.Implementations.Enums;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MulletaFlix.Server.Integration.Tests.Controllers;

public sealed class AudioControllerTests : IClassFixture<MulletaFlixApplicationFactory>
{
    private readonly MulletaFlixApplicationFactory _factory;
    private static string? _accessToken;

    public AudioControllerTests(MulletaFlixApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Audio/{0}/stream")]
    [InlineData("Audio/{0}/stream.mp3")]
    public async Task GetAudioStream_NoToken_Unauthorized(string route)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, route, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Audio/{0}/stream")]
    [InlineData("Audio/{0}/stream.mp3")]
    public async Task HeadAudioStream_NoToken_Unauthorized(string route)
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Head, string.Format(CultureInfo.InvariantCulture, route, Guid.NewGuid()));

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Audio/{0}/stream")]
    [InlineData("Audio/{0}/stream.mp3")]
    public async Task GetAudioStream_InvalidToken_Unauthorized(string route)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader("invalid-token");

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, route, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("Audio/{0}/stream")]
    [InlineData("Audio/{0}/stream.mp3")]
    public async Task GetAudioStream_ValidToken_NonexistentItem_NotFound(string route)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(_accessToken ??= await AuthHelper.CompleteStartupAsync(client));

        var response = await client.GetAsync(string.Format(CultureInfo.InvariantCulture, route, Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Audio/{0}/stream")]
    [InlineData("Audio/{0}/stream.mp3")]
    public async Task GetAudioStream_ValidQueryToken_NonexistentItem_NotFound(string route)
    {
        var client = _factory.CreateClient();
        var token = _accessToken ??= await AuthHelper.CompleteStartupAsync(client);
        var path = string.Format(CultureInfo.InvariantCulture, route, Guid.NewGuid());

        var response = await client.GetAsync($"{path}?api_key={Uri.EscapeDataString(token)}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("Audio/{0}/stream?static=true")]
    [InlineData("Audio/{0}/stream.m4a?static=true")]
    public async Task GetAudioStream_HiddenByUserTag_NotFound(string route)
    {
        var client = _factory.CreateClient();
        var token = _accessToken ??= await AuthHelper.CompleteStartupAsync(client);
        client.DefaultRequestHeaders.AddAuthHeader(token);

        var userDto = await AuthHelper.GetUserDtoAsync(client);
        var userManager = _factory.Services.GetRequiredService<IUserManager>();
        var user = userManager.GetUserById(userDto.Id);
        Assert.NotNull(user);
        var originalBlockedTags = user.GetPreference(PreferenceKind.BlockedTags);

        var path = Path.Combine(Path.GetTempPath(), $"mflx-hidden-audio-{Guid.NewGuid():N}.m4a");
        var item = new Audio
        {
            Id = Guid.NewGuid(),
            Name = "Hidden audio authorization test",
            Path = path,
            Tags = ["private-stream-test"]
        };

        try
        {
            await File.WriteAllBytesAsync(path, [0x4D, 0x46, 0x4C, 0x58], TestContext.Current.CancellationToken);
            _factory.Services.GetRequiredService<ILibraryManager>().CreateItem(item, null);

            var streamRoute = string.Format(CultureInfo.InvariantCulture, route, item.Id);
            using var visibleResponse = await client.GetAsync(streamRoute, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, visibleResponse.StatusCode);

            user.SetPreference(PreferenceKind.BlockedTags, ["private-stream-test"]);
            await userManager.UpdateUserAsync(user);

            using var response = await client.GetAsync(streamRoute, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        finally
        {
            user.SetPreference(PreferenceKind.BlockedTags, originalBlockedTags);
            await userManager.UpdateUserAsync(user);
            _factory.Services.GetRequiredService<ILibraryManager>().DeleteItem(item, new DeleteOptions { DeleteFileLocation = false });
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
