using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Jellyfin.Server.Tests.Dependencies;

/// <summary>
/// Regression guard for the Low-severity "Insecure Deserialization Risk" audit finding:
/// the codebase deserializes JSON through <c>System.Text.Json</c>, which has no concept of
/// polymorphic type-name handling and therefore cannot reproduce the classic Newtonsoft.Json
/// <c>TypeNameHandling.All</c>/<c>Auto</c> remote-code-execution gadget chain. This test scans
/// the actual source tree (not just currently-known files) so a future contributor cannot
/// silently reintroduce <c>JsonConvert.DeserializeObject</c> with unsafe
/// <c>TypeNameHandling</c>, or any other use of <c>TypeNameHandling.All</c>/<c>Auto</c>,
/// without a test failing.
/// </summary>
public class InsecureDeserializationAuditTests
{
    private static readonly string[] ExcludedDirectorySegments =
    [
        $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}",
        $"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}",
    ];

    private static readonly Regex UnsafeTypeNameHandling = new(
        @"TypeNameHandling\.(All|Auto)",
        RegexOptions.Compiled);

    [Fact]
    public void NoSourceFileUsesUnsafeNewtonsoftTypeNameHandling()
    {
        var offenders = EnumerateRepositorySourceFiles()
            .Select(path => (Path: path, Match: UnsafeTypeNameHandling.Match(File.ReadAllText(path))))
            .Where(result => result.Match.Success)
            .Select(result => $"{result.Path}: {result.Match.Value}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Found unsafe Newtonsoft.Json TypeNameHandling usage (polymorphic deserialization "
            + "RCE gadget risk). Use TypeNameHandling.None (the default) or explicit "
            + $"JsonSerializerSettings instead:\n{string.Join('\n', offenders)}");
    }

    [Fact]
    public void NoSourceFileCallsJsonConvertDeserializeObjectWithoutExplicitSafeSettings()
    {
        // JsonConvert.DeserializeObject(string) / DeserializeObject<T>(string) without a second
        // `settings` argument inherits JsonConvert.DefaultSettings, which defaults to
        // TypeNameHandling.None and is therefore safe on its own. This test exists primarily so
        // that *any* reintroduction of JsonConvert.DeserializeObject is visible here, pointing
        // whoever added it at this file's TypeNameHandling check above.
        var deserializeObjectCalls = EnumerateRepositorySourceFiles()
            .SelectMany(path => File.ReadAllLines(path)
                .Select((line, index) => (Path: path, Line: index + 1, Text: line))
                .Where(entry => entry.Text.Contains("JsonConvert.DeserializeObject", StringComparison.Ordinal)))
            .Select(entry => $"{entry.Path}:{entry.Line}: {entry.Text.Trim()}")
            .ToList();

        // No known occurrences at the time this test was written (the codebase deserializes
        // exclusively through System.Text.Json). If this starts failing, audit the new call
        // site(s) for explicit, safe JsonSerializerSettings before updating this test.
        Assert.True(
            deserializeObjectCalls.Count == 0,
            "JsonConvert.DeserializeObject is now used where it previously was not; audit for "
            + $"TypeNameHandling before accepting this change:\n{string.Join('\n', deserializeObjectCalls)}");
    }

    private static string[] EnumerateRepositorySourceFiles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var selfPath = Path.GetFullPath(
            Path.Combine(repositoryRoot, "tests", "Jellyfin.Server.Tests", "Dependencies", "InsecureDeserializationAuditTests.cs"));
        return Directory.EnumerateFiles(repositoryRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !ExcludedDirectorySegments.Any(segment => path.Contains(segment, StringComparison.OrdinalIgnoreCase)))
            .Where(path => !string.Equals(Path.GetFullPath(path), selfPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(typeof(InsecureDeserializationAuditTests).Assembly.Location)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MulletaFlix.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate MulletaFlix.sln above the test assembly to determine the repository root.");
    }
}
