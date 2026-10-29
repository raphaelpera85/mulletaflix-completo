using System;
using System.Collections.Generic;
using Xunit;

namespace MulletaFlix.Server.Integration.Tests;

public sealed class JellyfinApplicationFactoryCleanupTests
{
    [Fact]
    public void ExecuteCleanupActions_AttemptsEveryActionAndPreservesFailuresInOrder()
    {
        var attemptedActions = new List<string>();
        var hostFailure = new InvalidOperationException("simulated host disposal failure");
        var databaseFailure = new InvalidOperationException("simulated database cleanup failure");

        var aggregate = Assert.Throws<AggregateException>(() =>
            MulletaFlixApplicationFactory.ExecuteCleanupActions(
                "simulated cleanup failure",
                new Action[]
                {
                    () =>
                    {
                        attemptedActions.Add("host");
                        throw hostFailure;
                    },
                    () =>
                    {
                        attemptedActions.Add("database");
                        throw databaseFailure;
                    }
                }));

        Assert.Equal(new[] { "host", "database" }, attemptedActions);
        Assert.Collection(
            aggregate.InnerExceptions,
            exception => Assert.Same(hostFailure, exception),
            exception => Assert.Same(databaseFailure, exception));
    }

    [Fact]
    public void ExecuteCleanupActions_RethrowsSingleFailureWithoutWrapping()
    {
        var failure = new InvalidOperationException("simulated cleanup failure");

        var thrown = Assert.Throws<InvalidOperationException>(() =>
            MulletaFlixApplicationFactory.ExecuteCleanupActions(
                "simulated cleanup failure",
                new Action[] { () => throw failure }));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public void ExecuteCleanupStatements_AttemptsRemainingStatementsAndReportsFailure()
    {
        var attemptedStatements = new List<string>();
        var firstFailure = new InvalidOperationException("simulated schema cleanup failure");
        var statements = new[] { "drop first", "drop second", "drop user" };

        var aggregate = Assert.Throws<AggregateException>(() =>
            MulletaFlixApplicationFactory.ExecuteCleanupStatements(
                "mfx_test_schema",
                statements,
                statement =>
                {
                    attemptedStatements.Add(statement);
                    if (statement == "drop second")
                    {
                        throw firstFailure;
                    }
                }));

        Assert.Equal(statements, attemptedStatements);
        Assert.Contains(firstFailure, aggregate.InnerExceptions);
    }

    [Fact]
    public void ExecuteCleanupStatements_CompletesWhenEveryStatementSucceeds()
    {
        var attemptedStatements = new List<string>();
        var statements = new[] { "drop first", "drop second", "drop user" };

        MulletaFlixApplicationFactory.ExecuteCleanupStatements(
            "mfx_test_schema",
            statements,
            attemptedStatements.Add);

        Assert.Equal(statements, attemptedStatements);
    }
}
