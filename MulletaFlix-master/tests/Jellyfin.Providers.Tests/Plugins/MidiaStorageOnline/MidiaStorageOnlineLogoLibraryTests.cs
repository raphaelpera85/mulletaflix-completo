using System;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public class MidiaStorageOnlineLogoLibraryTests
{
    [Fact]
    public void BuildLogoFileName_DistinguishesUrlsWithSameBasename()
    {
        var firstUri = new Uri("https://cdn.example/first/logo.png");
        var secondUri = new Uri("https://images.example/second/logo.png");
        var caseVariantUri = new Uri("https://cdn.example/first/Logo.png");

        var firstName = MidiaStorageOnlineLogoLibrary.BuildLogoFileName("Canal Um", firstUri, "jpg");
        var secondName = MidiaStorageOnlineLogoLibrary.BuildLogoFileName("Canal Dois", secondUri, "jpg");
        var caseVariantName = MidiaStorageOnlineLogoLibrary.BuildLogoFileName("Canal Um", caseVariantUri, "jpg");

        Assert.NotEqual(firstName, secondName);
        Assert.NotEqual(firstName, caseVariantName);
        Assert.EndsWith(".png", firstName, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".png", secondName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(firstName, MidiaStorageOnlineLogoLibrary.BuildLogoFileName("Outro nome", firstUri, "jpg"));
        Assert.True(firstName.Length <= 120);
        Assert.True(secondName.Length <= 120);
    }

    [Fact]
    public void BuildLogoFileName_UsesFallbackForUnreasonablyLongExtension()
    {
        var uri = new Uri($"https://cdn.example/logo.{new string('x', 100)}");

        var fileName = MidiaStorageOnlineLogoLibrary.BuildLogoFileName("Canal", uri, "jpg");

        Assert.EndsWith(".jpg", fileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(fileName.Length <= 120);
    }
}
