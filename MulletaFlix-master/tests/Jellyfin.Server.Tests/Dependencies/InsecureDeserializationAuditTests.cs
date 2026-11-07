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

    private static readonly Regex SafeIntroSkipperSnapshotDeserializeCall = new(
        @"JsonConvert\.DeserializeObject\s*<\s*BackupSnapshot\s*>\s*\(\s*json\s*,\s*SnapshotJsonSettings\s*\)",
        RegexOptions.Compiled);

    private static readonly Regex SafeIntroSkipperSnapshotSettings = new(
        @"SnapshotJsonSettings\s*=\s*new\s*\(\s*\)\s*\{\s*TypeNameHandling\s*=\s*TypeNameHandling\.None\b",
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
        var snapshotParticipantPath = Path.GetFullPath(Path.Combine(
            FindRepositoryRoot(),
            "MulletaFlix.Plugin.IntroSkipper",
            "Services",
            "IntroSkipperBackupParticipant.cs"));
        var deserializeObjectCalls = EnumerateRepositorySourceFiles()
            .SelectMany(path =>
            {
                var source = File.ReadAllText(path);
                return Regex.Matches(source, @"JsonConvert\.DeserializeObject")
                    .Select(match => (
                        Path: path,
                        Line: source[..match.Index].Count(character => character == '\n') + 1,
                        Source: source));
            })
            .ToList();

        var onlyAllowedCall = deserializeObjectCalls.Count == 1
            && string.Equals(
                Path.GetFullPath(deserializeObjectCalls[0].Path),
                snapshotParticipantPath,
                StringComparison.OrdinalIgnoreCase)
            && SafeIntroSkipperSnapshotDeserializeCall.IsMatch(deserializeObjectCalls[0].Source)
            && SafeIntroSkipperSnapshotSettings.IsMatch(deserializeObjectCalls[0].Source);

        Assert.True(
            onlyAllowedCall,
            "Only IntroSkipperBackupParticipant may deserialize its snapshot using the explicit "
            + "SnapshotJsonSettings configured with TypeNameHandling.None. Other source calls "
            + "remain forbidden:\n"
            + string.Join('\n', deserializeObjectCalls.Select(call => $"{call.Path}:{call.Line}")));
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
