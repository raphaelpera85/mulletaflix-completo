// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

namespace MulletaFlix.Tools.IntroSkipperMigration;

/// <summary>
/// Entry point of the migration tool. All behaviour lives in <see cref="Migration"/>, which
/// the integration tests call directly, so the paths a person runs and the paths under test
/// cannot drift.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine(Options.Usage);
            return 2;
        }

        if (options.ShowHelp)
        {
            Console.WriteLine(Options.Usage);
            return 0;
        }

        return await Migration.RunAsync(options, Console.Out).ConfigureAwait(false);
    }
}
