using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.DependencyInjection;
using MulletaFlix.Testing;
using Xunit;

namespace MulletaFlix.Server.Integration.Tests.Controllers;

public sealed class ImageControllerProfileUploadTests : IClassFixture<MulletaFlixApplicationFactory>
{
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADUlEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC";

    private readonly MulletaFlixApplicationFactory _factory;

    public ImageControllerProfileUploadTests(MulletaFlixApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostUserImage_ReplacesPersistedImageAndRemovesOldFile()
    {
        MariaDbTestEnvironment.SkipUnlessConfigured();

        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AddAuthHeader(await AuthHelper.CompleteStartupAsync(client));
        var admin = await AuthHelper.GetUserDtoAsync(client);
        var userManager = _factory.Services.GetRequiredService<IUserManager>();

        using (var firstUpload = CreatePngContent())
        using (var firstResponse = await client.PostAsync($"UserImage?userId={admin.Id:D}", firstUpload, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        }

        var firstProfileImage = userManager.GetUserById(admin.Id)?.ProfileImage;
        Assert.NotNull(firstProfileImage);
        var firstPath = firstProfileImage.Path;
        Assert.True(File.Exists(firstPath));

        using (var secondUpload = CreatePngContent())
        using (var secondResponse = await client.PostAsync($"UserImage?userId={admin.Id:D}", secondUpload, cancellationToken))
        {
            Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);
        }

        var secondProfileImage = userManager.GetUserById(admin.Id)?.ProfileImage;
        Assert.NotNull(secondProfileImage);
        Assert.NotEqual(firstPath, secondProfileImage.Path);
        Assert.True(File.Exists(secondProfileImage.Path));
        Assert.False(File.Exists(firstPath));

        using var getResponse = await client.GetAsync($"UserImage?userId={admin.Id:D}", cancellationToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var returnedImage = await getResponse.Content.ReadAsByteArrayAsync(cancellationToken);
        Assert.NotEmpty(returnedImage);
    }

    private static StringContent CreatePngContent()
    {
        var content = new StringContent(TinyPngBase64, Encoding.ASCII);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return content;
    }
}
