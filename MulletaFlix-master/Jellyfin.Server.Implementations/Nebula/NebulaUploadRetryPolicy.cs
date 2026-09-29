using System;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>Calcula atrasos limitados para tentativas de upload do Nebula.</summary>
internal static class NebulaUploadRetryPolicy
{
    public const int MaximumAutomaticAttempts = 8;
    private const int InitialDelaySeconds = 60;
    private const int MaximumDelaySeconds = 3600;

    public static TimeSpan GetDelay(int attempt, double jitterFactor)
    {
        var exponent = Math.Clamp(attempt - 1, 0, 10);
        var baseSeconds = Math.Min(MaximumDelaySeconds, InitialDelaySeconds * (1 << exponent));
        var jitteredSeconds = Math.Clamp(baseSeconds * jitterFactor, 1, MaximumDelaySeconds);
        return TimeSpan.FromSeconds(jitteredSeconds);
    }

    public static bool IsTerminal(int attempt) => attempt >= MaximumAutomaticAttempts;
}
