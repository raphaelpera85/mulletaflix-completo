using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Helpers;

internal static class PortBindingRecovery
{
    private static readonly Regex NetstatLinePattern = new(
        @"^\s*TCP\s+(?<local>\S+)\s+\S+\s+(?<state>\S+)\s+(?<pid>\d+)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static async Task StartWithPortRecoveryAsync(
        Func<Task> startAsync,
        IEnumerable<int> ports,
        ILogger logger,
        string componentName,
        Func<Task>? resetAsync = null)
    {
        var portList = ports.Distinct().ToArray();
        var currentPid = Environment.ProcessId;

        Exception? bindException;
        try
        {
            await startAsync().ConfigureAwait(false);
            return;
        }
        catch (Exception ex) when (IsAddressInUseException(ex))
        {
            bindException = ex;
            logger.LogWarning(
                ex,
                "{Component} could not bind to one of the configured ports. Attempting to free them and retry once.",
                componentName);
        }

        var killedAny = await ReleasePortsAsync(portList, currentPid, logger).ConfigureAwait(false);
        if (!killedAny)
        {
            logger.LogWarning(
                "{Component} did not find a safe listener process to terminate on ports {Ports}. Startup will not terminate an unrelated process.",
                componentName,
                portList);

            throw new InvalidOperationException(
                $"{componentName} could not start because one of the configured ports is already in use. Stop the conflicting service or choose another port.",
                bindException);
        }

        var portsReleased = await WaitForPortsToFreeAsync(portList, currentPid, TimeSpan.FromSeconds(10), logger).ConfigureAwait(false);
        if (!portsReleased)
        {
            throw new InvalidOperationException(
                $"{componentName} could not reclaim the configured ports because another process is still listening.",
                bindException);
        }

        if (resetAsync is not null)
        {
            try
            {
                // Kestrel can remain in a partially-started state after a
                // bind failure. Reset the host before invoking StartAsync
                // again; otherwise the retry fails with "Server has already
                // started" even after the conflicting process was removed.
                await resetAsync().ConfigureAwait(false);
            }
            catch (Exception resetException)
            {
                logger.LogDebug(resetException, "Could not reset {Component} before port recovery retry.", componentName);
            }
        }

        await startAsync().ConfigureAwait(false);
    }

    public static async Task<bool> ReleasePortsAsync(IEnumerable<int> ports, int currentPid, ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            logger.LogWarning("Automatic port recovery is only implemented on Windows.");
            return false;
        }

        var portSet = ports.Distinct().ToHashSet();
        var pids = await GetListeningPidsAsync(portSet).ConfigureAwait(false);
        if (pids.Count == 0)
        {
            return false;
        }

        var killedAny = false;
        foreach (var pid in pids)
        {
            if (pid == currentPid)
            {
                logger.LogWarning(
                    "Port recovery found the current process PID {Pid} listening on one of the configured ports. It will wait for graceful release instead of killing itself.",
                    pid);
                continue;
            }

            try
            {
                var process = Process.GetProcessById(pid);
                var currentPath = Environment.ProcessPath;
                var targetPath = process.MainModule?.FileName;
                if (!CanTerminateProcess(currentPath, targetPath))
                {
                    logger.LogWarning(
                        "Refusing to terminate process {ProcessName} (PID {Pid}) on a configured port because its executable does not match the current server.",
                        process.ProcessName,
                        pid);
                    continue;
                }

                logger.LogWarning("Killing matching server process {ProcessName} (PID {Pid}) because it is listening on one of the configured ports.", process.ProcessName, pid);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                killedAny |= process.HasExited;
            }
            catch (ArgumentException)
            {
                // The process already exited between discovery and kill.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to terminate process with PID {Pid}.", pid);
            }
        }

        return killedAny;
    }

    private static async Task<bool> WaitForPortsToFreeAsync(IEnumerable<int> ports, int currentPid, TimeSpan timeout, ILogger logger)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var portSet = ports.Distinct().ToHashSet();
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            var pids = await GetListeningPidsAsync(portSet).ConfigureAwait(false);
            if (pids.Count == 0 || pids.All(pid => pid == currentPid))
            {
                return true;
            }

            logger.LogWarning(
                "Waiting for ports {Ports} to be released. Still listening PIDs: {Pids}",
                portSet,
                pids);
            await Task.Delay(500).ConfigureAwait(false);
        }

        return false;
    }

    internal static bool CanTerminateProcess(string? currentPath, string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(currentPath) || string.IsNullOrWhiteSpace(targetPath))
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(currentPath),
                Path.GetFullPath(targetPath),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task<HashSet<int>> GetListeningPidsAsync(HashSet<int> ports)
    {
        var result = new HashSet<int>();

        string output;
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "netstat.exe",
                    Arguments = "-ano -p tcp",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await process.WaitForExitAsync().ConfigureAwait(false);
        }
        catch
        {
            return result;
        }

        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var match = NetstatLinePattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var localEndpoint = match.Groups["local"].Value;
            var state = match.Groups["state"].Value;
            if (!string.Equals(state, "LISTENING", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var colonIndex = localEndpoint.LastIndexOf(':');
            if (colonIndex < 0)
            {
                continue;
            }

            if (!int.TryParse(localEndpoint[(colonIndex + 1)..], out var port) || !ports.Contains(port))
            {
                continue;
            }

            if (int.TryParse(match.Groups["pid"].Value, out var pid))
            {
                result.Add(pid);
            }
        }

        return result;
    }

    private static bool IsAddressInUseException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is AddressInUseException)
            {
                return true;
            }

            if (current is SocketException socketException
                && socketException.SocketErrorCode is SocketError.AddressAlreadyInUse)
            {
                return true;
            }

            var message = current.Message;
            if (message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
                || message.Contains("address is already in use", StringComparison.OrdinalIgnoreCase)
                || message.Contains("already in use", StringComparison.OrdinalIgnoreCase)
                || message.Contains("10048", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
