#pragma warning disable CA1707 // Identifiers should not contain underscores
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CA1307 // StringComparison
#pragma warning disable CA1308 // ToLowerInvariant

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MulletaFlix.Database.Implementations.Contexts;
using MulletaFlix.Database.Implementations.Entities;
using MulletaFlix.Database.Implementations.Enums;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Serviço nativo em C# para backup e restauração de usuários entre MongoDB e Supabase Cloud.
/// </summary>
public sealed class NebulaSupabaseSyncService : IDisposable
{
    public const string ServiceRoleKeyRequiredMessage = "O backup e a restauração exigem a Secret key do Supabase (sb_secret_... ou chave JWT com role service_role). Não use a chave publishable/anon e não conceda permissões de escrita à role anon.";

    private readonly ILogger<NebulaSupabaseSyncService> _logger;
    private readonly NebulaMongoContext _mongoContext;
    private readonly IDbContextFactory<UsersDbContext>? _usersDbProvider;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _backupGate = new(1, 1);
    private CancellationTokenSource? _continuousSyncCts;
    private Task? _continuousSyncTask;
    private DateTime? _lastSuccessfulBackupTime;
    private DateTime? _lastSuccessfulRestoreTime;
    private bool _disposed;

    /// <summary>
    /// Obtém a data e hora UTC do último backup bem-sucedido realizado nesta sessão.
    /// </summary>
    public DateTime? LastSuccessfulBackupTime => _lastSuccessfulBackupTime;

    /// <summary>
    /// Obtém a data e hora UTC da última restauração bem-sucedida realizada nesta sessão.
    /// </summary>
    public DateTime? LastSuccessfulRestoreTime => _lastSuccessfulRestoreTime;

    /// <summary>
    /// Verifica se a chave pode ser usada pelo backend para acessar tabelas protegidas por RLS.
    /// Chaves publishable/anon nunca devem receber permissões de escrita.
    /// </summary>
    public static bool IsServiceRoleKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var value = key.Trim();
        if (value.StartsWith("sb_secret_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("sb_publishable_", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("sb_anon_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Chaves JWT legadas carregam o papel no payload. Só service_role pode
        // operar as tabelas de backup com as políticas atuais.
        var parts = value.Split('.');
        if (parts.Length == 3)
        {
            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload += new string('=', (4 - (payload.Length % 4)) % 4);
                using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
                if (document.RootElement.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String)
                {
                    return string.Equals(role.GetString(), "service_role", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (FormatException)
            {
                // Chave não-JWT: mantém compatibilidade e deixa o Supabase validar.
            }
            catch (JsonException)
            {
                // Chave não-JWT: mantém compatibilidade e deixa o Supabase validar.
            }
        }

        // Formatos privados antigos podem não expor o papel localmente.
        return true;
    }

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaSupabaseSyncService"/>.
    /// </summary>
    public NebulaSupabaseSyncService(
        NebulaMongoContext mongoContext,
        ILogger<NebulaSupabaseSyncService> logger,
        IDbContextFactory<UsersDbContext>? usersDbProvider = null)
    {
        _mongoContext = mongoContext;
        _logger = logger;
        _usersDbProvider = usersDbProvider;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MulletaFlix-Server/12.0");
    }

    /// <summary>
    /// Inicia o ciclo de sincronização contínua em segundo plano.
    /// </summary>
    public void StartContinuousSync(string supabaseUrl, string supabaseKey, int intervalMinutes = 360, Action<string>? progressAction = null)
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            _logger.LogWarning("[SUPABASE-AUTO-SYNC] Supabase não configurado; sincronização contínua desativada.");
            return;
        }

        StopContinuousSync();
        _continuousSyncCts = new CancellationTokenSource();
        var ct = _continuousSyncCts.Token;

        _continuousSyncTask = Task.Run(
            async () =>
            {
                _logger.LogInformation("[SUPABASE-AUTO-SYNC] Loop de sincronização contínua iniciado (Intervalo: {Min} min).", intervalMinutes);

                // Primeira execução imediata
                try
                {
                    await PerformBackupAsync(supabaseUrl, supabaseKey, progressAction, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(ex, "[SUPABASE-AUTO-SYNC] Aviso na sincronização inicial com Supabase.");
                }

                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(Math.Max(5, intervalMinutes)), ct).ConfigureAwait(false);
                        _logger.LogInformation("[SUPABASE-AUTO-SYNC] Executando sincronização periódica programada...");
                        await PerformBackupAsync(supabaseUrl, supabaseKey, progressAction, ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[SUPABASE-AUTO-SYNC] Erro no ciclo de sincronização periódica.");
                    }
                }
            },
            ct);
    }

    /// <summary>
    /// Encerra a sincronização contínua em segundo plano.
    /// </summary>
    public void StopContinuousSync()
    {
        var task = _continuousSyncTask;
        try
        {
            _continuousSyncCts?.Cancel();

            if (task != null && !task.IsCompleted)
            {
                task.Wait(TimeSpan.FromSeconds(5));
            }

            _continuousSyncCts?.Dispose();
            _continuousSyncCts = null;
            _continuousSyncTask = null;
        }
        catch
        {
            // Ignora
        }
    }

    /// <summary>
    /// Sincroniza um nó individual do MongoDB imediatamente para o Supabase.
    /// </summary>
    public async Task<bool> SyncSingleNodeAsync(string supabaseUrl, string supabaseKey, BsonDocument doc, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey) || doc == null)
        {
            return false;
        }

        try
        {
            var record = ConvertBsonDocToSupabaseRecord(doc);
            var json = JsonSerializer.Serialize(new[] { record });

            var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_files?on_conflict=id";
            using var req = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", supabaseKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
            req.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogDebug("[SUPABASE-SYNC] Nó '{Name}' sincronizado em tempo real com o Supabase.", record.Name);
                return true;
            }

            var errorBody = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("[SUPABASE-SYNC] Falha ao sincronizar nó '{Name}': HTTP {Code} - {Body}", record.Name, resp.StatusCode, errorBody);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUPABASE-SYNC] Erro ao sincronizar nó individual.");
            return false;
        }
    }

    public async Task<List<string>> GetBotTokensAsync(string supabaseUrl, string supabaseKey, string tableName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey) || string.IsNullOrWhiteSpace(tableName))
        {
            return [];
        }

        try
        {
            // Supabase uses the canonical `token` column. Requesting the optional
            // legacy `bot_token` column makes PostgREST reject the entire query.
            var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/{Uri.EscapeDataString(tableName)}?select=token,enabled&order=index.asc";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("apikey", supabaseKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[SUPABASE-TOKENS] Falha ao carregar tokens: HTTP {Code}.", response.StatusCode);
                return [];
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
            return json.RootElement.ValueKind != JsonValueKind.Array
                ? []
                : json.RootElement.EnumerateArray()
                    .Where(item => !item.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False)
                    .Select(item => item.TryGetProperty("token", out var token) ? token : item.TryGetProperty("bot_token", out var botToken) ? botToken : default)
                    .Where(value => value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    .Select(value => value.GetString()!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or UriFormatException)
        {
            _logger.LogWarning(ex, "[SUPABASE-TOKENS] Não foi possível carregar tokens da tabela {Table}.", tableName);
            return [];
        }
    }

    /// <summary>
    /// Obtém a quantidade total de arquivos registrados no Supabase.
    /// </summary>
    public async Task<long> GetSupabaseFileCountAsync(string supabaseUrl, string supabaseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            return -1;
        }

        try
        {
            var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_files?select=id&limit=1";
            using var req = new HttpRequestMessage(HttpMethod.Head, uri);
            req.Headers.Add("apikey", supabaseKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
            req.Headers.Add("Prefer", "count=exact");

            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return -1;
            }

            if (resp.Content.Headers.TryGetValues("Content-Range", out var ranges))
            {
                var rangeStr = ranges.FirstOrDefault();
                if (!string.IsNullOrEmpty(rangeStr))
                {
                    var slashIdx = rangeStr.LastIndexOf('/');
                    if (slashIdx >= 0 && long.TryParse(rangeStr[(slashIdx + 1)..], out var count))
                    {
                        return count;
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SUPABASE-COUNT] Falha ao consultar total de arquivos no Supabase.");
            return -1;
        }
    }

    /// <summary>
    /// Obtém o timestamp UTC do último backup registrado com sucesso no Supabase.
    /// </summary>
    public async Task<DateTime?> GetLastRemoteBackupTimestampAsync(string supabaseUrl, string supabaseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            return null;
        }

        try
        {
            var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_backups?status=eq.success&order=id.desc&limit=1";
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.Add("apikey", supabaseKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                if (first.TryGetProperty("created_at", out var caProp) && caProp.TryGetDateTime(out var dt))
                {
                    return dt.ToUniversalTime();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SUPABASE-TIMESTAMP] Falha ao obter timestamp do último backup remoto.");
            return null;
        }
    }

    /// <summary>
    /// Obtém o timestamp de atualização mais recente já gravado na tabela de arquivos do Supabase.
    /// Serve de marcador quando não há registro de backup de usuários.
    /// </summary>
    public async Task<DateTime?> GetLastRemoteFileTimestampAsync(string supabaseUrl, string supabaseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            return null;
        }

        try
        {
            var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_files?select=updated_at&order=updated_at.desc&limit=1";
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.Add("apikey", supabaseKey);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

            using var resp = await _httpClient.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0 &&
                doc.RootElement[0].TryGetProperty("updated_at", out var updatedProp) &&
                updatedProp.TryGetDateTime(out var updatedAt))
            {
                return updatedAt.ToUniversalTime();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SUPABASE-TIMESTAMP] Falha ao obter o updated_at mais recente do Supabase.");
            return null;
        }
    }

    /// <summary>
    /// Executa um backup/sincronização do MongoDB para o Supabase (delta por padrão, ou completo).
    /// </summary>
    public Task<NebulaSupabaseBackupResultDto> PerformBackupAsync(
        string supabaseUrl,
        string supabaseKey,
        CancellationToken cancellationToken = default)
    {
        return PerformBackupAsync(supabaseUrl, supabaseKey, progressAction: null, forceFullSync: false, cancellationToken);
    }

    /// <summary>
    /// Executa um backup/sincronização do MongoDB para o Supabase (delta por padrão, ou completo) com relatório de progresso.
    /// </summary>
    public Task<NebulaSupabaseBackupResultDto> PerformBackupAsync(
        string supabaseUrl,
        string supabaseKey,
        Action<string>? progressAction,
        CancellationToken cancellationToken = default)
    {
        return PerformBackupAsync(supabaseUrl, supabaseKey, progressAction, forceFullSync: false, cancellationToken);
    }

    /// <summary>
    /// Executa um backup/sincronização do MongoDB para o Supabase em lotes otimizados, enviando apenas o delta ou todos os arquivos.
    /// </summary>
    public async Task<NebulaSupabaseBackupResultDto> PerformBackupAsync(
        string supabaseUrl,
        string supabaseKey,
        Action<string>? progressAction,
        bool forceFullSync,
        CancellationToken cancellationToken = default)
    {
        var result = new NebulaSupabaseBackupResultDto();
        var startTime = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            result.Success = false;
            result.Message = "Supabase URL e API Key são obrigatórios.";
            return result;
        }

        if (!IsServiceRoleKey(supabaseKey))
        {
            result.Success = false;
            result.Message = ServiceRoleKeyRequiredMessage;
            progressAction?.Invoke($"[SUPABASE-ERRO] {result.Message}");
            return result;
        }

        await _backupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Este é o fluxo do Nebula: MongoDB (arquivos, usuários FTP e tokens).
            // Usuários do aplicativo ficam exclusivamente na tela Usuários > Backup & Restore.
            _logger.LogInformation("[SUPABASE-SYNC] Backup do MongoDB iniciado; usuários do aplicativo ignorados.");
            progressAction?.Invoke("[SUPABASE-SYNC] Backup do MongoDB iniciado; mídia e usuários FTP incluídos.");

            var remoteCutoff = forceFullSync ? null : await GetLastRemoteFileTimestampAsync(supabaseUrl, supabaseKey, cancellationToken).ConfigureAwait(false);
            var fileDocs = remoteCutoff.HasValue
                ? await _mongoContext.GetSyncDeltaAsync(remoteCutoff.Value, cancellationToken).ConfigureAwait(false)
                : await _mongoContext.GetAllFilesForSyncAsync(cancellationToken).ConfigureAwait(false);
            var fileRecords = fileDocs.Select(ConvertBsonDocToSupabaseRecord).ToList();
            foreach (var batch in fileRecords.Chunk(100))
            {
                await SendSupabaseBatchWithRetryAsync(supabaseUrl, supabaseKey, "nebula_files?on_conflict=id", batch, cancellationToken).ConfigureAwait(false);
            }
            var syncedFiles = fileRecords.Count;

            // Usuários FTP do MongoDB
            var allUsers = await _mongoContext.GetAllUsersForSyncAsync(cancellationToken).ConfigureAwait(false);
            var syncedUsers = 0;
            if (allUsers.Count > 0)
            {
                var userRecords = new List<SupabaseUserRecord>();
                foreach (var userDoc in allUsers)
                {
                    userRecords.Add(ConvertBsonDocToSupabaseUser(userDoc));
                }

                var jsonUsers = JsonSerializer.Serialize(userRecords);
                var uriUsers = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_users?on_conflict=login";
                using var reqUsers = new HttpRequestMessage(HttpMethod.Post, uriUsers)
                {
                    Content = new StringContent(jsonUsers, Encoding.UTF8, "application/json")
                };
                reqUsers.Headers.Add("apikey", supabaseKey);
                reqUsers.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
                reqUsers.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

                using var respUsers = await _httpClient.SendAsync(reqUsers, cancellationToken).ConfigureAwait(false);
                if (respUsers.IsSuccessStatusCode)
                {
                    syncedUsers = userRecords.Count;
                }
            }

            // O backup é espelho dos usuários FTP atuais. Usuários do app têm outro fluxo.
            var deletedFtpUsers = await RemoveDeletedNebulaUsersAsync(supabaseUrl, supabaseKey, allUsers, cancellationToken).ConfigureAwait(false);

            // 5. Registra log na tabela nebula_backups
            var syncModeName = forceFullSync ? "mongo_full_sync" : "mongo_delta_sync";
            var backupLog = new
            {
                backup_type = syncModeName,
                status = "success",
                total_files = 0,
                total_users = syncedUsers,
                details = $"Backup do MongoDB concluído com {syncedFiles} arquivos e {syncedUsers} usuários FTP; {deletedFtpUsers} usuários removidos do backup. Usuários do aplicativo não incluídos."
            };

            try
            {
                var jsonLog = JsonSerializer.Serialize(backupLog);
                var uriLog = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_backups";
                using var reqLog = new HttpRequestMessage(HttpMethod.Post, uriLog)
                {
                    Content = new StringContent(jsonLog, Encoding.UTF8, "application/json")
                };
                reqLog.Headers.Add("apikey", supabaseKey);
                reqLog.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
                reqLog.Headers.Add("Prefer", "return=minimal");

                _ = await _httpClient.SendAsync(reqLog, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SUPABASE-SYNC] Aviso ao registrar histórico de backup no Supabase.");
            }

            _lastSuccessfulBackupTime = DateTime.UtcNow;
            result.Success = true;
            result.FilesBackedUp = syncedFiles;
            result.UsersBackedUp = syncedUsers;
            result.ElapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
            result.Message = $"Backup do MongoDB concluído! ({syncedFiles} arquivos e {syncedUsers} usuários FTP em {result.ElapsedSeconds:F1}s; usuários do aplicativo não incluídos)";
            _logger.LogInformation("[SUPABASE-SYNC] {Message}", result.Message);
            progressAction?.Invoke($"[SUPABASE] {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUPABASE-SYNC] Erro durante a sincronização com o Supabase.");
            result.Success = false;
            result.Message = $"Erro: {ex.Message}";
            progressAction?.Invoke($"[SUPABASE-ERRO] Falha na sincronização: {result.Message}");
            return result;
        }
        finally
        {
            _backupGate.Release();
        }
    }

    /// <summary>
    /// Restaura a biblioteca, usuários e tokens do Supabase para o MongoDB (delta por padrão, ou completo).
    /// </summary>
    public Task<NebulaSupabaseRestoreResultDto> PerformRestoreAsync(
        string supabaseUrl,
        string supabaseKey,
        CancellationToken cancellationToken = default)
    {
        return PerformRestoreAsync(supabaseUrl, supabaseKey, progressAction: null, forceFullRestore: false, cancellationToken);
    }

    /// <summary>
    /// Restaura a biblioteca, usuários e tokens do Supabase para o MongoDB com relatório de progresso.
    /// </summary>
    public Task<NebulaSupabaseRestoreResultDto> PerformRestoreAsync(
        string supabaseUrl,
        string supabaseKey,
        Action<string>? progressAction,
        CancellationToken cancellationToken = default)
    {
        return PerformRestoreAsync(supabaseUrl, supabaseKey, progressAction, forceFullRestore: false, cancellationToken);
    }

    /// <summary>
    /// Restaura usuários e tokens do Supabase para o MongoDB.
    /// </summary>
    public async Task<NebulaSupabaseRestoreResultDto> PerformRestoreAsync(
        string supabaseUrl,
        string supabaseKey,
        Action<string>? progressAction,
        bool forceFullRestore,
        CancellationToken cancellationToken = default)
    {
        _ = forceFullRestore;
        var result = new NebulaSupabaseRestoreResultDto();
        var startTime = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            result.Success = false;
            result.Message = "Supabase URL e API Key são obrigatórios.";
            return result;
        }

        if (!IsServiceRoleKey(supabaseKey))
        {
            result.Success = false;
            result.Message = ServiceRoleKeyRequiredMessage;
            progressAction?.Invoke($"[SUPABASE-ERRO] {result.Message}");
            return result;
        }

        await _backupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Esta tela é exclusiva de usuários. Nunca leia ou grave nebula_files:
            // a restauração não altera mídia, NFO, imagens, fila ou cache.
            const int restoredFiles = 0;
            _logger.LogInformation("[SUPABASE-RESTORE] Restauração de usuários iniciada; arquivos de mídia ignorados.");
            progressAction?.Invoke("[SUPABASE-RESTORE] Restauração de usuários iniciada; mídia ignorada.");

            // Restaura usuários
            var restoredUsers = 0;
            try
            {
                var uriUsers = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_users?select=*";
                using var reqUsers = new HttpRequestMessage(HttpMethod.Get, uriUsers);
                reqUsers.Headers.Add("apikey", supabaseKey);
                reqUsers.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

                using var respUsers = await _httpClient.SendAsync(reqUsers, cancellationToken).ConfigureAwait(false);
                if (respUsers.IsSuccessStatusCode)
                {
                    var bodyUsers = await respUsers.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using var docUsers = JsonDocument.Parse(bodyUsers);
                    if (docUsers.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in docUsers.RootElement.EnumerateArray())
                        {
                            if (item.TryGetProperty("doc_data", out var userDocData) && userDocData.ValueKind == JsonValueKind.Object)
                            {
                                var userBson = BsonDocument.Parse(userDocData.GetRawText());
                                await _mongoContext.UpsertRawUserDocAsync(userBson, cancellationToken).ConfigureAwait(false);
                                restoredUsers++;
                            }
                            else if (item.TryGetProperty("login", out var loginProp) && loginProp.ValueKind == JsonValueKind.String)
                            {
                                var login = loginProp.GetString()!;
                                var passHash = item.TryGetProperty("password_hash", out var pProp) ? pProp.GetString() ?? string.Empty : string.Empty;
                                var userBson = new BsonDocument
                                {
                                    { "_id", login },
                                    { "password_hash", passHash },
                                    { "permissions", "elradfmwM" },
                                    { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                                };
                                await _mongoContext.UpsertRawUserDocAsync(userBson, cancellationToken).ConfigureAwait(false);
                                restoredUsers++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SUPABASE-RESTORE] Aviso ao restaurar tabela de usuários.");
            }

            var restoredAppUsers = await RestoreMulletaFlixUsersAsync(supabaseUrl, supabaseKey, cancellationToken).ConfigureAwait(false);

            // 3. Restaura tokens de bot
            var restoredTokens = 0;
            try
            {
                var uriTokens = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_bot_tokens?select=*&order=index.asc";
                using var reqTokens = new HttpRequestMessage(HttpMethod.Get, uriTokens);
                reqTokens.Headers.Add("apikey", supabaseKey);
                reqTokens.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

                using var respTokens = await _httpClient.SendAsync(reqTokens, cancellationToken).ConfigureAwait(false);
                if (respTokens.IsSuccessStatusCode)
                {
                    var bodyTokens = await respTokens.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    using var docTokens = JsonDocument.Parse(bodyTokens);
                    if (docTokens.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in docTokens.RootElement.EnumerateArray())
                        {
                            var token = item.TryGetProperty("token", out var tProp) ? tProp.GetString() : (item.TryGetProperty("bot_token", out var btProp) ? btProp.GetString() : null);
                            if (!string.IsNullOrWhiteSpace(token))
                            {
                                var idx = item.TryGetProperty("index", out var iProp) && iProp.TryGetInt32(out var iVal) ? iVal : (restoredTokens + 1);
                                var enabled = !item.TryGetProperty("enabled", out var eProp) || eProp.ValueKind != JsonValueKind.False;
                                var tokenBson = new BsonDocument
                                {
                                    { "index", idx },
                                    { "token", token.Trim() },
                                    { "enabled", enabled },
                                    { "updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                                };
                                await _mongoContext.UpsertRawTokenDocAsync(tokenBson, cancellationToken).ConfigureAwait(false);
                                restoredTokens++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[SUPABASE-RESTORE] Aviso ao restaurar tokens de bot.");
            }

            _lastSuccessfulRestoreTime = DateTime.UtcNow;
            result.Success = true;
            result.FilesRestored = restoredFiles;
            result.UsersRestored = restoredUsers + restoredAppUsers;
            result.ElapsedSeconds = (DateTime.UtcNow - startTime).TotalSeconds;
            result.Message = $"Restauração de usuários finalizada com sucesso! {restoredUsers} usuários FTP e {restoredAppUsers} usuários do MulletaFlix recuperados em {result.ElapsedSeconds:F1}s. Mídia não alterada.";
            _logger.LogInformation("[SUPABASE-RESTORE] {Message}", result.Message);
            progressAction?.Invoke($"[SUPABASE-RESTORE] {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SUPABASE-RESTORE] Erro ao restaurar do Supabase.");
            result.Success = false;
            result.Message = $"Erro: {ex.Message}";
            progressAction?.Invoke($"[SUPABASE-RESTORE-ERRO] Falha na restauração: {result.Message}");
            return result;
        }
        finally
        {
            _backupGate.Release();
        }
    }

    private static SupabaseFileRecord ConvertBsonDocToSupabaseRecord(BsonDocument doc)
    {
        var id = doc.GetValue("_id").ToString()!;
        var name = doc.GetValue("name", string.Empty).AsString;
        var parent = doc.Contains("parent") && !doc["parent"].IsBsonNull ? doc["parent"].ToString() : null;
        var size = doc.Contains("size") && doc["size"].IsNumeric ? doc["size"].ToInt64() : 0L;
        var status = doc.GetValue("status", "completed").AsString;
        var uploadedAt = doc.Contains("uploaded_at") && doc["uploaded_at"].IsNumeric ? (double?)doc["uploaded_at"].ToDouble() : null;

        var cleanDict = new Dictionary<string, object?>();
        foreach (var elem in doc.Elements)
        {
            if (elem.Name == "_id")
            {
                cleanDict["_id"] = elem.Value.ToString();
            }
            else if (elem.Name == "parent" && !elem.Value.IsBsonNull)
            {
                cleanDict["parent"] = elem.Value.ToString();
            }
            else
            {
                cleanDict[elem.Name] = BsonTypeToNetObject(elem.Value);
            }
        }

        object? partsObj = null;
        if (cleanDict.TryGetValue("parts", out var pObj))
        {
            partsObj = pObj;
            // Remove parts duplicado de doc_data para reduzir pela metade o tamanho do payload
            // e aliviar a memória do Postgres / PostgREST no Supabase
            cleanDict.Remove("parts");
        }

        return new SupabaseFileRecord
        {
            Id = id,
            Name = name,
            Parent = parent,
            Size = size,
            Status = status,
            Parts = partsObj,
            UploadedAt = uploadedAt,
            DocData = cleanDict
        };
    }

    /// <summary>
    /// Fluxo separado da tela Usuários: somente usuários relacionais do MulletaFlix.
    /// </summary>
    public async Task<NebulaSupabaseBackupResultDto> PerformUsersBackupAsync(
        string supabaseUrl,
        string supabaseKey,
        Action<string>? progressAction = null,
        CancellationToken cancellationToken = default)
    {
        var result = new NebulaSupabaseBackupResultDto();
        var started = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            result.Message = "Supabase URL e API Key são obrigatórios.";
            return result;
        }

        if (!IsServiceRoleKey(supabaseKey))
        {
            result.Message = ServiceRoleKeyRequiredMessage;
            progressAction?.Invoke($"[SUPABASE-ERRO] {result.Message}");
            return result;
        }

        await _backupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progressAction?.Invoke("[SUPABASE-USERS] Backup de usuários iniciado; MongoDB e mídia ignorados.");
            var count = await BackupMulletaFlixUsersAsync(supabaseUrl, supabaseKey, cancellationToken).ConfigureAwait(false);
            var removed = await RemoveDeletedMulletaFlixUsersAsync(supabaseUrl, supabaseKey, cancellationToken).ConfigureAwait(false);
            result.Success = true;
            result.UsersBackedUp = count;
            result.ElapsedSeconds = (DateTime.UtcNow - started).TotalSeconds;
            result.Message = $"Backup de usuários concluído! ({count} usuários; {removed} removidos do backup; mídia não incluída)";
            progressAction?.Invoke($"[SUPABASE-USERS] {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            result.Message = $"Erro: {ex.Message}";
            progressAction?.Invoke($"[SUPABASE-ERRO] {result.Message}");
            return result;
        }
        finally
        {
            _backupGate.Release();
        }
    }

    /// <summary>
    /// Fluxo separado da tela Usuários: restaura somente usuários relacionais.
    /// </summary>
    public async Task<NebulaSupabaseRestoreResultDto> PerformUsersRestoreAsync(
        string supabaseUrl,
        string supabaseKey,
        Action<string>? progressAction = null,
        CancellationToken cancellationToken = default)
    {
        var result = new NebulaSupabaseRestoreResultDto();
        var started = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
        {
            result.Message = "Supabase URL e API Key são obrigatórios.";
            return result;
        }

        if (!IsServiceRoleKey(supabaseKey))
        {
            result.Message = ServiceRoleKeyRequiredMessage;
            progressAction?.Invoke($"[SUPABASE-ERRO] {result.Message}");
            return result;
        }

        await _backupGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            progressAction?.Invoke("[SUPABASE-USERS] Restauração de usuários iniciada; MongoDB e mídia não serão alterados.");
            var count = await RestoreMulletaFlixUsersAsync(supabaseUrl, supabaseKey, cancellationToken).ConfigureAwait(false);
            result.Success = true;
            result.UsersRestored = count;
            result.ElapsedSeconds = (DateTime.UtcNow - started).TotalSeconds;
            result.Message = $"Restauração de usuários concluída! ({count} usuários; mídia não alterada)";
            progressAction?.Invoke($"[SUPABASE-USERS] {result.Message}");
            return result;
        }
        catch (Exception ex)
        {
            result.Message = $"Erro: {ex.Message}";
            progressAction?.Invoke($"[SUPABASE-ERRO] {result.Message}");
            return result;
        }
        finally
        {
            _backupGate.Release();
        }
    }

    private async Task SendSupabaseBatchWithRetryAsync<T>(
        string supabaseUrl,
        string supabaseKey,
        string endpoint,
        IReadOnlyCollection<T> records,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(records);
        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/{endpoint}";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("apikey", supabaseKey);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
                request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
                if (!retryable || attempt == 3)
                {
                    throw new InvalidOperationException($"Supabase rejeitou o lote: HTTP {(int)response.StatusCode} - {body}");
                }

                _logger.LogWarning("[SUPABASE-SYNC] Falha transitória no lote (tentativa {Attempt}/3): HTTP {Code} - {Body}", attempt, response.StatusCode, body);
            }
            catch (Exception ex) when (attempt < 3 && !cancellationToken.IsCancellationRequested && (ex is HttpRequestException or TaskCanceledException))
            {
                _logger.LogWarning(ex, "[SUPABASE-SYNC] Erro transitório no lote (tentativa {Attempt}/3).", attempt);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt), cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException("Não foi possível enviar o lote ao Supabase após 3 tentativas.");
    }

    /// <summary>
    /// Verifica quantos usuários reais do MulletaFlix existem no banco relacional.
    /// </summary>
    public async Task<int> GetMulletaFlixUserCountAsync(CancellationToken cancellationToken = default)
    {
        if (_usersDbProvider == null)
        {
            return 0;
        }

        await using var db = await _usersDbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await db.Users.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém a quantidade de usuários do MulletaFlix já armazenados no Supabase.
    /// </summary>
    public async Task<int> GetMulletaFlixUserBackupCountAsync(string supabaseUrl, string supabaseKey, CancellationToken cancellationToken = default)
    {
        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/mulletaflix_users?select=id";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        return document.RootElement.ValueKind == JsonValueKind.Array
            ? document.RootElement.GetArrayLength()
            : 0;
    }

    private async Task<int> BackupMulletaFlixUsersAsync(string supabaseUrl, string supabaseKey, CancellationToken cancellationToken)
    {
        if (_usersDbProvider == null)
        {
            return 0;
        }

        await using var db = await _usersDbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var users = await db.Users
            .Include(user => user.Permissions)
            .Include(user => user.License)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = users.Select(user => new MulletaFlixUserRecord
        {
            Id = user.Id,
            Username = user.Username,
            NormalizedUsername = user.NormalizedUsername,
            Password = user.Password,
            PhoneNumber = user.PhoneNumber,
            MustUpdatePassword = user.MustUpdatePassword,
            AuthenticationProviderId = user.AuthenticationProviderId,
            PasswordResetProviderId = user.PasswordResetProviderId,
            EnableLocalPassword = user.EnableLocalPassword,
            EnableUserPreferenceAccess = user.EnableUserPreferenceAccess,
            Permissions = user.Permissions.Select(permission => new MulletaFlixPermissionRecord
            {
                Kind = (int)permission.Kind,
                Value = permission.Value
            }).ToList(),
            License = user.License == null ? null : new MulletaFlixLicenseRecord
            {
                StartDate = user.License.StartDate,
                DurationHours = user.License.DurationHours,
                ExpirationDate = user.License.ExpirationDate,
                IsUnlimited = user.License.IsUnlimited,
                AdminNotes = user.License.AdminNotes,
                GrantedByUserId = user.License.GrantedByUserId,
                CreatedAt = user.License.CreatedAt,
                UpdatedAt = user.License.UpdatedAt
            }
        })
        // Supabase has a unique index on normalized_username. Keep one local
        // record per normalized name before sending the batch.
        .Where(record => !string.IsNullOrWhiteSpace(record.NormalizedUsername))
        .GroupBy(record => record.NormalizedUsername, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToList();

        // Conflict target must match the remote unique index. Using only id
        // caused HTTP 409 when a stale row kept the same username.
        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/mulletaflix_users?on_conflict=normalized_username";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(JsonSerializer.Serialize(records), Encoding.UTF8, "application/json")
        };
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        request.Headers.Add("Prefer", "resolution=merge-duplicates,return=minimal");

        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound && body.Contains("PGRST205", StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "[SUPABASE-SYNC] A tabela mulletaflix_users não existe no projeto remoto; backup dos usuários do aplicativo foi ignorado. Crie a tabela/migração para habilitar esta etapa.");
                return 0;
            }

            throw new InvalidOperationException($"Falha ao salvar usuários do MulletaFlix no Supabase: HTTP {(int)response.StatusCode} - {body}");
        }

        return records.Count;
    }

    private async Task<int> RemoveDeletedNebulaUsersAsync(
        string supabaseUrl,
        string supabaseKey,
        IReadOnlyCollection<BsonDocument> localUsers,
        CancellationToken cancellationToken)
    {
        if (localUsers.Count == 0)
        {
            _logger.LogWarning("[SUPABASE-SYNC] Nenhum usuário FTP local encontrado; remoção remota foi ignorada por segurança.");
            return 0;
        }

        var localLogins = localUsers
            .Select(doc => doc.GetValue("_id", string.Empty).ToString())
            .Where(static login => !string.IsNullOrWhiteSpace(login))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/nebula_users?select=login";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[SUPABASE-SYNC] Não foi possível listar usuários FTP remotos; nenhuma remoção será feita.");
            return 0;
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var deleted = 0;
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("login", out var loginProperty) || loginProperty.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var login = loginProperty.GetString();
            if (string.IsNullOrWhiteSpace(login) || localLogins.Contains(login))
            {
                continue;
            }

            if (await DeleteSupabaseRowAsync(supabaseUrl, supabaseKey, "nebula_users", "login", login, cancellationToken).ConfigureAwait(false))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private async Task<int> RemoveDeletedMulletaFlixUsersAsync(
        string supabaseUrl,
        string supabaseKey,
        CancellationToken cancellationToken)
    {
        if (_usersDbProvider == null)
        {
            return 0;
        }

        await using var db = await _usersDbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var localIds = (await db.Users.Select(user => user.Id).ToListAsync(cancellationToken).ConfigureAwait(false))
            .ToHashSet();
        if (localIds.Count == 0)
        {
            _logger.LogWarning("[SUPABASE-SYNC] Nenhum usuário MulletaFlix local encontrado; remoção remota foi ignorada por segurança.");
            return 0;
        }

        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/mulletaflix_users?select=id";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[SUPABASE-SYNC] Não foi possível listar usuários MulletaFlix remotos; nenhuma remoção será feita.");
            return 0;
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        if (json.RootElement.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var deleted = 0;
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProperty) || idProperty.ValueKind != JsonValueKind.String || !Guid.TryParse(idProperty.GetString(), out var id) || localIds.Contains(id))
            {
                continue;
            }

            if (await DeleteSupabaseRowAsync(supabaseUrl, supabaseKey, "mulletaflix_users", "id", id.ToString(), cancellationToken).ConfigureAwait(false))
            {
                deleted++;
            }
        }

        return deleted;
    }

    private async Task<bool> DeleteSupabaseRowAsync(
        string supabaseUrl,
        string supabaseKey,
        string table,
        string column,
        string value,
        CancellationToken cancellationToken)
    {
        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/{table}?{column}=eq.{Uri.EscapeDataString(value)}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, uri);
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        request.Headers.Add("Prefer", "return=minimal");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[SUPABASE-SYNC] Não foi possível remover registro antigo de {Table}: HTTP {StatusCode}.", table, response.StatusCode);
            return false;
        }

        return true;
    }

    private async Task<int> RestoreMulletaFlixUsersAsync(string supabaseUrl, string supabaseKey, CancellationToken cancellationToken)
    {
        if (_usersDbProvider == null)
        {
            return 0;
        }

        var uri = $"{supabaseUrl.TrimEnd('/')}/rest/v1/mulletaflix_users?select=*&order=username.asc";
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("apikey", supabaseKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return 0;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var records = JsonSerializer.Deserialize<List<MulletaFlixUserRecord>>(body) ?? [];
        await using var db = await _usersDbProvider.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var restored = 0;

        foreach (var record in records)
        {
            if (record.Id == Guid.Empty || string.IsNullOrWhiteSpace(record.Username))
            {
                continue;
            }

            var user = await db.Users
                .Include(item => item.Permissions)
                .FirstOrDefaultAsync(item => item.Id == record.Id || item.NormalizedUsername == record.NormalizedUsername, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                user = new User(record.Username, record.AuthenticationProviderId, record.PasswordResetProviderId)
                {
                    Id = record.Id
                };
                db.Users.Add(user);
            }

            user.Username = record.Username;
            user.NormalizedUsername = record.NormalizedUsername;
            user.Password = record.Password;
            user.PhoneNumber = record.PhoneNumber;
            user.MustUpdatePassword = record.MustUpdatePassword;
            user.AuthenticationProviderId = record.AuthenticationProviderId;
            user.PasswordResetProviderId = record.PasswordResetProviderId;
            user.EnableLocalPassword = record.EnableLocalPassword;
            user.EnableUserPreferenceAccess = record.EnableUserPreferenceAccess;

            db.Permissions.RemoveRange(user.Permissions);
            user.Permissions.Clear();
            foreach (var permission in record.Permissions)
            {
                user.Permissions.Add(new Permission((PermissionKind)permission.Kind, permission.Value)
                {
                    UserId = user.Id
                });
            }

            var license = await db.UserLicenses
                .FirstOrDefaultAsync(item => item.UserId == user.Id, cancellationToken)
                .ConfigureAwait(false);
            if (record.License == null)
            {
                if (license != null)
                {
                    db.UserLicenses.Remove(license);
                }
            }
            else
            {
                license ??= new UserLicense { UserId = user.Id };
                license.StartDate = record.License.StartDate;
                license.DurationHours = record.License.DurationHours;
                license.ExpirationDate = record.License.ExpirationDate;
                license.IsUnlimited = record.License.IsUnlimited;
                license.AdminNotes = record.License.AdminNotes;
                license.GrantedByUserId = record.License.GrantedByUserId;
                license.CreatedAt = record.License.CreatedAt;
                license.UpdatedAt = record.License.UpdatedAt;
                if (license.Id == 0)
                {
                    db.UserLicenses.Add(license);
                }
            }

            restored++;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return restored;
    }

    private static SupabaseUserRecord ConvertBsonDocToSupabaseUser(BsonDocument doc)
    {
        var login = doc.GetValue("_id").ToString()!;
        var passHash = doc.GetValue("password_hash", string.Empty).AsString;

        var cleanDict = new Dictionary<string, object?>();
        foreach (var elem in doc.Elements)
        {
            cleanDict[elem.Name] = elem.Name == "_id" ? elem.Value.ToString() : BsonTypeToNetObject(elem.Value);
        }

        return new SupabaseUserRecord
        {
            Login = login,
            PasswordHash = passHash,
            Permissions = cleanDict.GetValueOrDefault("permissions"),
            DocData = cleanDict
        };
    }

    private sealed class MulletaFlixUserRecord
    {
        [JsonPropertyName("id")] public Guid Id { get; set; }
        [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
        [JsonPropertyName("normalized_username")] public string NormalizedUsername { get; set; } = string.Empty;
        [JsonPropertyName("password")] public string? Password { get; set; }
        [JsonPropertyName("phone_number")] public string? PhoneNumber { get; set; }
        [JsonPropertyName("must_update_password")] public bool MustUpdatePassword { get; set; }
        [JsonPropertyName("authentication_provider_id")] public string AuthenticationProviderId { get; set; } = string.Empty;
        [JsonPropertyName("password_reset_provider_id")] public string PasswordResetProviderId { get; set; } = string.Empty;
        [JsonPropertyName("enable_local_password")] public bool EnableLocalPassword { get; set; }
        [JsonPropertyName("enable_user_preference_access")] public bool EnableUserPreferenceAccess { get; set; }
        [JsonPropertyName("permissions")] public List<MulletaFlixPermissionRecord> Permissions { get; set; } = [];
        [JsonPropertyName("license")] public MulletaFlixLicenseRecord? License { get; set; }
    }

    private sealed class MulletaFlixPermissionRecord
    {
        [JsonPropertyName("kind")] public int Kind { get; set; }
        [JsonPropertyName("value")] public bool Value { get; set; }
    }

    private sealed class MulletaFlixLicenseRecord
    {
        [JsonPropertyName("start_date")] public DateTime StartDate { get; set; }
        [JsonPropertyName("duration_hours")] public int? DurationHours { get; set; }
        [JsonPropertyName("expiration_date")] public DateTime? ExpirationDate { get; set; }
        [JsonPropertyName("is_unlimited")] public bool IsUnlimited { get; set; }
        [JsonPropertyName("admin_notes")] public string? AdminNotes { get; set; }
        [JsonPropertyName("granted_by_user_id")] public Guid? GrantedByUserId { get; set; }
        [JsonPropertyName("created_at")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("updated_at")] public DateTime UpdatedAt { get; set; }
    }

    private static object? BsonTypeToNetObject(BsonValue val)
    {
        if (val.IsBsonNull)
        {
            return null;
        }

        if (val.IsBoolean)
        {
            return val.AsBoolean;
        }

        if (val.IsInt32)
        {
            return val.AsInt32;
        }

        if (val.IsInt64)
        {
            return val.AsInt64;
        }

        if (val.IsDouble)
        {
            return val.AsDouble;
        }

        if (val.IsString)
        {
            return val.AsString;
        }

        if (val.IsBsonArray)
        {
            var list = new List<object?>();
            foreach (var item in val.AsBsonArray)
            {
                list.Add(BsonTypeToNetObject(item));
            }

            return list;
        }

        if (val.IsBsonDocument)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var elem in val.AsBsonDocument.Elements)
            {
                dict[elem.Name] = BsonTypeToNetObject(elem.Value);
            }

            return dict;
        }

        return val.ToString();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopContinuousSync();
        _httpClient.Dispose();
        _backupGate.Dispose();
        _disposed = true;
    }

    private sealed class SupabaseFileRecord
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("parent")]
        public string? Parent { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = "completed";

        [JsonPropertyName("parts")]
        public object? Parts { get; set; }

        [JsonPropertyName("uploaded_at")]
        public double? UploadedAt { get; set; }

        [JsonPropertyName("doc_data")]
        public object? DocData { get; set; }
    }

    private sealed class SupabaseUserRecord
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;

        [JsonPropertyName("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [JsonPropertyName("permissions")]
        public object? Permissions { get; set; }

        [JsonPropertyName("doc_data")]
        public object? DocData { get; set; }
    }
}
