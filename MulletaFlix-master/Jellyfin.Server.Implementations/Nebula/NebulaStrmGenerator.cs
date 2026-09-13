#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Gerador nativo em C# de arquivos STRM para a biblioteca do MulletaFlix a partir do banco MongoDB.
/// </summary>
public sealed class NebulaStrmGenerator
{
    private readonly NebulaMongoContext _mongoContext;
    private readonly ILogger<NebulaStrmGenerator> _logger;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaStrmGenerator"/>.
    /// </summary>
    public NebulaStrmGenerator(NebulaMongoContext mongoContext, ILogger<NebulaStrmGenerator> logger)
    {
        _mongoContext = mongoContext;
        _logger = logger;
    }

    private static readonly System.Text.RegularExpressions.Regex SeasonPattern = new(
        @"(?i)(?:season|temporada)\s*(\d{1,2})",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex EpisodePattern = new(
        @"(?i)(?<prefix>.*?)(?:[.\s_-]+)?s(?<season>\d{1,2})[.\s_-]*e(?<episode>\d{1,3})",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Roteia a pasta de destino do arquivo .strm garantindo a estrutura padrão:
    /// - Filmes: Nebula\Filmes\Nome do Filme (Ano).
    /// - Séries: Nebula\Series\Nome da Série\Season ##.
    /// - Pornô:  Nebula\Porno.
    /// </summary>
    /// <param name="relativeDir">Diretório relativo original.</param>
    /// <param name="filename">Nome do arquivo da mídia.</param>
    /// <returns>Caminho relativo estruturado para o arquivo STRM.</returns>
    public static string RouteStrmRelativeDirectory(string? relativeDir, string filename)
    {
        var mediaType = NebulaUploadEngine.ClassifyMediaType(relativeDir, filename);

        if (mediaType == "PORNO")
        {
            return Path.Combine("Nebula", "Porno");
        }

        var rawParts = (relativeDir ?? string.Empty)
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.Equals(p, "strm", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(p, "nebula", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (mediaType == "SERIE")
        {
            if (rawParts.Count > 0 && (string.Equals(rawParts[0], "series", StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(rawParts[0], "serie", StringComparison.OrdinalIgnoreCase)))
            {
                rawParts.RemoveAt(0);
            }

            string seriesName;
            string seasonFolder;

            if (rawParts.Count >= 2)
            {
                seriesName = rawParts[0];
                var seasonPart = rawParts[1];
                var mSeason = SeasonPattern.Match(seasonPart);
                if (mSeason.Success && int.TryParse(mSeason.Groups[1].Value, out var sNum))
                {
                    seasonFolder = $"Season {sNum:D2}";
                }
                else
                {
                    seasonFolder = seasonPart;
                }
            }
            else if (rawParts.Count == 1)
            {
                var onlyPart = rawParts[0];
                var mSeason = SeasonPattern.Match(onlyPart);
                if (mSeason.Success && int.TryParse(mSeason.Groups[1].Value, out var sNum))
                {
                    var epMatch = EpisodePattern.Match(filename);
                    seriesName = epMatch.Success && !string.IsNullOrWhiteSpace(epMatch.Groups["prefix"].Value)
                        ? epMatch.Groups["prefix"].Value.Trim('.', ' ', '_', '-')
                        : "Outras Series";
                    seasonFolder = $"Season {sNum:D2}";
                }
                else
                {
                    seriesName = onlyPart;
                    var epMatch = EpisodePattern.Match(filename);
                    if (epMatch.Success && int.TryParse(epMatch.Groups["season"].Value, out var sNum2))
                    {
                        seasonFolder = $"Season {sNum2:D2}";
                    }
                    else
                    {
                        seasonFolder = "Season 01";
                    }
                }
            }
            else
            {
                var epMatch = EpisodePattern.Match(filename);
                if (epMatch.Success)
                {
                    seriesName = !string.IsNullOrWhiteSpace(epMatch.Groups["prefix"].Value)
                        ? epMatch.Groups["prefix"].Value.Trim('.', ' ', '_', '-')
                        : "Outras Series";
                    var sNum = int.TryParse(epMatch.Groups["season"].Value, out var s) ? s : 1;
                    seasonFolder = $"Season {sNum:D2}";
                }
                else
                {
                    seriesName = Path.GetFileNameWithoutExtension(filename);
                    seasonFolder = "Season 01";
                }
            }

            return Path.Combine("Nebula", "Series", seriesName, seasonFolder);
        }

        // Caso FILME:
        if (rawParts.Count > 0 && (string.Equals(rawParts[0], "filmes", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(rawParts[0], "filme", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(rawParts[0], "movies", StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(rawParts[0], "movie", StringComparison.OrdinalIgnoreCase)))
        {
            rawParts.RemoveAt(0);
        }

        string movieFolder;
        if (rawParts.Count > 0)
        {
            movieFolder = rawParts[0];
        }
        else
        {
            movieFolder = Path.GetFileNameWithoutExtension(filename);
        }

        return Path.Combine("Nebula", "Filmes", movieFolder);
    }

    /// <summary>
    /// Resolve o host/IP que deve ser inserido nos arquivos .strm.
    /// Se o host configurado for "0.0.0.0", "::", vazio ou loopback, tenta detectar o endereço IPv4 ativo na rede local.
    /// </summary>
    /// <param name="serverHost">Host configurado no Nebula.</param>
    /// <returns>Endereço IP ou hostname resolvido.</returns>
    public static string ResolveServerHost(string? serverHost)
    {
        if (!string.IsNullOrWhiteSpace(serverHost) &&
            !string.Equals(serverHost.Trim(), "0.0.0.0", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(serverHost.Trim(), "::", StringComparison.OrdinalIgnoreCase))
        {
            return serverHost.Trim();
        }

        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is System.Net.IPEndPoint endPoint &&
                !System.Net.IPAddress.IsLoopback(endPoint.Address))
            {
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            // Fallback via Dns
        }

        try
        {
            var hostName = System.Net.Dns.GetHostName();
            var hostEntry = System.Net.Dns.GetHostEntry(hostName);
            var localIp = hostEntry.AddressList.FirstOrDefault(
                ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                      !System.Net.IPAddress.IsLoopback(ip));
            if (localIp != null)
            {
                return localIp.ToString();
            }
        }
        catch
        {
            // Ignore
        }

        return "127.0.0.1";
    }

    /// <summary>
    /// Constrói o target (URL de rede) a ser gravado no arquivo .strm apontando para o IP do servidor e o caminho da mídia.
    /// Suporta streaming direto nativo via HTTP (porta 2123) com busca instantânea e compatibilidade universal, ou fallback FTP.
    /// </summary>
    /// <param name="config">Configuração do NebulaFTP.</param>
    /// <param name="relDir">Diretório relativo da mídia no catálogo.</param>
    /// <param name="fileName">Nome do arquivo da mídia.</param>
    /// <param name="useHttp">Indica se deve gerar URL HTTP nativa para streaming direto.</param>
    /// <returns>URL de rede formatada para o arquivo .strm.</returns>
    public static string BuildStrmTargetUrl(
        NebulaFtpConfiguration config,
        string? relDir,
        string fileName,
        bool useHttp = false)
    {
        var serverHost = ResolveServerHost(config.ServerHost);

        var virtualPath = string.IsNullOrEmpty(relDir) ? fileName : $"{relDir}/{fileName}";
        var cleanVirtualPath = virtualPath.TrimStart('/', '\\').Replace('\\', '/');

        var pathParts = cleanVirtualPath
            .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !string.Equals(p, "strm", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(p, "nebula", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Se o caminho começar com /raphael/ ou usuário configurado, remove para alinhar com o root
        if (pathParts.Count > 0 && (string.Equals(pathParts[0], "raphael", StringComparison.OrdinalIgnoreCase) ||
                                    (!string.IsNullOrWhiteSpace(config.Username) && string.Equals(pathParts[0], config.Username, StringComparison.OrdinalIgnoreCase))))
        {
            pathParts.RemoveAt(0);
        }

        var encodedPath = string.Join("/", pathParts.Select(Uri.EscapeDataString));

        if (useHttp)
        {
            var httpPort = config.HttpStreamPort > 0 ? config.HttpStreamPort : 2123;
            var encodedToken = Uri.EscapeDataString(config.HttpStreamToken ?? string.Empty);
            return $"http://{serverHost}:{httpPort}/stream?id={encodedPath}&token={encodedToken}";
        }

        var port = config.ServerPort > 0 ? config.ServerPort : 2121;

        var authPrefix = string.Empty;
        if (config.EmbedFtpCredentialsInStrmUrls &&
            !string.IsNullOrWhiteSpace(config.Username) &&
            !string.IsNullOrWhiteSpace(config.Password))
        {
            authPrefix = $"{Uri.EscapeDataString(config.Username)}:{Uri.EscapeDataString(config.Password)}@";
        }

        return $"ftp://{authPrefix}{serverHost}:{port}/{encodedPath}";
    }

    /// <summary>
    /// Executa a geração de arquivos .strm nas pastas de destino configuradas.
    /// </summary>
    public async Task<int> GenerateAsync(
        NebulaFtpConfiguration config,
        Action<string>? onLog = null,
        CancellationToken cancellationToken = default)
    {
        onLog?.Invoke("[STRM] Consultando catálogo de mídias no MongoDB...");
        _logger.LogInformation("[NEBULA-STRM] Iniciando geração da biblioteca STRM...");

        var dirPathMap = await _mongoContext.BuildDirectoryPathMapAsync(cancellationToken).ConfigureAwait(false);
        var files = await _mongoContext.GetAllCompletedFilesAsync(cancellationToken).ConfigureAwait(false);

        onLog?.Invoke($"[STRM] {files.Count} arquivos encontrados no banco de dados.");

        var targetRoots = (config.MonitorPaths ?? Array.Empty<string>())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (targetRoots.Count == 0)
        {
            var fallback = Path.Combine(AppContext.BaseDirectory, "midias");
            targetRoots.Add(fallback);
        }

        var resolvedHost = ResolveServerHost(config.ServerHost);
        var httpPort = config.HttpStreamPort > 0 ? config.HttpStreamPort : 2123;
        onLog?.Invoke($"[STRM] Endereço de streaming configurado: http://{resolvedHost}:{httpPort}/stream?id=... (streaming HTTP nativo via Nebula)");

        var generatedCount = 0;

        foreach (var fileDoc in files)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var name = fileDoc.GetValue("name", string.Empty).AsString;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var parentId = fileDoc.Contains("parent") && !fileDoc["parent"].IsBsonNull ? fileDoc["parent"].ToString() : null;
            var relDir = parentId != null && dirPathMap.TryGetValue(parentId, out var p) ? p : string.Empty;

            // Determina a string apontada dentro do arquivo .strm: streaming nativo HTTP (sem necessidade de disco montado)
            var strmTarget = BuildStrmTargetUrl(config, relDir, name, useHttp: true);

            var baseFileName = Path.GetFileNameWithoutExtension(name);
            var strmFileName = $"{baseFileName}.strm";
            var strmRelDir = RouteStrmRelativeDirectory(relDir, name);

            foreach (var root in targetRoots)
            {
                try
                {
                    var destDir = Path.Combine(root, strmRelDir);
                    Directory.CreateDirectory(destDir);

                    var strmFilePath = Path.Combine(destDir, strmFileName);
                    await File.WriteAllTextAsync(strmFilePath, strmTarget, cancellationToken).ConfigureAwait(false);
                    generatedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[NEBULA-STRM] Erro ao criar arquivo STRM para {Name}", name);
                }
            }
        }

        onLog?.Invoke($"[STRM] Geração concluída com sucesso! Total de {generatedCount} arquivos .strm gerados/atualizados.");
        _logger.LogInformation("[NEBULA-STRM] Geração de STRM finalizada: {Count} arquivos.", generatedCount);

        return generatedCount;
    }
}
