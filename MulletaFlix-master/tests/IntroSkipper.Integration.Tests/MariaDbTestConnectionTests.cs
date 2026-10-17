// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

using Xunit;
using MulletaFlix.Testing;

namespace IntroSkipper.Integration.Tests;

public sealed class MariaDbTestConnectionTests
{
    [Theory]
    [InlineData("Server=db.example.test;Port=13306;User ID=test;Password=x;")]
    [InlineData("Server=127.0.0.1;Port=3306;User ID=test;Password=x;")]
    [InlineData("Server=127.0.0.1,db.example.test;Port=33099;User ID=test;Password=x;")]
    public void Validate_RejectsRemoteDefaultOrMultiHostConnections(string connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => MariaDbTestEnvironment.Validate(connectionString));
    }

    [Theory]
    [InlineData("Server=127.0.0.1;Port=33099;User ID=test;Password=x;")]
    [InlineData("Server=localhost;Port=33099;User ID=test;Password=x;")]
    public void Validate_AcceptsLoopbackOnNonDefaultPort(string connectionString)
    {
        var result = MariaDbTestEnvironment.Validate(connectionString);

        Assert.True(result.Port == 33099);
        Assert.Empty(result.Database);
        Assert.Equal(5, checked((int)result.ConnectionTimeout));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RejectsMissingConnection(string? connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => MariaDbTestEnvironment.Validate(connectionString));
    }
}
