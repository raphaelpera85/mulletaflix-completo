using System;
using Jellyfin.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaErrorMessageFormatterTests
{
    [Fact]
    public void Format_NormalizesWhitespaceInShortMessage()
    {
        var message = NebulaErrorMessageFormatter.Format(new InvalidOperationException("MongoDB\r\n connection   refused"));

        Assert.Equal("MongoDB connection refused", message);
    }

    [Fact]
    public void Format_UsesRootCauseWhenWrapperMessageIsTooLong()
    {
        var exception = new InvalidOperationException(new string('x', NebulaErrorMessageFormatter.MaximumMessageLength + 1), new InvalidOperationException("MongoDB connection refused"));

        var message = NebulaErrorMessageFormatter.Format(exception);

        Assert.Equal("MongoDB connection refused", message);
    }

    [Fact]
    public void Format_TruncatesOversizedRootCauseToMaximumLength()
    {
        var exception = new InvalidOperationException(
            new string('x', NebulaErrorMessageFormatter.MaximumMessageLength + 1),
            new InvalidOperationException(new string('y', NebulaErrorMessageFormatter.MaximumMessageLength + 20)));

        var message = NebulaErrorMessageFormatter.Format(exception);

        Assert.Equal(NebulaErrorMessageFormatter.MaximumMessageLength, message.Length);
        Assert.EndsWith("…", message, StringComparison.Ordinal);
    }
}
