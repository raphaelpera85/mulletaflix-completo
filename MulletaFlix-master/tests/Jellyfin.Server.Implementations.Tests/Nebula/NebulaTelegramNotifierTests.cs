using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaTelegramNotifierTests
{
    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenNoBotTokensConfigured()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new List<string>(),
            -100123456789,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync("<b>Teste</b>", null, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenMessageIsEmpty()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "bot123:token" },
            -100123456789,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync(string.Empty, null, CancellationToken.None);
        Assert.False(result);
    }

    [Fact]
    public async Task SendMessageAsync_ReturnsFalse_WhenChatIdIsZeroOrEmpty()
    {
        await using var pool = new NebulaTelegramPool(
            12345,
            "test_hash",
            new[] { "bot123:token" },
            0,
            string.Empty,
            NullLogger<NebulaTelegramPool>.Instance);

        var result = await pool.SendMessageAsync("<b>Teste</b>", string.Empty, CancellationToken.None);
        Assert.False(result);
    }
}
