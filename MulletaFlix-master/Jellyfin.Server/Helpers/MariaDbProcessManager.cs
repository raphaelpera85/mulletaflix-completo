using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller;
using MySqlConnector;
using Microsoft.Extensions.Logging;
using MulletaFlix.Database.Implementations;

namespace MulletaFlix.Server.Helpers
{
    public static class MariaDbProcessManager
    {
        private static Process? _mariaDbProcess;
        private static Mutex? _startupMutex;

        public static async Task StartMariaDbAsync(IServerApplicationPaths appPaths, ILogger logger, CancellationToken cancellationToken = default)
        {
            try
            {
                var dataDir = Path.Combine(appPaths.DataPath, "mariadb_data");

                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(appDir))
                {
                    return;
                }

                var binDir = Path.Combine(appDir, "mariadb", "bin");
                var exePath = Path.Combine(binDir, "mysqld.exe");
                var installDbPath = Path.Combine(binDir, "mysql_install_db.exe");
                var masterConnString = "Server=127.0.0.1;Port=3306;User ID=root;Password=;CharSet=utf8mb4;SslMode=None;Connection Timeout=2;";

                if (!File.Exists(exePath))
                {
                    logger.LogWarning("MariaDB portable executable not found at {ExePath}. Assuming external database is configured.", exePath);
                    return;
                }

                _startupMutex = AcquireStartupMutex();
                var databaseAvailable = await WaitForDatabaseAsync(masterConnString, TimeSpan.FromSeconds(2), logger, cancellationToken).ConfigureAwait(false);
                if (!databaseAvailable)
                {
                    if (await IsPortOpenAsync(3306, cancellationToken).ConfigureAwait(false))
                    {
                        logger.LogWarning("MariaDB port 3306 is occupied but SQL connections are not ready. Waiting for the existing process instead of starting a second instance.");
                    }
                    else
                    {
                        if (!Directory.Exists(dataDir) || Directory.GetFiles(dataDir).Length == 0)
                        {
                            logger.LogInformation("Initializing new MariaDB data directory at {DataDir}", dataDir);
                            Directory.CreateDirectory(dataDir);

                            if (File.Exists(installDbPath))
                            {
                                var initStartInfo = new ProcessStartInfo
                                {
                                    FileName = installDbPath,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                initStartInfo.ArgumentList.Add($"--datadir={dataDir}");
                                using var initProcess = Process.Start(initStartInfo);
                                if (initProcess != null)
                                {
                                    await initProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                                    if (initProcess.ExitCode != 0)
                                    {
                                        throw new InvalidOperationException($"MariaDB data-directory initialization failed with exit code {initProcess.ExitCode}.");
                                    }
                                }
                            }
                        }

                        EnsureDataDirectoryWritable(dataDir);

                        logger.LogInformation("Starting embedded MariaDB from {ExePath}...", exePath);

                        var startInfo = new ProcessStartInfo
                        {
                            FileName = exePath,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        startInfo.ArgumentList.Add($"--datadir={dataDir}");
                        startInfo.ArgumentList.Add("--console");
                        startInfo.ArgumentList.Add("--skip-log-bin");
                        startInfo.ArgumentList.Add("--bind-address=127.0.0.1");

                        // Concurrency and throughput tuning for the embedded engine. These are
                        // the values the server has to work under, so they travel with it
                        // instead of depending on a my.ini the operator may never create.
                        // Only applied to the instance this process starts: an external
                        // MariaDB belongs to whoever runs it.
                        foreach (var argument in BuildTuningArguments())
                        {
                            startInfo.ArgumentList.Add(argument);
                        }

                        _mariaDbProcess = new Process { StartInfo = startInfo };

                        _mariaDbProcess.OutputDataReceived += (_, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                logger.LogDebug("MariaDB: {Data}", e.Data);
                            }
                        };
                        _mariaDbProcess.ErrorDataReceived += (_, e) =>
                        {
                            if (!string.IsNullOrEmpty(e.Data))
                            {
                                logger.LogWarning("MariaDB: {Data}", e.Data);
                            }
                        };

                        _mariaDbProcess.Start();
                        _mariaDbProcess.BeginOutputReadLine();
                        _mariaDbProcess.BeginErrorReadLine();
                    }
                }

                if (!await WaitForDatabaseAsync(masterConnString, TimeSpan.FromSeconds(30), logger, cancellationToken).ConfigureAwait(false))
                {
                    if (_mariaDbProcess?.HasExited == true)
                    {
                        throw new InvalidOperationException($"Embedded MariaDB exited before accepting connections (exit code {_mariaDbProcess.ExitCode}). Check the MariaDB log and data-directory permissions.");
                    }

                    throw new TimeoutException("Embedded MariaDB did not accept SQL connections within 30 seconds. Check whether another process owns the port and whether the data directory is writable.");
                }

                if (_mariaDbProcess is null)
                {
                    logger.LogInformation("Existing MariaDB is accepting connections on port 3306");
                }
                else
                {
                    logger.LogInformation("MariaDB embedded process started with PID {PID} and is accepting connections on port 3306", _mariaDbProcess.Id);
                }
                logger.LogWarning("Embedded MariaDB is running without a root password. This is acceptable for localhost-only access, but set a password if the port is exposed.");
                await InitializeDatabaseAsync(logger, masterConnString, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start embedded MariaDB.");
                try
                {
                    await StopMariaDbAsync(logger, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    logger.LogError(cleanupException, "Failed to clean up embedded MariaDB after startup failure.");
                }

                throw;
            }
        }

        private static async Task<bool> IsMariaDbAlreadyAvailableAsync(string masterConnString, ILogger? logger, CancellationToken cancellationToken)
        {
            try
            {
                using var connection = new MySqlConnection(masterConnString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return connection.State == System.Data.ConnectionState.Open;
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "MariaDB SQL probe not ready yet while checking readiness.");
                return false;
            }
        }

        private static Mutex AcquireStartupMutex()
        {
            try
            {
                var mutex = new Mutex(false, "Global\\MulletaFlix.EmbeddedMariaDb");
                try
                {
                    if (!mutex.WaitOne(TimeSpan.FromSeconds(45)))
                    {
                        mutex.Dispose();
                        throw new TimeoutException("Timed out waiting for another MulletaFlix process to finish MariaDB startup.");
                    }
                }
                catch (AbandonedMutexException)
                {
                    // The previous owner exited without releasing the mutex.
                    // The OS transfers ownership to this process, so startup
                    // can safely continue.
                }

                return mutex;
            }
            catch (UnauthorizedAccessException)
            {
                var fallbackMutex = new Mutex(false, "MulletaFlix.EmbeddedMariaDb");
                try
                {
                    fallbackMutex.WaitOne();
                }
                catch (AbandonedMutexException)
                {
                    // The abandoned mutex is acquired by this process.
                }

                return fallbackMutex;
            }
        }

        private static void EnsureDataDirectoryWritable(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            var probePath = Path.Combine(dataDir, $".mulletaflix-write-test-{Environment.ProcessId}");
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
        }

        private static async Task<bool> WaitForDatabaseAsync(string connectionString, TimeSpan timeout, ILogger logger, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (await IsMariaDbAlreadyAvailableAsync(connectionString, logger, cancellationToken).ConfigureAwait(false))
                {
                    logger.LogInformation("MariaDB is accepting SQL connections after {Elapsed}ms", sw.ElapsedMilliseconds);
                    return true;
                }

                if (_mariaDbProcess?.HasExited == true)
                {
                    return false;
                }

                await Task.Delay(250, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Server arguments that make the embedded engine cope with the concurrency the
        /// application actually produces.
        /// <para>
        /// Measured on a live library: 78k items and 500 MB of data while MariaDB ran with
        /// stock defaults — a 128 MB buffer pool (too small for the hot tables, so scans
        /// spilled to disk), <c>innodb_io_capacity</c> 200 (a spinning-disk default on an
        /// NVMe drive) and <c>max_connections</c> 151, below the 200 the server's own
        /// connection pool is allowed to open.
        /// </para>
        /// <para>
        /// The memory settings stay deliberately small. The host is memory-constrained (a
        /// 15 GB machine committing ~39 GB, so it pages), and MariaDB reserves the buffer
        /// pool and the redo log at startup: growing them by hundreds of megabytes would
        /// trade database disk reads for system-wide paging, which is the worse of the two
        /// stalls. The zero-memory settings below are where the win is.
        /// </para>
        /// </summary>
        /// <returns>The arguments to append to the mysqld command line.</returns>
        private static IEnumerable<string> BuildTuningArguments()
        {
            // Double the stock pool: the largest hot table is ~300 MB, so this still cannot
            // hold everything, but it keeps the frequently read pages resident at a
            // guaranteed +128 MB instead of the +384 MB a full working set would need.
            yield return "--innodb-buffer-pool-size=256M";

            // Redo headroom for bulk metadata writes (library scans). +32 MB over the 96 MB
            // default reduces checkpoint frequency during a scan without a large reserve.
            yield return "--innodb-log-file-size=128M";

            // The data directory lives on an SSD. 200 is the spinning-disk default; leaving
            // it there throttles flush and purge work far below the device's ability. This
            // costs no memory.
            yield return "--innodb-io-capacity=2000";

            // Above the 200 the application's connection pool may open, so a burst can never
            // be rejected with "Too many connections". Peak observed: 71 concurrent threads.
            // Idle connections cost little; the per-connection buffers are what matter.
            yield return "--max-connections=300";

            // Descriptors for the index and table set of a library this size.
            yield return "--table-open-cache=4096";

            // 50s is the default and the worst case for a stalled request: a thread waits
            // most of a minute before the application's retry can run. 25s keeps the retry
            // useful while still absorbing ordinary scan contention.
            yield return "--innodb-lock-wait-timeout=25";
        }

        private static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
        }

        private static async Task InitializeDatabaseAsync(ILogger logger, string masterConnString, CancellationToken cancellationToken)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    logger.LogInformation("Ensuring the MulletaFlix database exists in MariaDB...");
                    using var connection = new MySqlConnection(masterConnString);
                    await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

                    using var command = connection.CreateCommand();
                    var dbs = new[] { DatabaseNames.Main, "mulletaflix_users", "mulletaflix_movies", "mulletaflix_series", "mulletaflix_channels", "mulletaflix_books" };
                    foreach (var db in dbs)
                    {
                        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{db}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
                        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        logger.LogInformation("Database '{Database}' verified/created.", db);
                    }
                    return;
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Connection attempt {Attempt} failed: {Message}. Retrying...", i + 1, ex.Message);
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Could not initialize the MulletaFlix databases in MariaDB after 5 attempts.");
        }

        public static void StopMariaDb(ILogger logger)
        {
            StopMariaDbAsync(logger).GetAwaiter().GetResult();
        }

        public static async Task StopMariaDbAsync(ILogger logger, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_mariaDbProcess != null && !_mariaDbProcess.HasExited)
                {
                    try
                    {
                        logger.LogInformation("Stopping embedded MariaDB gracefully...");

                        var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                        var mysqlAdminPath = string.IsNullOrEmpty(appDir)
                            ? null
                            : Path.Combine(appDir, "mariadb", "bin", "mysqladmin.exe");

                        if (!string.IsNullOrEmpty(mysqlAdminPath) && File.Exists(mysqlAdminPath))
                        {
                            using var shutdown = Process.Start(new ProcessStartInfo
                            {
                                FileName = mysqlAdminPath,
                                Arguments = "--protocol=tcp --host=127.0.0.1 --port=3306 --user=root shutdown",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            });

                            if (shutdown is not null)
                            {
                                await shutdown.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
                            }
                        }

                        if (!_mariaDbProcess.HasExited)
                        {
                            logger.LogWarning("MariaDB did not stop gracefully; terminating the process to avoid leaving the service running.");
                            _mariaDbProcess.Kill(entireProcessTree: true);
                            await _mariaDbProcess.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                        }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while stopping embedded MariaDB. Attempting a final process termination.");
                    try
                    {
                        if (!_mariaDbProcess.HasExited)
                        {
                            _mariaDbProcess.Kill(entireProcessTree: true);
                            await _mariaDbProcess.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                        }
                    }
                    catch (Exception killException)
                    {
                        logger.LogError(killException, "Final embedded MariaDB termination failed.");
                    }
                }
                finally
                {
                    _mariaDbProcess.Dispose();
                    _mariaDbProcess = null;
                }
            }
            }
            finally
            {
                ReleaseStartupMutex();
            }
        }

        private static void ReleaseStartupMutex()
        {
            if (_startupMutex is not null)
            {
                try
                {
                }
                finally
                {
                    try
                    {
                        _startupMutex.ReleaseMutex();
                    }
                    finally
                    {
                        _startupMutex.Dispose();
                        _startupMutex = null;
                    }
                }
            }
        }
    }
}
