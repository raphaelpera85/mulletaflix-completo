using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;

namespace MulletaFlix.Server.Helpers
{
    /// <summary>
    /// Gerencia a inicialização e ciclo de vida do proxy reverso HTTPS (Nginx)
    /// e componentes de conexão segura quando o MulletaFlix Server é iniciado.
    /// </summary>
    public static class SecureConnectionProcessManager
    {
        private const string NginxServiceName = "MulletaFlixNginx";
        private const int HttpsPort = 443;
        private const int HttpPort = 80;

        private static Process? _nginxProcess;
        private static string? _activeNginxDir;

        /// <summary>
        /// Localiza o caminho para o executável do Nginx em locais conhecidos.
        /// </summary>
        /// <param name="appDir">Diretório base da aplicação.</param>
        /// <returns>Caminho completo do nginx.exe ou nulo se não encontrado.</returns>
        public static string? FindNginxExecutable(string? appDir = null)
        {
            var envPath = Environment.GetEnvironmentVariable("MULLETAFLIX_NGINX_PATH");
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                return envPath;
            }

            if (!string.IsNullOrWhiteSpace(appDir))
            {
                var candidateInApp = Path.Combine(appDir, "nginx", "nginx.exe");
                if (File.Exists(candidateInApp))
                {
                    return candidateInApp;
                }
            }

            try
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrWhiteSpace(userProfile))
                {
                    var userNginx = Path.Combine(userProfile, "nginx", "nginx.exe");
                    if (File.Exists(userNginx))
                    {
                        return userNginx;
                    }
                }

                var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrWhiteSpace(programFiles))
                {
                    var pfNginx = Path.Combine(programFiles, "MulletaFlix", "nginx", "nginx.exe");
                    if (File.Exists(pfNginx))
                    {
                        return pfNginx;
                    }
                }
            }
            catch
            {
                // Ignora exceções de acesso a pastas especiais
            }

            return null;
        }

        /// <summary>
        /// Verifica se uma porta TCP local está aberta e aceitando conexões.
        /// </summary>
        /// <param name="port">Porta TCP a ser verificada.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>True se a porta estiver aberta; caso contrário, False.</returns>
        public static async Task<bool> IsPortOpenAsync(int port, CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = new TcpClient();
                using var registration = cancellationToken.Register(() => client.Close());
                await client.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
                return client.Connected;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Assegura que o proxy reverso Nginx e serviços necessários para conexão segura
        /// estejam iniciados junto com o servidor MulletaFlix.
        /// </summary>
        /// <param name="appPaths">Caminhos da aplicação do servidor.</param>
        /// <param name="logger">Instância de logger.</param>
        /// <param name="cancellationToken">Token de cancelamento.</param>
        /// <returns>Tarefa assíncrona.</returns>
        public static async Task EnsureSecureConnectionStartedAsync(
            IServerApplicationPaths? appPaths,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            try
            {
                logger.LogInformation("Verificando status dos componentes de conexão segura HTTPS...");

                // 1. Verificar se a porta HTTPS (443) já está respondendo
                var httpsAlreadyOpen = await IsPortOpenAsync(HttpsPort, cancellationToken).ConfigureAwait(false);
                if (httpsAlreadyOpen)
                {
                    logger.LogInformation("Conexão segura HTTPS (porta {Port}) já está ativa e respondendo.", HttpsPort);
                    EnsureDuckDnsRunning(logger);
                    return;
                }

                // 2. Tentar via Serviço Windows (MulletaFlixNginx) usando sc.exe nativo
                var serviceStarted = TryStartNginxWindowsService(logger);
                if (serviceStarted)
                {
                    logger.LogInformation("Serviço Windows '{ServiceName}' verificado e iniciado.", NginxServiceName);
                    EnsureDuckDnsRunning(logger);
                    return;
                }

                // 3. Fallback: Iniciar o processo nginx.exe diretamente caso o serviço não exista
                var appDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var nginxExe = FindNginxExecutable(appDir);
                if (nginxExe != null && File.Exists(nginxExe))
                {
                    var nginxDir = Path.GetDirectoryName(nginxExe);
                    var confPath = Path.Combine(nginxDir!, "conf", "nginx.conf");
                    if (File.Exists(confPath))
                    {
                        logger.LogInformation("Iniciando processo Nginx a partir de {ExePath} com configuração {ConfPath}...", nginxExe, confPath);
                        var startInfo = new ProcessStartInfo
                        {
                            FileName = nginxExe,
                            Arguments = $"-p \"{nginxDir}\" -c conf/nginx.conf",
                            WorkingDirectory = nginxDir,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        _nginxProcess = Process.Start(startInfo);
                        _activeNginxDir = nginxDir;

                        // Aguarda até 3 segundos para confirmar que a porta 443 subiu
                        var sw = Stopwatch.StartNew();
                        while (sw.Elapsed < TimeSpan.FromSeconds(3) && !cancellationToken.IsCancellationRequested)
                        {
                            if (await IsPortOpenAsync(HttpsPort, cancellationToken).ConfigureAwait(false))
                            {
                                logger.LogInformation("Nginx iniciado com sucesso e porta {Port} ativa.", HttpsPort);
                                break;
                            }

                            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        logger.LogWarning("Arquivo de configuração do Nginx não encontrado em {ConfPath}.", confPath);
                    }
                }
                else
                {
                    logger.LogInformation("Executável do Nginx não encontrado localmente. Conexão segura operando em modo externo.");
                }

                // 4. Garantir que o DuckDNS updater esteja rodando
                EnsureDuckDnsRunning(logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Não foi possível garantir a inicialização automática de todos os componentes HTTPS/Nginx: {Message}", ex.Message);
            }
        }

        /// <summary>
        /// Para o processo Nginx se ele tiver sido iniciado diretamente por esta instância do servidor.
        /// </summary>
        /// <param name="logger">Instância de logger.</param>
        /// <returns>Tarefa assíncrona.</returns>
        public static Task StopSecureConnectionAsync(ILogger logger)
        {
            if (_nginxProcess != null && !_nginxProcess.HasExited)
            {
                try
                {
                    logger.LogInformation("Encerrando processo Nginx gerenciado...");
                    if (!string.IsNullOrEmpty(_activeNginxDir))
                    {
                        var nginxExe = Path.Combine(_activeNginxDir, "nginx.exe");
                        if (File.Exists(nginxExe))
                        {
                            var stopInfo = new ProcessStartInfo
                            {
                                FileName = nginxExe,
                                Arguments = $"-p \"{_activeNginxDir}\" -s stop",
                                WorkingDirectory = _activeNginxDir,
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var stopProc = Process.Start(stopInfo);
                            stopProc?.WaitForExit(3000);
                        }
                    }

                    if (!_nginxProcess.HasExited)
                    {
                        _nginxProcess.Kill(entireProcessTree: true);
                    }

                    _nginxProcess.Dispose();
                    _nginxProcess = null;
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Erro ao finalizar processo Nginx.");
                }
            }

            return Task.CompletedTask;
        }

        private static bool TryStartNginxWindowsService(ILogger logger)
        {
            try
            {
                var queryInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"query {NginxServiceName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var queryProc = Process.Start(queryInfo);
                if (queryProc == null)
                {
                    return false;
                }

                var output = queryProc.StandardOutput.ReadToEnd();
                queryProc.WaitForExit(3000);

                if (output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Serviço Windows '{ServiceName}' já está em execução.", NginxServiceName);
                    return true;
                }

                if (output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase) || queryProc.ExitCode == 0)
                {
                    logger.LogInformation("Iniciando serviço Windows '{ServiceName}'...", NginxServiceName);
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = $"start {NginxServiceName}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var startProc = Process.Start(startInfo);
                    startProc?.WaitForExit(5000);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Tentativa de consultar/iniciar serviço Windows '{ServiceName}' falhou.", NginxServiceName);
                return false;
            }
        }

        private static void EnsureDuckDnsRunning(ILogger logger)
        {
            try
            {
                var existingProcesses = Process.GetProcessesByName("DuckDns");
                if (existingProcesses.Length > 0)
                {
                    return;
                }

                var progFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                var duckDnsExe = Path.Combine(progFiles86, "DuckDNS", "DuckDns.exe");

                if (File.Exists(duckDnsExe))
                {
                    logger.LogInformation("Iniciando cliente DuckDNS a partir de {Path}...", duckDnsExe);
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = duckDnsExe,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };
                    Process.Start(startInfo);
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Não foi possível verificar ou iniciar cliente DuckDNS.");
            }
        }
    }
}
