// SPDX-FileCopyrightText: 2026 rlauuzo
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace IntroSkipper.FFmpeg;

/// <summary>
/// Spawns an ffmpeg or ffprobe process, drains its output and kills the whole process tree
/// on cancellation or timeout.
/// </summary>
/// <param name="logger">Logger.</param>
internal sealed partial class FFmpegProcessRunner(ILogger logger)
{
    internal const int MaxDiagnosticBytes = 8 * 1024;

    private const int MaxDiagnosticCharacters = 1024;
    private static readonly Regex _networkUriRegex = new("""(?i)\b[a-z][a-z0-9+.-]*://[^\s"'<>]+""", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex _pathLikeLineRegex = new(@"(?i)(?:\b[A-Z]:[\\/]|\\\\[^\\/\s]+[\\/]|(?<!\w)/[^/\s]+|(?:^|[\s""'=:(])\.{1,2}[\\/]|(?:^|[\s""'=:(])(?:[^\\/\s]+[\\/]){2,}[^\\/\s]+|(?:^|[\s""'=:(])[^\\/\s]+\.(?:mkv|mp4|avi|mov|m4v|m3u8|strm|mp3|aac|flac|wav|ts|m4a|ogg|webm)\b|[\\/][^\\/\s]+\.(?:mkv|mp4|avi|mov|m4v|m3u8|strm|mp3|aac|flac|wav|ts|m4a|ogg|webm)\b)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex _inputOpenFailureRegex = new(@"(?i)\b(?:(?:error|failed|could not|cannot)\s+(?:to\s+)?(?:opening|open)|unable\s+to\s+(?:opening|open))\s+(?:the\s+)?input\s+file\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex _httpStatusRegex = new(@"(?i)(?:HTTP\s+(?:error\s+)?\d{3}|Server returned\s+\d{3})", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex _authorizationHeaderRegex = new(@"(?i)\bAuthorization\s*:\s*[^\s,;]+(?:\s+[^\s,;]+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex _bearerTokenRegex = new(@"(?i)\bBearer\s+[^\s,;]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex _queryRegex = new("""\?[^\s"'<>]+""", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ILogger _logger = logger;

    /// <summary>
    /// Runs a process to completion and returns one of its output streams.
    /// </summary>
    /// <param name="processPath">Executable to start.</param>
    /// <param name="args">Arguments, one token each.</param>
    /// <param name="stderr"><see langword="true"/> to return standard error; otherwise standard output is returned.</param>
    /// <param name="timeout">Milliseconds to wait for the process to exit before killing it.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process.</param>
    /// <returns>The raw bytes of the selected stream.</returns>
    /// <exception cref="TimeoutException">The process did not exit within <paramref name="timeout"/> and was killed.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled; the process has been killed.</exception>
    public async Task<byte[]> RunAsync(
        string processPath,
        IReadOnlyList<string> args,
        bool stderr = false,
        int timeout = 60 * 1000,
        CancellationToken cancellationToken = default)
    {
        var result = await RunCoreAsync(processPath, args, stderr, timeout, cancellationToken, captureDiagnostics: false, logArguments: true).ConfigureAwait(false);
        return result.Output;
    }

    /// <summary>
    /// Runs a process while retaining a bounded stderr tail for safe failure diagnostics.
    /// Command arguments are deliberately omitted from logs because inputs may contain media URIs.
    /// </summary>
    /// <param name="processPath">Executable to start.</param>
    /// <param name="args">Arguments, one token each.</param>
    /// <param name="timeout">Milliseconds to wait for the process to exit before killing it.</param>
    /// <param name="cancellationToken">Cancels the wait and kills the process.</param>
    /// <returns>Selected process output, exit code and a bounded stderr tail.</returns>
    public Task<FFmpegProcessOutput> RunWithDiagnosticsAsync(
        string processPath,
        IReadOnlyList<string> args,
        int timeout = 60 * 1000,
        CancellationToken cancellationToken = default)
        => RunCoreAsync(processPath, args, stderr: false, timeout, cancellationToken, captureDiagnostics: true, logArguments: false);

    private async Task<FFmpegProcessOutput> RunCoreAsync(
        string processPath,
        IReadOnlyList<string> args,
        bool stderr,
        int timeout,
        CancellationToken cancellationToken,
        bool captureDiagnostics,
        bool logArguments)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var info = new ProcessStartInfo(processPath)
        {
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
            UseShellExecute = false,
            ErrorDialog = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        if (logArguments && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Starting ffmpeg with the following arguments: {Arguments}", string.Join(" ", info.ArgumentList.Select(RedactArgument)));
        }

        using var process = new Process { StartInfo = info };
        process.Start();

        try
        {
            try
            {
                process.PriorityClass = Plugin.Instance?.Configuration.ProcessPriority ?? ProcessPriorityClass.BelowNormal;
            }
            catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
            {
                LogFfmpegPriorityNotModified(_logger, e.Message);
            }

            using var ms = new MemoryStream();
            var diagnosticBuffer = captureDiagnostics ? new BoundedTailBuffer(MaxDiagnosticBytes) : null;
            // Draining must not use the caller token: on cancellation or timeout the process is
            // killed first, then its remaining output is drained so the pipes cannot deadlock.
            var stdoutTask = DrainAsync(process.StandardOutput.BaseStream, stderr ? null : ms, diagnosticBuffer: null);
            var stderrTask = DrainAsync(process.StandardError.BaseStream, stderr ? ms : null, diagnosticBuffer);

            using var timeoutCts = new CancellationTokenSource(timeout);
            using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            var timedOut = false;
            try
            {
                await process.WaitForExitAsync(waitCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                KillProcessTree(process);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                LogFfmpegExitTimeout(_logger, timeout);
                KillProcessTree(process);
                timedOut = true;
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (timedOut)
            {
                throw new TimeoutException($"ffmpeg process was killed after not exiting within {timeout}ms");
            }

            // Observed only: callers parse whatever was written and cache the result, and a
            // nonzero exit does not yet say whether that output was usable (a file without an
            // audio stream fails a fingerprint run this way). The line makes those cases
            // visible in the log before any caller starts treating an exit code as a failure.
            if (logArguments && process.ExitCode != 0 && _logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("ffmpeg exited with code {ExitCode}: {Arguments}", process.ExitCode, string.Join(" ", info.ArgumentList.Select(RedactArgument)));
            }

            return new FFmpegProcessOutput(ms.ToArray(), process.ExitCode, diagnosticBuffer is null ? string.Empty : Encoding.UTF8.GetString(diagnosticBuffer.ToArray()));
        }
        finally
        {
            // Only reachable with a live process when draining or the exit wait itself faulted.
            KillProcessTree(process);
        }
    }

    private static async Task DrainAsync(Stream stream, Stream? destination, BoundedTailBuffer? diagnosticBuffer)
    {
        var buffer = new byte[4096];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(buffer).ConfigureAwait(false)) > 0)
        {
            if (destination is not null)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
            }

            diagnosticBuffer?.Append(buffer, bytesRead);
        }
    }

    /// <summary>
    /// Removes remote URIs, query strings and path-bearing lines while retaining useful
    /// transport status text such as an HTTP 404 response.
    /// </summary>
    /// <param name="diagnostic">Raw process stderr.</param>
    /// <returns>A short diagnostic safe to include in the server log.</returns>
    internal static string SanitizeDiagnostic(string diagnostic)
    {
        if (string.IsNullOrWhiteSpace(diagnostic))
        {
            return string.Empty;
        }

        var safeLines = new List<string>();
        foreach (var rawLine in diagnostic.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = _networkUriRegex.Replace(rawLine, "[remote URI redacted]");
            line = _queryRegex.Replace(line, "[query redacted]");
            var status = _httpStatusRegex.Match(line);
            if (status.Success)
            {
                line = status.Value;
            }
            else if (_pathLikeLineRegex.IsMatch(line) || _inputOpenFailureRegex.IsMatch(line))
            {
                line = "[path-bearing FFmpeg detail redacted]";
            }
            else
            {
                line = _authorizationHeaderRegex.Replace(line, "Authorization: [redacted]");
                line = _bearerTokenRegex.Replace(line, "Bearer [redacted]");
            }

            line = new string(line.Where(character => !char.IsControl(character) || character == '\t').ToArray()).Trim();
            if (line.Length > 0)
            {
                safeLines.Add(line);
            }
        }

        var result = string.Join(" | ", safeLines);
        return result.Length <= MaxDiagnosticCharacters ? result : result[..MaxDiagnosticCharacters];
    }

    private sealed class BoundedTailBuffer(int capacity)
    {
        private readonly byte[] _buffer = new byte[capacity];
        private int _start;
        private int _count;

        public void Append(byte[] source, int length)
        {
            for (var i = 0; i < length; i++)
            {
                if (_count < _buffer.Length)
                {
                    _buffer[(_start + _count) % _buffer.Length] = source[i];
                    _count++;
                }
                else
                {
                    _buffer[_start] = source[i];
                    _start = (_start + 1) % _buffer.Length;
                }
            }
        }

        public byte[] ToArray()
        {
            var result = new byte[_count];
            var firstLength = Math.Min(_count, _buffer.Length - _start);
            Array.Copy(_buffer, _start, result, 0, firstLength);
            Array.Copy(_buffer, 0, result, firstLength, _count - firstLength);
            return result;
        }
    }

    internal static string RedactArgument(string argument)
    {
        if (!Uri.TryCreate(argument, UriKind.Absolute, out var uri)
            || string.IsNullOrEmpty(uri.Scheme)
            || uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
        {
            return argument;
        }

        return "[remote URI redacted]";
    }

    private void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException ex)
        {
            LogFfmpegProcessAlreadyGone(_logger, ex);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or AggregateException)
        {
            // Kill(entireProcessTree: true) reports partial failures as an AggregateException.
            LogFfmpegKillFailed(_logger, ex.Message);
        }
        catch (NotSupportedException ex)
        {
            LogFfmpegKillNotSupported(_logger, ex.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "ffmpeg priority could not be modified. {Message}")]
    private static partial void LogFfmpegPriorityNotModified(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "ffmpeg did not exit within {TimeoutMs}ms; killing process")]
    private static partial void LogFfmpegExitTimeout(ILogger logger, int timeoutMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to kill ffmpeg process tree: {Message}")]
    private static partial void LogFfmpegKillFailed(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Killing the ffmpeg process tree is not supported on this platform: {Message}")]
    private static partial void LogFfmpegKillNotSupported(ILogger logger, string message);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ffmpeg process already gone while killing process tree")]
    private static partial void LogFfmpegProcessAlreadyGone(ILogger logger, Exception ex);
}

/// <summary>Result of a process invocation that retains a bounded error tail.</summary>
/// <param name="Output">The selected standard output bytes.</param>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardError">The bounded standard error tail.</param>
internal sealed record FFmpegProcessOutput(byte[] Output, int ExitCode, string StandardError);
