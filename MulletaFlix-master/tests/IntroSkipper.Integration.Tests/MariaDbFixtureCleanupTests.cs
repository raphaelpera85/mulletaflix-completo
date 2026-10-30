// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using Xunit;

namespace IntroSkipper.Integration.Tests;

public sealed class MariaDbFixtureCleanupTests
{
    [Fact]
    public void EachFixtureGetsAnIsolatedTestSchema()
    {
        var first = new MariaDbFixture();
        var second = new MariaDbFixture();

        Assert.StartsWith("mfx_intro_test_", first.DatabaseName, StringComparison.Ordinal);
        Assert.StartsWith("mfx_intro_test_", second.DatabaseName, StringComparison.Ordinal);
        Assert.NotEqual(first.DatabaseName, second.DatabaseName);
    }

    [Fact]
    public void CleanupAttemptsEveryActionAndReturnsFailuresInOrder()
    {
        var firstFailure = new InvalidOperationException("host dispose failed");
        var secondFailure = new InvalidOperationException("schema drop failed");
        var restorationAttempted = false;

        var failures = MariaDbFixture.RunCleanupActions(
            () => throw firstFailure,
            () => throw secondFailure,
            () => restorationAttempted = true);

        Assert.True(restorationAttempted);
        Assert.Collection(
            failures,
            failure => Assert.Same(firstFailure, failure),
            failure => Assert.Same(secondFailure, failure));
    }

    [Fact]
    public void CleanupReturnsNoFailuresWhenAllActionsSucceed()
    {
        var actionCount = 0;

        var failures = MariaDbFixture.RunCleanupActions(
            () => actionCount++,
            () => actionCount++);

        Assert.Equal(2, actionCount);
        Assert.Empty(failures);
    }

    [Fact]
    public void InitializationFailureRemainsFirstWhenCleanupAlsoFails()
    {
        var initializationFailure = new InvalidOperationException("fixture initialization failed");
        var disposalFailure = new InvalidOperationException("service disposal failed");
        var dropFailure = new InvalidOperationException("schema drop failed");

        var combined = Assert.IsType<AggregateException>(
            MariaDbFixture.CombineInitializationFailure(initializationFailure, [disposalFailure, dropFailure]));

        Assert.Collection(
            combined.InnerExceptions,
            failure => Assert.Same(initializationFailure, failure),
            failure => Assert.Same(disposalFailure, failure),
            failure => Assert.Same(dropFailure, failure));
    }

    [Fact]
    public void InitializationFailureIsReturnedUnwrappedWhenCleanupSucceeds()
    {
        var initializationFailure = new InvalidOperationException("fixture initialization failed");

        var combined = MariaDbFixture.CombineInitializationFailure(initializationFailure, []);

        Assert.Same(initializationFailure, combined);
    }
}
