using System.Collections.Generic;
using MediaBrowser.Providers.Plugins.MidiaStorageOnline;
using Xunit;

namespace MulletaFlix.Providers.Tests.Plugins.MidiaStorageOnline;

public sealed class MidiaStorageOnlineManifestCleanupTests
{
    [Fact]
    public void CreatePlan_WhenMediaSourceIsIncomplete_PreservesOldEntriesAndSkipsDeletion()
    {
        var previous = new Dictionary<string, string>
        {
            [@"Filmes\Filme offline\Filme offline.mkv"] = "https://media.example/offline",
            [@"Filmes\Filme removido\Filme removido.mkv"] = "https://media.example/removed"
        };
        var current = new Dictionary<string, string>
        {
            [@"Filmes\Filme novo\Filme novo.mkv"] = "https://media.example/new"
        };

        var plan = MidiaStorageOnlineManifestCleanup.CreatePlan(previous, current, force: false, mediaSourceIncomplete: true);

        Assert.Empty(plan.PathsToDelete);
        Assert.Equal("https://media.example/offline", plan.ManifestToSave[@"Filmes\Filme offline\Filme offline.mkv"]);
        Assert.Equal("https://media.example/removed", plan.ManifestToSave[@"Filmes\Filme removido\Filme removido.mkv"]);
        Assert.Equal("https://media.example/new", plan.ManifestToSave[@"Filmes\Filme novo\Filme novo.mkv"]);
    }

    [Fact]
    public void CreatePlan_WhenPlaylistHasNoEntriesAndPreviousManifestExists_PreservesPreviousEntries()
    {
        var previous = new Dictionary<string, string>
        {
            [@"Filmes\Filme existente\Filme existente.mkv"] = "https://media.example/existing"
        };
        var emptyCurrent = new Dictionary<string, string>();

        var plan = MidiaStorageOnlineManifestCleanup.CreatePlan(previous, emptyCurrent, force: false, mediaSourceIncomplete: true);

        Assert.Empty(plan.PathsToDelete);
        Assert.Equal(previous, plan.ManifestToSave);
    }

    [Fact]
    public void CreatePlan_WhenValidationIsComplete_DeletesEntriesMissingFromCurrentManifest()
    {
        var previous = new Dictionary<string, string>
        {
            [@"Filmes\Filme removido\Filme removido.mkv"] = "https://media.example/removed",
            [@"Filmes\Filme atual\Filme atual.mkv"] = "https://media.example/old"
        };
        var current = new Dictionary<string, string>
        {
            [@"Filmes\Filme atual\Filme atual.mkv"] = "https://media.example/current"
        };

        var plan = MidiaStorageOnlineManifestCleanup.CreatePlan(previous, current, force: false, mediaSourceIncomplete: false);

        Assert.Equal(new[] { @"Filmes\Filme removido\Filme removido.mkv" }, plan.PathsToDelete);
        Assert.Single(plan.ManifestToSave);
        Assert.Equal("https://media.example/current", plan.ManifestToSave[@"Filmes\Filme atual\Filme atual.mkv"]);
    }

    [Fact]
    public void CreatePlan_WhenForced_StillPreservesOldManifestIfValidationIsIncomplete()
    {
        var previous = new Dictionary<string, string>
        {
            [@"Filmes\Filme existente\Filme existente.mkv"] = "https://media.example/existing"
        };
        var current = new Dictionary<string, string>();

        var plan = MidiaStorageOnlineManifestCleanup.CreatePlan(previous, current, force: true, mediaSourceIncomplete: true);

        Assert.Empty(plan.PathsToDelete);
        Assert.Equal("https://media.example/existing", plan.ManifestToSave[@"Filmes\Filme existente\Filme existente.mkv"]);
    }
}
