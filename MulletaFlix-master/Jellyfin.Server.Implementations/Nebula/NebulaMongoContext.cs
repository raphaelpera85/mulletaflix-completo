using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Nebula;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Camada de acesso a dados nativa em C# para o MongoDB do Nebula.
/// </summary>
public sealed class NebulaMongoContext : IDisposable
{
    /// <summary>
    /// Estados de um arquivo que ainda não foi enviado ao Telegram. Eles entram
    /// sempre no delta de sincronização, para que o cache remoto reflita tanto o
    /// que já foi enviado quanto o que ainda falta enviar.
    /// </summary>
    private static readonly string[] NotUploadedStatuses = ["queued", "staging", "uploading", "failed"];

    /// <summary>
    /// Estados de um arquivo que está na fila ou em envio: o worker de upload é o
    /// dono desses arquivos e a varredura de staging não precisa reavaliá-los.
    /// </summary>
    private static readonly string[] InFlightStatuses = ["queued", "staging", "uploading"];

    private readonly ILogger<NebulaMongoContext> _logger;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<BsonDocument> _filesCollection;
    private readonly IMongoCollection<BsonDocument> _usersCollection;
    private readonly IMongoCollection<BsonDocument> _botTokensCollection;
    private readonly IMongoCollection<BsonDocument> _operationReplaysCollection;

    /// <summary>
    /// Diário da última varredura de staging: cada arquivo já tratado é lembrado
    /// com tamanho e data de modificação, para que a varredura seguinte processe
    /// apenas o delta (arquivos novos ou alterados).
    /// </summary>
    private readonly ConcurrentDictionary<string, StagingScanEntry> _stagingScanJournal = new(StringComparer.OrdinalIgnoreCase);

    private bool _disposed;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaMongoContext"/>.
    /// </summary>
    /// <param name="connectionString">URI de conexão com o MongoDB.</param>
    /// <param name="databaseName">Nome do banco de dados (padrão: "ftp").</param>
    /// <param name="logger">Instância de logger.</param>
    public NebulaMongoContext(string connectionString, string databaseName, ILogger<NebulaMongoContext> logger)
    {
        _logger = logger;
        var uri = string.IsNullOrWhiteSpace(connectionString) ? "mongodb://localhost:27017" : connectionString;
        var dbName = string.IsNullOrWhiteSpace(databaseName) ? "ftp" : databaseName;

        var settings = MongoClientSettings.FromConnectionString(uri);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(15);
        settings.ConnectTimeout = TimeSpan.FromSeconds(10);
        settings.SocketTimeout = TimeSpan.FromSeconds(30);
        settings.MaxConnectionPoolSize = 250;
        settings.RetryReads = true;
        settings.RetryWrites = true;

        _client = new MongoClient(settings);
        _database = _client.GetDatabase(dbName);
        _filesCollection = _database.GetCollection<BsonDocument>("files");
        _usersCollection = _database.GetCollection<BsonDocument>("users");
        _botTokensCollection = _database.GetCollection<BsonDocument>("bot_tokens");
        _operationReplaysCollection = _database.GetCollection<BsonDocument>("operation_replays");
    }

    /// <summary>
    /// Garante que o banco compartilhado tenha as coleções básicas do Nebula.
    /// O MongoDB cria o banco na primeira coleção criada; a operação é idempotente
    /// e preserva as coleções existentes para permitir uso compartilhado com outro
    /// cliente Nebula.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        foreach (var collectionName in new[] { "files", "users", "bot_tokens", "operation_replays" })
        {
            try
            {
                await _database.CreateCollectionAsync(collectionName, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("[NEBULA-MONGO] Coleção {Collection} criada ou inicializada.", collectionName);
            }
            catch (MongoCommandException ex) when (ex.Code == 48 || ex.CodeName == "NamespaceExists")
            {
                // A coleção já existe; isso é o caminho normal quando os dois
                // sistemas compartilham o banco ftp.
            }
        }
    }

    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        await _database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Garante que todos os índices necessários no MongoDB estão criados.
    /// </summary>
    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var indexes = new (IndexKeysDefinition<BsonDocument> Keys, string Name)[]
        {
            (Builders<BsonDocument>.IndexKeys.Ascending("status"), "status_1"),
            (Builders<BsonDocument>.IndexKeys.Ascending("parent"), "parent_1"),
            (Builders<BsonDocument>.IndexKeys.Ascending("name"), "name_1"),
            (Builders<BsonDocument>.IndexKeys.Ascending("parent").Ascending("name"), "parent_1_name_1"),
            (Builders<BsonDocument>.IndexKeys.Descending("modified_at"), "modified_at_-1"),
            (Builders<BsonDocument>.IndexKeys.Descending("uploaded_at"), "uploaded_at_-1")
        };

        foreach (var (keys, name) in indexes)
        {
            try
            {
                var model = new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = name, Background = true });
                await _filesCollection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-MONGO] Índice {Name} já existe ou não pôde ser recriado.", name);
            }
        }

        try
        {
            // A fila pode ser alimentada pelo downloader, pelo watcher e pelo
            // scanner ao mesmo tempo. Esta chave torna o registro idempotente
            // mesmo quando dois desses caminhos chegam juntos.
            await _filesCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("queue_identity"),
                    new CreateIndexOptions { Name = "queue_identity_1", Unique = true, Sparse = true, Background = true }),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-MONGO] Índice único da fila já existe ou não pôde ser criado.");
        }

        try
        {
            await _botTokensCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("enabled").Ascending("index"),
                    new CreateIndexOptions { Name = "enabled_1_index_1", Background = true }),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-MONGO] Índice da coleção de tokens já existe ou não pôde ser criado.");
        }

        try
        {
            await _operationReplaysCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("operation").Ascending("idempotency_key"),
                    new CreateIndexOptions { Name = "operation_1_idempotency_key_1", Unique = true, Background = true }),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-MONGO] Índice de replay de operações já existe ou não pôde ser criado.");
        }

        try
        {
            await _operationReplaysCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<BsonDocument>(
                    Builders<BsonDocument>.IndexKeys.Ascending("expires_at"),
                    new CreateIndexOptions { Name = "expires_at_1", ExpireAfter = TimeSpan.Zero, Background = true }),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-MONGO] Índice TTL de replay de operações já existe ou não pôde ser criado.");
        }

        await CleanupStrmRootDirectoryAsync(cancellationToken).ConfigureAwait(false);
        await RemoveProtectedContentAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[NEBULA-MONGO] Índices do MongoDB verificados com sucesso.");
    }

    /// <summary>
    /// Remove do catálogo remoto registros de NFO, imagens e legendas.
    /// Esses arquivos permanecem no armazenamento local do servidor e não
    /// participam do catálogo montado nem da fila do Telegram.
    /// </summary>
    public async Task<long> RemoveProtectedContentAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Regex("name", NebulaProtectedContent.NameExpression),
            Builders<BsonDocument>.Filter.Regex("local_path", NebulaProtectedContent.NameExpression));
        var result = await _filesCollection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
        if (result.DeletedCount > 0)
        {
            _logger.LogInformation(
                "[NEBULA-MONGO] {Count} registros de NFO, imagens e legendas removidos do catálogo montado.",
                result.DeletedCount);
        }

        return result.DeletedCount;
    }

    /// <summary>
    /// Recupera o resultado persistido de uma operação idempotente.
    /// Falhas de disponibilidade do Mongo são propagadas para que o chamador
    /// possa usar o cache de processo como fallback sem duplicar a operação.
    /// </summary>
    public async Task<T?> GetOperationReplayAsync<T>(string operation, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("operation", operation),
            Builders<BsonDocument>.Filter.Eq("idempotency_key", idempotencyKey),
            Builders<BsonDocument>.Filter.Gt("expires_at", DateTime.UtcNow));
        var document = await _operationReplaysCollection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null || !document.TryGetValue("result_json", out var json) || !json.IsString)
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json.AsString);
    }

    /// <summary>
    /// Persiste o resultado resumido de uma operação idempotente por 15 minutos.
    /// Apenas o DTO de resultado é salvo; segredos e payloads de configuração não
    /// fazem parte deste documento.
    /// </summary>
    public async Task SaveOperationReplayAsync<T>(string operation, string idempotencyKey, T result, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(result);
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("operation", operation),
            Builders<BsonDocument>.Filter.Eq("idempotency_key", idempotencyKey));
        var document = new BsonDocument
        {
            ["operation"] = operation,
            ["idempotency_key"] = idempotencyKey,
            ["result_json"] = json,
            ["expires_at"] = DateTime.UtcNow.Add(ttl)
        };
        await _operationReplaysCollection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Limpa qualquer pasta raiz virtual 'strm' residual do MongoDB e move seus filhos para a raiz.
    /// </summary>
    public async Task CleanupStrmRootDirectoryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var strmFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression("^strm$", "i")),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("is_directory", true),
                    Builders<BsonDocument>.Filter.Eq("type", "dir")));

            using var cursor = await _filesCollection.FindAsync(strmFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
            var strmDocs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var strmDoc in strmDocs)
            {
                var strmId = strmDoc.GetValue("_id");
                var parent = strmDoc.Contains("parent") ? strmDoc["parent"] : BsonNull.Value;
                var isRootParent = parent.IsBsonNull ||
                                   (parent.IsString && (string.IsNullOrEmpty(parent.AsString) || parent.AsString == "/" || string.Equals(parent.AsString, "/raphael", StringComparison.OrdinalIgnoreCase)));

                if (isRootParent)
                {
                    // Re-parenta os filhos de 'strm' diretamente para a raiz
                    var childFilter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("parent", strmId),
                        Builders<BsonDocument>.Filter.Eq("parent", strmId.ToString()),
                        Builders<BsonDocument>.Filter.Eq("parent", "strm"),
                        Builders<BsonDocument>.Filter.Eq("parent", "/strm"),
                        Builders<BsonDocument>.Filter.Eq("parent", "/raphael/strm"));

                    var newParent = parent.IsString && !string.IsNullOrEmpty(parent.AsString) ? parent : BsonNull.Value;
                    // Migrações antigas podem ter criado uma pasta "strm"
                    // dentro da própria pasta "strm". Ela é residual e vazia;
                    // movê-la para a raiz colide com o índice parent+name.
                    var nestedStrmFilter = Builders<BsonDocument>.Filter.And(
                        childFilter,
                        Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression("^strm$", "i")),
                        Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.Eq("is_directory", true),
                            Builders<BsonDocument>.Filter.Eq("type", "dir")));
                    using var nestedCursor = await _filesCollection.FindAsync(nestedStrmFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var nestedStrmDocs = await nestedCursor.ToListAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var nestedStrmDoc in nestedStrmDocs)
                    {
                        var nestedId = nestedStrmDoc.GetValue("_id");
                        var nestedChildrenFilter = Builders<BsonDocument>.Filter.Or(
                            Builders<BsonDocument>.Filter.Eq("parent", nestedId),
                            Builders<BsonDocument>.Filter.Eq("parent", nestedId.ToString()));
                        if (await _filesCollection.CountDocumentsAsync(nestedChildrenFilter, cancellationToken: cancellationToken).ConfigureAwait(false) == 0)
                        {
                            await _filesCollection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", nestedId), cancellationToken).ConfigureAwait(false);
                        }
                    }
                    var update = Builders<BsonDocument>.Update.Set("parent", newParent);
                    await _filesCollection.UpdateManyAsync(childFilter, update, cancellationToken: cancellationToken).ConfigureAwait(false);

                    await _filesCollection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", strmId), cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("[NEBULA-MONGO] Diretório raiz residual 'strm' removido do MongoDB com sucesso.");
                }
            }

            // 2. Garante que a pasta Porno não tenha subpastas (achata todas as mídias diretamente em 'Porno')
            var pornoFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression("^porno$", "i")),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.Eq("is_directory", true),
                    Builders<BsonDocument>.Filter.Eq("type", "dir")));

            using var pornoCursor = await _filesCollection.FindAsync(pornoFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
            var pornoDocs = await pornoCursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var pornoDoc in pornoDocs)
            {
                var pornoId = pornoDoc.GetValue("_id");
                var subDirsFilter = Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("parent", pornoId),
                        Builders<BsonDocument>.Filter.Eq("parent", pornoId.ToString()),
                        Builders<BsonDocument>.Filter.Eq("parent", "porno"),
                        Builders<BsonDocument>.Filter.Eq("parent", "/porno"),
                        Builders<BsonDocument>.Filter.Eq("parent", "/raphael/porno")),
                    Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("is_directory", true),
                        Builders<BsonDocument>.Filter.Eq("type", "dir")));

                using var subDirCursor = await _filesCollection.FindAsync(subDirsFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
                var subDirs = await subDirCursor.ToListAsync(cancellationToken).ConfigureAwait(false);

                foreach (var subDir in subDirs)
                {
                    var subDirId = subDir.GetValue("_id");
                    var moveFilesFilter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("parent", subDirId),
                        Builders<BsonDocument>.Filter.Eq("parent", subDirId.ToString()));
                    var updatePornoParent = Builders<BsonDocument>.Update.Set("parent", pornoId);
                    await _filesCollection.UpdateManyAsync(moveFilesFilter, updatePornoParent, cancellationToken: cancellationToken).ConfigureAwait(false);

                    await _filesCollection.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", subDirId), cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("[NEBULA-MONGO] Subpasta {SubDir} de 'Porno' achatada e removida com sucesso.", subDir.GetValue("name", string.Empty).AsString);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao limpar pasta residual 'strm' ou achatar subpastas de Porno no MongoDB.");
        }
    }

    /// <summary>
    /// Conta o total de arquivos no MongoDB.
    /// </summary>
    public async Task<long> CountFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _filesCollection.CountDocumentsAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-MONGO] Erro ao contar arquivos no MongoDB.");
            return 0;
        }
    }

    /// <summary>
    /// Normaliza o caminho virtual para padrão POSIX.
    /// </summary>
    private static string NormalizePath(string path)
    {
        var normalized = path.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    internal static bool IsRaphaelPath(string path)
        => string.Equals(path, "/raphael", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/raphael/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifica se existe pelo menos um arquivo com payload do Telegram descendente do caminho virtual informado.
    /// Usado para ocultar pastas vazias (sem arquivos publicados) no sistema de arquivos virtual FTP.
    /// </summary>
    /// <param name="virtualPath">Caminho virtual POSIX do diretório (ex.: "/raphael/Series/Dark").</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns><see langword="true"/> se houver ao menos um arquivo com partes do Telegram sob esse caminho.</returns>
    public async Task<bool> HasAnyFileDescendantAsync(string virtualPath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizePath(virtualPath).TrimEnd('/');

        // Variantes do caminho: com e sem o prefixo /raphael
        var paths = new List<string> { norm };
        if (IsRaphaelPath(norm) && norm.Length > "/raphael".Length)
        {
            paths.Add(norm["/raphael".Length..]);
        }
        else if (!IsRaphaelPath(norm) && norm != "/")
        {
            paths.Add($"/raphael{norm}");
        }

        // Filtro de payload: arquivo com partes publicadas no Telegram
        var hasPartsFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Exists("parts", true),
            Builders<BsonDocument>.Filter.Not(Builders<BsonDocument>.Filter.Size("parts", 0)));

        // Para cada variante de caminho, verifica filhos diretos E descendentes via prefixo
        var pathOrFilters = new List<FilterDefinition<BsonDocument>>();
        foreach (var p in paths)
        {
            // Filhos diretos
            pathOrFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", p));
            // Descendentes: parent começa com "{p}/"
            var escapedPrefix = System.Text.RegularExpressions.Regex.Escape(p + "/");
            pathOrFilters.Add(Builders<BsonDocument>.Filter.Regex("parent", new BsonRegularExpression($"^{escapedPrefix}")));
        }

        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Or(pathOrFilters),
            hasPartsFilter);

        var count = await _filesCollection.CountDocumentsAsync(filter, new CountOptions { Limit = 1 }, cancellationToken).ConfigureAwait(false);
        return count > 0;
    }

    /// <summary>
    /// Busca os nós filhos de um determinado pai (ID).
    /// </summary>
    public Task<IReadOnlyList<BsonDocument>> GetChildrenAsync(string? parentId, CancellationToken cancellationToken = default)
        => GetChildrenAsync(parentId, null, cancellationToken);

    /// <summary>
    /// Busca os nós filhos de um determinado pai (ID ou caminho virtual POSIX).
    /// </summary>
    public async Task<IReadOnlyList<BsonDocument>> GetChildrenAsync(string? parentId, string? virtualPath, CancellationToken cancellationToken = default)
    {
        var parentFilters = new List<FilterDefinition<BsonDocument>>();

        if (!string.IsNullOrWhiteSpace(virtualPath))
        {
            var normPath = NormalizePath(virtualPath);
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", normPath));

            // Caso o caminho comece com /{user}, adiciona também versão sem o prefixo
            if (normPath.StartsWith("/raphael/", StringComparison.OrdinalIgnoreCase))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", normPath["/raphael".Length..]));
            }
            else if (!IsRaphaelPath(normPath) && normPath != "/")
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", $"/raphael{normPath}"));
            }

            if (normPath == "/" || normPath.Equals("/raphael", StringComparison.OrdinalIgnoreCase))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/raphael"));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/"));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", BsonNull.Value));
                parentFilters.Add(Builders<BsonDocument>.Filter.Exists("parent", false));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", ""));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "null"));
            }
        }
        else if (string.IsNullOrWhiteSpace(parentId))
        {
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/raphael"));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/"));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", BsonNull.Value));
            parentFilters.Add(Builders<BsonDocument>.Filter.Exists("parent", false));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", ""));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "null"));
        }

        if (!string.IsNullOrWhiteSpace(parentId))
        {
            if (ObjectId.TryParse(parentId, out var pOid))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", pOid));
            }

            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", parentId));
        }

        var filter = parentFilters.Count > 0
            ? Builders<BsonDocument>.Filter.Or(parentFilters)
            : Builders<BsonDocument>.Filter.Empty;

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var list = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

        // Se for a raiz /raphael ou /, não incluir a própria pasta "raphael" como subpasta de si mesma
        if ((virtualPath == "/" || virtualPath?.Equals("/raphael", StringComparison.OrdinalIgnoreCase) == true) && parentId == null)
        {
            list = list.Where(d => !string.Equals(d.GetValue("name", string.Empty).AsString, "raphael", StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return list;
    }

    /// <summary>
    /// Busca um nó por nome e ID pai.
    /// </summary>
    public Task<BsonDocument?> FindByNameAndParentAsync(string name, string? parentId, CancellationToken cancellationToken = default)
        => FindByNameAndParentAsync(name, parentId, null, cancellationToken);

    /// <summary>
    /// Busca um nó por nome e pai (ID ou caminho virtual POSIX).
    /// </summary>
    public async Task<BsonDocument?> FindByNameAndParentAsync(string name, string? parentId, string? virtualPath, CancellationToken cancellationToken = default)
    {
        var nameFilter = Builders<BsonDocument>.Filter.Eq("name", name);
        var parentFilters = new List<FilterDefinition<BsonDocument>>();

        if (!string.IsNullOrWhiteSpace(virtualPath))
        {
            var normPath = NormalizePath(virtualPath);
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", normPath));

            if (normPath.StartsWith("/raphael/", StringComparison.OrdinalIgnoreCase))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", normPath["/raphael".Length..]));
            }
            else if (!IsRaphaelPath(normPath) && normPath != "/")
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", $"/raphael{normPath}"));
            }

            if (normPath == "/" || normPath.Equals("/raphael", StringComparison.OrdinalIgnoreCase))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/raphael"));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/"));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", BsonNull.Value));
                parentFilters.Add(Builders<BsonDocument>.Filter.Exists("parent", false));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", ""));
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "null"));
            }
        }
        else if (string.IsNullOrWhiteSpace(parentId))
        {
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/raphael"));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/"));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", BsonNull.Value));
            parentFilters.Add(Builders<BsonDocument>.Filter.Exists("parent", false));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", ""));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "null"));
        }

        if (!string.IsNullOrWhiteSpace(parentId))
        {
            if (ObjectId.TryParse(parentId, out var pOid))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", pOid));
            }

            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", parentId));
        }

        var parentFilter = parentFilters.Count > 0
            ? Builders<BsonDocument>.Filter.Or(parentFilters)
            : Builders<BsonDocument>.Filter.Empty;

        var filter = Builders<BsonDocument>.Filter.And(nameFilter, parentFilter);
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var matches = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        if (matches.Count <= 1)
        {
            return matches.FirstOrDefault();
        }

        // Se houver mais de um documento com o mesmo nome sob o pai (ex.: duplicações históricas),
        // prefira o documento que possui payload do Telegram ou é um diretório válido.
        return matches
            .OrderByDescending(d =>
                (d.TryGetValue("parts", out var p) && p.IsBsonArray && p.AsBsonArray.Count > 0) ||
                (d.TryGetValue("tg_file_id", out var t) && !string.IsNullOrEmpty(t.AsString)) ||
                d.GetValue("is_directory", false).AsBoolean ||
                d.GetValue("type", string.Empty).AsString == "dir")
            .First();
    }

    /// <summary>
    /// Busca um nó pelo ID.
    /// </summary>
    public async Task<BsonDocument?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var filter = ObjectId.TryParse(id, out var oid)
            ? Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("_id", oid),
                Builders<BsonDocument>.Filter.Eq("_id", id))
            : Builders<BsonDocument>.Filter.Eq("_id", id);

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Busca um arquivo pelo identificador da mensagem publicada no Telegram.
    /// </summary>
    public async Task<BsonDocument?> FindByTelegramMessageAsync(long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tg_chat_id", chatId),
            Builders<BsonDocument>.Filter.Eq("tg_message_id", messageId));
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Remove um nó virtual do catálogo pelo ID.
    /// </summary>
    public async Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return;
        }

        await _filesCollection.DeleteOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", objectId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Busca um usuário pelo login.
    /// </summary>
    public async Task<BsonDocument?> FindUserByLoginAsync(string login, CancellationToken cancellationToken = default)
    {
        // O Nebula original usa documentos com login em "_id" ou no campo
        // "login", dependendo da versão/esquema que criou a conta.
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("_id", login),
            Builders<BsonDocument>.Filter.Eq("login", login));
        using var cursor = await _usersCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Carrega tokens habilitados do armazenamento MongoDB do Nebula.
    /// Aceita os nomes de campo token/bot_token e ordena por index/order.
    /// </summary>
    public async Task<List<string>> GetBotTokensAsync(string? collectionName = null, CancellationToken cancellationToken = default)
    {
        var collection = string.IsNullOrWhiteSpace(collectionName) || collectionName.Equals("bot_tokens", StringComparison.OrdinalIgnoreCase)
            ? _botTokensCollection
            : _database.GetCollection<BsonDocument>(collectionName);

        try
        {
            using var cursor = await collection.FindAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
            var docs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
            return docs
                .OrderBy(d => d.TryGetValue("index", out var index) && index.IsNumeric ? index.ToInt32() : int.MaxValue)
                .ThenBy(d => d.TryGetValue("order", out var order) && order.IsNumeric ? order.ToInt32() : int.MaxValue)
                .Where(d => !d.TryGetValue("enabled", out var enabled) || !enabled.IsBoolean || enabled.AsBoolean)
                .Select(d => d.TryGetValue("token", out var token) ? token : d.GetValue("bot_token", BsonNull.Value))
                .Where(v => v.IsString && !string.IsNullOrWhiteSpace(v.AsString))
                .Select(v => v.AsString.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Não foi possível carregar tokens da coleção {Collection}.", collectionName ?? "bot_tokens");
            return [];
        }
    }

    /// <summary>
    /// Cria ou atualiza um usuário.
    /// </summary>
    public async Task UpsertUserAsync(string login, string passwordHash, string permissions = "elradfmwM", CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", login);
        var update = Builders<BsonDocument>.Update
            .Set("login", login)
            .Set("password_hash", passwordHash)
            .Set("permissions", permissions)
            .SetOnInsert("created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _usersCollection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Insere um documento de arquivo ou diretório na coleção de arquivos.
    /// </summary>
    public async Task InsertFileDocAsync(BsonDocument doc, CancellationToken cancellationToken = default)
    {
        if (doc != null && (!doc.Contains("modified_at") || doc["modified_at"].IsBsonNull))
        {
            doc["modified_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        await _filesCollection.InsertOneAsync(doc!, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Insere um arquivo apenas uma vez, mesmo com chamadas concorrentes.
    /// Retorna false quando outro produtor já registrou o mesmo caminho.
    /// </summary>
    public async Task<bool> InsertFileDocIfAbsentAsync(BsonDocument doc, CancellationToken cancellationToken = default)
    {
        if (doc == null)
        {
            return false;
        }

        if (!doc.Contains("modified_at") || doc["modified_at"].IsBsonNull)
        {
            doc["modified_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        if (!doc.TryGetValue("local_path", out var localPath) || !localPath.IsString || string.IsNullOrWhiteSpace(localPath.AsString))
        {
            await _filesCollection.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }

        doc["queue_identity"] = NormalizeQueueIdentity(localPath.AsString);
        var insertFields = new BsonDocument(doc.Where(static pair => pair.Name != "_id"));
        var result = await _filesCollection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("queue_identity", doc["queue_identity"]),
            new BsonDocumentUpdateDefinition<BsonDocument>(new BsonDocument("$setOnInsert", insertFields)),
            new UpdateOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);

        return result.UpsertedId != null;
    }

    private static string NormalizeQueueIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? fullPath.ToUpperInvariant() : fullPath;
    }

    /// <summary>
    /// Cria ou substitui um arquivo concluído identificado pela mensagem do Telegram.
    /// </summary>
    public async Task UpsertUploadedFileAsync(BsonDocument doc, long chatId, int messageId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("tg_chat_id", chatId),
            Builders<BsonDocument>.Filter.Eq("tg_message_id", messageId));

        await _filesCollection.ReplaceOneAsync(
            filter,
            doc,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Busca um arquivo pelo seu identificador ObjectId.
    /// </summary>
    /// <param name="id">Identificador ObjectId do documento.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Documento encontrado ou null.</returns>
    public async Task<BsonDocument?> FindFileByIdAsync(ObjectId id, CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Filter.Ne("type", "dir"),
            Builders<BsonDocument>.Filter.Eq("status", "completed"));
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Busca um arquivo pelo caminho virtual, nome ou ID de string.
    /// </summary>
    /// <param name="pathOrName">Caminho virtual, nome ou ID do arquivo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Documento encontrado ou null.</returns>
    public async Task<BsonDocument?> FindFileByVirtualPathOrNameAsync(string pathOrName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            return null;
        }

        if (ObjectId.TryParse(pathOrName, out var oid))
        {
            var byId = await FindFileByIdAsync(oid, cancellationToken).ConfigureAwait(false);
            if (byId != null)
            {
                return byId;
            }
        }

        var identifierFilter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("name", pathOrName),
            Builders<BsonDocument>.Filter.Eq("local_path", pathOrName),
            Builders<BsonDocument>.Filter.Eq("tg_file_id", pathOrName),
            Builders<BsonDocument>.Filter.Eq("tg_file", pathOrName));
        var filter = Builders<BsonDocument>.Filter.And(
            identifierFilter,
            Builders<BsonDocument>.Filter.Ne("type", "dir"),
            Builders<BsonDocument>.Filter.Eq("status", "completed"));

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var match = await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (match != null || (!pathOrName.Contains('/', StringComparison.Ordinal) && !pathOrName.Contains('\\', StringComparison.Ordinal)))
        {
            return match;
        }

        var segments = pathOrName.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0 && segments[0].Length == 2 && segments[0][1] == ':')
        {
            segments = segments[1..];
        }

        if (segments.Length == 0)
        {
            return null;
        }

        string? parentId = null;
        var parentPath = "/";
        for (var index = 0; index < segments.Length; index++)
        {
            var node = await FindByNameAndParentAsync(segments[index], parentId, parentPath, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return null;
            }

            var isLast = index == segments.Length - 1;
            if (isLast)
            {
                return string.Equals(node.GetValue("type", string.Empty).AsString, "dir", StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(node.GetValue("status", string.Empty).AsString, "completed", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : node;
            }

            if (!string.Equals(node.GetValue("type", string.Empty).AsString, "dir", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            parentId = node.GetValue("_id").ToString();
            parentPath = NormalizePath(parentPath.TrimEnd('/') + "/" + segments[index]);
        }

        return null;
    }

    /// <summary>
    /// Busca todos os arquivos completados que não são diretórios.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de documentos BSON completados.</returns>
    public async Task<List<BsonDocument>> GetAllCompletedFilesAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Ne("type", "dir"),
            Builders<BsonDocument>.Filter.Eq("status", "completed"),
            NebulaProtectedContent.NotProtected());

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns published files and media that is already staged, queued, or being uploaded.
    /// This is used by the downloader to prevent a second STRM from creating a
    /// competing upload for the same media while the first one is still active.
    /// </summary>
    public async Task<List<BsonDocument>> GetCompletedOrActiveFilesAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Ne("type", "dir"),
            Builders<BsonDocument>.Filter.In(
                "status",
                new[] { "completed", "staging", "queued", "uploading" }),
            NebulaProtectedContent.NotProtected());

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Constrói um mapa de caminhos de diretórios em memória para resolver o caminho completo de qualquer nó de forma ultra rápida.
    /// </summary>
    public async Task<Dictionary<string, string>> BuildDirectoryPathMapAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("type", "dir");
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        var dirs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

        var parentMap = new Dictionary<string, (string Name, string? Parent)>();
        foreach (var dir in dirs)
        {
            var id = dir.GetValue("_id").ToString()!;
            var name = dir.GetValue("name", string.Empty).AsString;
            var parent = dir.Contains("parent") && !dir["parent"].IsBsonNull ? dir["parent"].ToString() : null;
            parentMap[id] = (name, parent);
        }

        var pathMap = new Dictionary<string, string>();
        var resolving = new HashSet<string>(StringComparer.Ordinal);

        string ResolvePath(string dirId)
        {
            if (pathMap.TryGetValue(dirId, out var cached))
            {
                return cached;
            }

            if (!parentMap.TryGetValue(dirId, out var info))
            {
                return string.Empty;
            }

            if (!resolving.Add(dirId))
            {
                _logger.LogWarning("[NEBULA-MONGO] Ciclo detectado na árvore de diretórios no nó {DirectoryId}.", dirId);
                return string.Empty;
            }

            var parentPath = info.Parent != null ? ResolvePath(info.Parent) : string.Empty;
            var fullPath = string.IsNullOrEmpty(parentPath) ? info.Name : $"{parentPath}/{info.Name}";
            pathMap[dirId] = fullPath;
            resolving.Remove(dirId);
            return fullPath;
        }

        foreach (var key in parentMap.Keys)
        {
            ResolvePath(key);
        }

        return pathMap;
    }

    /// <summary>
    /// Limpa ou marca nós inconsistentes no banco de dados.
    /// Capas, imagens, NFO/XML e legendas (conteúdo protegido) nunca são removidos,
    /// nem quando o registro ficou em erro, para não perder informação do cache local.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Quantidade de registros removidos.</returns>
    public async Task<long> PruneCompletedAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("status", "error"),
            NebulaProtectedContent.NotProtected());
        var result = await _filesCollection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount;
    }

    /// <summary>
    /// Obtém todos os documentos da coleção de arquivos para sincronização.
    /// </summary>
    public async Task<List<BsonDocument>> GetAllFilesForSyncAsync(CancellationToken cancellationToken = default)
    {
        using var cursor = await _filesCollection.FindAsync(NebulaProtectedContent.NotProtected(), cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Varre a árvore virtual do Nebula e move grupos identificados como novelas
    /// de Series para Novelas. Somente o campo parent é alterado: IDs, partes do
    /// Telegram, local_path e arquivos físicos permanecem intactos.
    /// </summary>
    public async Task<NebulaNovelaMigrationResult> ScanAndMoveNovelasAsync(CancellationToken cancellationToken = default)
    {
        var result = new NebulaNovelaMigrationResult { Success = false };
        var documents = await GetAllFilesForSyncAsync(cancellationToken).ConfigureAwait(false);
        result.Scanned = documents.Count;

        var byId = documents
            .Where(d => d.Contains("_id"))
            .ToDictionary(d => d.GetValue("_id").ToString(), StringComparer.OrdinalIgnoreCase);

        static string ParentKey(BsonDocument doc)
            => doc.TryGetValue("parent", out var parent) && !parent.IsBsonNull ? parent.ToString() : string.Empty;

        static bool SameParent(BsonDocument left, BsonDocument right)
            => string.Equals(ParentKey(left), ParentKey(right), StringComparison.OrdinalIgnoreCase);

        string ResolvePath(BsonDocument doc)
        {
            var parts = new Stack<string>();
            var current = doc;
            var guard = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (current != null && current.Contains("name") && guard.Add(current.GetValue("_id").ToString()))
            {
                parts.Push(current.GetValue("name", string.Empty).AsString);
                var parent = ParentKey(current);
                if (string.IsNullOrEmpty(parent))
                {
                    break;
                }

                if (!byId.TryGetValue(parent, out current!))
                {
                    // Legacy documents store the virtual parent as a path.
                    var legacy = parent.Replace('\\', '/').Trim('/');
                    if (!string.IsNullOrEmpty(legacy))
                    {
                        foreach (var segment in legacy.Split('/', StringSplitOptions.RemoveEmptyEntries).Reverse())
                        {
                            parts.Push(segment);
                        }
                    }

                    break;
                }
            }

            return string.Join('/', parts);
        }

        static bool IsNovela(BsonDocument doc, string path)
        {
            var values = new[] { path, doc.GetValue("name", string.Empty).AsString }
                .Concat(new[] { "media_type", "category", "media_category", "content_type" }
                    .Select(field => doc.GetValue(field, string.Empty).ToString()));
            return values.Any(value => value.Contains("novela", StringComparison.OrdinalIgnoreCase)
                || value.Contains("telenovela", StringComparison.OrdinalIgnoreCase));
        }

        var seriesRoots = documents.Where(d =>
            d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
            && d.GetValue("name", string.Empty).AsString.Equals("Series", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var series in seriesRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var novelaRoot = documents.FirstOrDefault(d =>
                d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                && d.GetValue("name", string.Empty).AsString.Equals("Novelas", StringComparison.OrdinalIgnoreCase)
                && SameParent(d, series));

            if (novelaRoot == null)
            {
                novelaRoot = new BsonDocument
                {
                    { "_id", ObjectId.GenerateNewId() },
                    { "name", "Novelas" },
                    { "type", "dir" },
                    { "is_directory", true },
                    { "status", "completed" },
                    { "parent", series.GetValue("parent", BsonNull.Value) },
                    { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                };
                await InsertFileDocAsync(novelaRoot, cancellationToken).ConfigureAwait(false);
                documents.Add(novelaRoot);
                byId[novelaRoot.GetValue("_id").ToString()] = novelaRoot;
            }

            var seriesPath = ResolvePath(series);
            var directChildren = documents.Where(d =>
                !d.GetValue("_id").Equals(series.GetValue("_id"))
                && ParentKey(d).Equals(series.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrEmpty(seriesPath) && ParentKey(d).TrimEnd('/').Equals(seriesPath, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in directChildren)
            {
                var childPath = ResolvePath(child);
                if (IsNovela(child, childPath))
                {
                    candidates.Add(child.GetValue("_id").ToString());
                }
            }

            foreach (var candidateId in candidates)
            {
                var candidate = byId[candidateId];
                var candidateName = candidate.GetValue("name", string.Empty).AsString;

                // Se o candidato for a própria pasta de agrupamento "Novelas" sob "Series",
                // não devemos movê-la como subpasta (o que criaria Novelas/Novelas).
                // Em vez disso, movemos todos os seus filhos diretamente para novelaRoot
                // e excluímos o nó duplicado da pasta Novelas sob Series.
                if (candidate.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                    && candidateName.Equals("Novelas", StringComparison.OrdinalIgnoreCase))
                {
                    var childrenOfDuplicate = documents.Where(d =>
                        !d.GetValue("_id").Equals(candidate.GetValue("_id"))
                        && ParentKey(d).Equals(candidate.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var childOfDup in childrenOfDuplicate)
                    {
                        var updateChild = Builders<BsonDocument>.Update
                            .Set("parent", novelaRoot.GetValue("_id"))
                            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        var updRes = await _filesCollection.UpdateOneAsync(
                            Builders<BsonDocument>.Filter.Eq("_id", childOfDup.GetValue("_id")), updateChild, new UpdateOptions(), cancellationToken).ConfigureAwait(false);
                        if (updRes.ModifiedCount > 0)
                        {
                            result.Moved++;
                        }
                    }

                    // Corrige caminhos legados que continham .../Series/Novelas/... -> .../Novelas/...
                    var dupOldPrefix = $"{seriesPath}/{candidateName}".Trim('/');
                    var dupNewPrefix = ResolvePath(novelaRoot).Trim('/');
                    foreach (var descendant in documents.Where(d => ParentKey(d).Replace('\\', '/').Trim('/').StartsWith(dupOldPrefix + "/", StringComparison.OrdinalIgnoreCase)))
                    {
                        await _filesCollection.UpdateOneAsync(
                            Builders<BsonDocument>.Filter.Eq("_id", descendant.GetValue("_id")),
                            Builders<BsonDocument>.Update.Set("parent", ParentKey(descendant).Replace('\\', '/').Trim('/')[dupOldPrefix.Length..].Insert(0, dupNewPrefix)),
                            new UpdateOptions(),
                            cancellationToken).ConfigureAwait(false);
                    }

                    // Remove a pasta duplicada Novelas sob Series
                    await _filesCollection.DeleteOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", candidate.GetValue("_id")), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var update = Builders<BsonDocument>.Update
                    .Set("parent", novelaRoot.GetValue("_id"))
                    .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                var updateResult = await _filesCollection.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", candidate.GetValue("_id")), update, new UpdateOptions(), cancellationToken).ConfigureAwait(false);
                if (updateResult.ModifiedCount > 0)
                {
                    result.Moved++;
                }

                // Legacy descendants carry virtual parent strings instead of ObjectId.
                var oldPrefix = $"{seriesPath}/{candidate.GetValue("name", string.Empty).AsString}".Trim('/');
                var newPrefix = $"{ResolvePath(novelaRoot)}/{candidate.GetValue("name", string.Empty).AsString}".Trim('/');
                foreach (var descendant in documents.Where(d => ParentKey(d).Replace('\\', '/').Trim('/').StartsWith(oldPrefix + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    await _filesCollection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", descendant.GetValue("_id")),
                        Builders<BsonDocument>.Update.Set("parent", ParentKey(descendant).Replace('\\', '/').Trim('/')[oldPrefix.Length..].Insert(0, newPrefix)),
                        new UpdateOptions(),
                        cancellationToken).ConfigureAwait(false);
                }
            }

            // Normaliza caso já existisse uma pasta Novelas/Novelas sob a raiz de novelas
            var dupUnderNovela = documents.FirstOrDefault(d =>
                d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                && d.GetValue("name", string.Empty).AsString.Equals("Novelas", StringComparison.OrdinalIgnoreCase)
                && ParentKey(d).Equals(novelaRoot.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase));
            if (dupUnderNovela != null)
            {
                var childrenOfDup = documents.Where(d =>
                    !d.GetValue("_id").Equals(dupUnderNovela.GetValue("_id"))
                    && ParentKey(d).Equals(dupUnderNovela.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var c in childrenOfDup)
                {
                    await _filesCollection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", c.GetValue("_id")),
                        Builders<BsonDocument>.Update
                            .Set("parent", novelaRoot.GetValue("_id"))
                            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                        new UpdateOptions(),
                        cancellationToken).ConfigureAwait(false);
                    result.Moved++;
                }

                var dupOld = $"{ResolvePath(novelaRoot)}/Novelas".Trim('/');
                var dupNew = ResolvePath(novelaRoot).Trim('/');
                foreach (var descendant in documents.Where(d => ParentKey(d).Replace('\\', '/').Trim('/').StartsWith(dupOld + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    await _filesCollection.UpdateOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", descendant.GetValue("_id")),
                        Builders<BsonDocument>.Update.Set("parent", ParentKey(descendant).Replace('\\', '/').Trim('/')[dupOld.Length..].Insert(0, dupNew)),
                        new UpdateOptions(),
                        cancellationToken).ConfigureAwait(false);
                }

                await _filesCollection.DeleteOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", dupUnderNovela.GetValue("_id")), cancellationToken).ConfigureAwait(false);
            }
        }

        result.AlreadyInNovelas = documents.Count(d => ResolvePath(d).Contains("Novelas", StringComparison.OrdinalIgnoreCase));
        result.Success = true;
        result.Message = $"Varredura concluída: {result.Moved} grupo(s) de novela movido(s) para Novelas.";
        _logger.LogInformation("[NEBULA-MONGO] {Message}", result.Message);
        return result;
    }

    /// <summary>
    /// Remove a categoria legada Series/Animações. O Nebula mantém uma biblioteca
    /// própria em Animações; deixar esse nó dentro de Series faz o Jellyfin expô-lo
    /// como uma série recente no aplicativo.
    /// </summary>
    public async Task<NebulaAnimacaoMigrationResult> NormalizeAnimacoesLibraryAsync(CancellationToken cancellationToken = default)
    {
        var result = new NebulaAnimacaoMigrationResult { Success = false };
        var documents = await GetAllFilesForSyncAsync(cancellationToken).ConfigureAwait(false);

        static string ParentKey(BsonDocument doc)
            => doc.TryGetValue("parent", out var parent) && !parent.IsBsonNull ? parent.ToString() : string.Empty;

        var seriesRoots = documents.Where(d =>
            d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
            && d.GetValue("name", string.Empty).AsString.Equals("Series", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var seriesRoot in seriesRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var seriesParent = ParentKey(seriesRoot);
            var animationRoot = documents.FirstOrDefault(d =>
                d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                && d.GetValue("name", string.Empty).AsString.Equals("Animações", StringComparison.OrdinalIgnoreCase)
                && ParentKey(d).Equals(seriesParent, StringComparison.OrdinalIgnoreCase));

            if (animationRoot == null)
            {
                continue;
            }

            var duplicate = documents.FirstOrDefault(d =>
                d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                && d.GetValue("name", string.Empty).AsString.Equals("Animações", StringComparison.OrdinalIgnoreCase)
                && ParentKey(d).Equals(seriesRoot.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase));

            if (duplicate == null)
            {
                continue;
            }

            var duplicateId = duplicate.GetValue("_id");
            var children = documents.Where(d => ParentKey(d).Equals(duplicateId.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var child in children)
            {
                var update = Builders<BsonDocument>.Update
                    .Set("parent", animationRoot.GetValue("_id"))
                    .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                var updateResult = await _filesCollection.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", child.GetValue("_id")),
                    update,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                if (updateResult.ModifiedCount > 0)
                {
                    result.Moved++;
                }
            }

            var duplicateFilter = Builders<BsonDocument>.Filter.Eq("_id", duplicateId);
            var deleteResult = await _filesCollection.DeleteOneAsync(duplicateFilter, cancellationToken).ConfigureAwait(false);
            result.DuplicateRemoved |= deleteResult.DeletedCount > 0;
        }

        result.Success = true;
        result.Message = result.Moved == 0 && !result.DuplicateRemoved
            ? "Nenhum nó duplicado Series/Animações encontrado."
            : $"Biblioteca normalizada: {result.Moved} grupo(s) movido(s) para Animações e nó duplicado removido: {result.DuplicateRemoved}.";
        _logger.LogInformation("[NEBULA-ANIMACOES] {Message}", result.Message);
        return result;
    }

    /// <summary>
    /// Normaliza pastas duplicadas onde uma raiz de categoria aparece dentro de si mesma
    /// (ex.: Series/Series, Filmes/Filmes). Promove os nós filhos para a raiz correta e
    /// remove a pasta duplicada intermediária.
    /// </summary>
    public async Task NormalizeDuplicateCategoryRootsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var documents = await GetAllFilesForSyncAsync(cancellationToken).ConfigureAwait(false);
            static string ParentKey(BsonDocument doc)
                => doc.TryGetValue("parent", out var parent) && !parent.IsBsonNull ? parent.ToString() : string.Empty;

            var targetCategories = new[] { "Series", "Filmes", "Novelas", "Animações", "Doramas" };

            foreach (var cat in targetCategories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var catRoots = documents.Where(d =>
                    d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                    && d.GetValue("name", string.Empty).AsString.Equals(cat, StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrEmpty(ParentKey(d)) || ParentKey(d) == "/" || ParentKey(d).Equals("/raphael", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                foreach (var root in catRoots)
                {
                    var rootId = root.GetValue("_id").ToString();
                    var duplicate = documents.FirstOrDefault(d =>
                        d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                        && d.GetValue("name", string.Empty).AsString.Equals(cat, StringComparison.OrdinalIgnoreCase)
                        && ParentKey(d).Equals(rootId, StringComparison.OrdinalIgnoreCase));

                    if (duplicate == null)
                    {
                        continue;
                    }

                    var dupId = duplicate.GetValue("_id");
                    var children = documents.Where(d => ParentKey(d).Equals(dupId.ToString(), StringComparison.OrdinalIgnoreCase)).ToList();
                    foreach (var child in children)
                    {
                        var update = Builders<BsonDocument>.Update
                            .Set("parent", root.GetValue("_id"))
                            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                        await _filesCollection.UpdateOneAsync(
                            Builders<BsonDocument>.Filter.Eq("_id", child.GetValue("_id")), update, new UpdateOptions(), cancellationToken).ConfigureAwait(false);
                    }

                    // Ajusta caminhos virtuais legados: .../{cat}/{cat}/ -> .../{cat}/
                    var oldSub = $"{cat}/{cat}";
                    foreach (var descendant in documents.Where(d => ParentKey(d).Replace('\\', '/').Trim('/').Contains(oldSub, StringComparison.OrdinalIgnoreCase)))
                    {
                        var currentParent = ParentKey(descendant).Replace('\\', '/');
                        var updatedParent = System.Text.RegularExpressions.Regex.Replace(
                            currentParent,
                            $@"(?i)(/|^){System.Text.RegularExpressions.Regex.Escape(cat)}/{System.Text.RegularExpressions.Regex.Escape(cat)}(/|$)",
                            $"$1{cat}$2");
                        if (!string.Equals(currentParent, updatedParent, StringComparison.OrdinalIgnoreCase))
                        {
                            await _filesCollection.UpdateOneAsync(
                                Builders<BsonDocument>.Filter.Eq("_id", descendant.GetValue("_id")),
                                Builders<BsonDocument>.Update.Set("parent", updatedParent),
                                new UpdateOptions(),
                                cancellationToken).ConfigureAwait(false);
                        }
                    }

                    await _filesCollection.DeleteOneAsync(
                        Builders<BsonDocument>.Filter.Eq("_id", dupId), cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("[NEBULA-MONGO] Pasta duplicada {Category}/{Category} eliminada e nós promovidos.", cat, cat);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao normalizar raízes de categorias duplicadas.");
        }
    }

    /// <summary>
    /// Obtém o delta de sincronização do catálogo: documentos criados ou modificados
    /// a partir de uma data UTC, mais os arquivos que ainda não foram enviados
    /// (fila, staging, envio em andamento ou falha). A coleção inteira nunca é devolvida.
    /// </summary>
    /// <param name="sinceUtc">Data UTC de corte.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Documentos alterados e pendentes.</returns>
    public async Task<List<BsonDocument>> GetSyncDeltaAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var sinceEpoch = new DateTimeOffset(sinceUtc.ToUniversalTime()).ToUnixTimeSeconds();
        var minOid = ObjectId.GenerateNewId(sinceUtc.ToUniversalTime());

        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Gte("modified_at", sinceEpoch),
            Builders<BsonDocument>.Filter.Gte("uploaded_at", sinceEpoch),
            Builders<BsonDocument>.Filter.Gte("_id", minOid),
            Builders<BsonDocument>.Filter.In("status", NotUploadedStatuses));
        filter = Builders<BsonDocument>.Filter.And(filter, NebulaProtectedContent.NotProtected());

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém o timestamp UTC mais recente registrado na coleção local de arquivos.
    /// </summary>
    public async Task<DateTime?> GetLatestFileTimestampAsync(CancellationToken cancellationToken = default)
    {
        DateTime? latest = null;

        try
        {
            var filterMod = Builders<BsonDocument>.Filter.Exists("modified_at");
            var sortMod = Builders<BsonDocument>.Sort.Descending("modified_at");
            var docMod = await _filesCollection.Find(filterMod).Sort(sortMod).Limit(1).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (docMod != null && docMod.Contains("modified_at") && docMod["modified_at"].IsNumeric)
            {
                var epoch = docMod["modified_at"].ToInt64();
                if (epoch > 0)
                {
                    latest = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                }
            }

            var filterUp = Builders<BsonDocument>.Filter.Exists("uploaded_at");
            var sortUp = Builders<BsonDocument>.Sort.Descending("uploaded_at");
            var docUp = await _filesCollection.Find(filterUp).Sort(sortUp).Limit(1).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (docUp != null && docUp.Contains("uploaded_at") && docUp["uploaded_at"].IsNumeric)
            {
                var epoch = docUp["uploaded_at"].ToInt64();
                if (epoch > 0)
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                    if (latest == null || dt > latest.Value)
                    {
                        latest = dt;
                    }
                }
            }

            var sortId = Builders<BsonDocument>.Sort.Descending("_id");
            var docId = await _filesCollection.Find(Builders<BsonDocument>.Filter.Empty).Sort(sortId).Limit(1).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (docId != null && docId.Contains("_id") && docId["_id"].IsObjectId)
            {
                var dt = docId["_id"].AsObjectId.CreationTime;
                if (latest == null || dt > latest.Value)
                {
                    latest = dt;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao obter timestamp mais recente dos arquivos.");
        }

        return latest;
    }

    /// <summary>
    /// Obtém todos os documentos da coleção de usuários para sincronização.
    /// </summary>
    public async Task<List<BsonDocument>> GetAllUsersForSyncAsync(CancellationToken cancellationToken = default)
    {
        using var cursor = await _usersCollection.FindAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém a quantidade de usuários locais sem carregar os documentos inteiros.
    /// </summary>
    public Task<long> CountUsersAsync(CancellationToken cancellationToken = default)
    {
        return _usersCollection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Empty,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Busca somente os arquivos que estão sendo enviados neste momento.
    /// </summary>
    public async Task<List<BsonDocument>> GetActiveUploadsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("status", "uploading");
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Recoloca na fila os uploads que ficaram marcados como ativos após uma
    /// interrupção do servidor. As partes já publicadas permanecem no documento
    /// para que o motor retome o arquivo sem reenviar o que já foi concluído.
    /// </summary>
    public async Task<long> RequeueInterruptedUploadsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("status", "uploading");
        var update = Builders<BsonDocument>.Update
            .Set("status", "queued")
            .Unset("worker_id")
            .Unset("started_at")
            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var result = await _filesCollection.UpdateManyAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.ModifiedCount;
    }

    public async Task<List<BsonDocument>> GetQueuedUploadsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("status", "queued");
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Obtém todos os uploads que ainda não estão concluídos e aguardam processamento.
    /// O estado staging também é pendente: ele pode ser criado pelo feeder antes de
    /// o worker conseguir promovê-lo para queued.
    /// </summary>
    public async Task<List<BsonDocument>> GetPendingUploadsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.In("status", new[] { "staging", "queued" });
        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Faz upsert de um documento bruto restaurado do Supabase para o MongoDB.
    /// </summary>
    /// <param name="doc">Documento BSON a ser inserido ou atualizado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task UpsertRawDocAsync(BsonDocument doc, CancellationToken cancellationToken = default)
    {
        if (doc == null || !doc.Contains("_id"))
        {
            return;
        }

        // Normaliza _id para ObjectId se for uma string hexadecimal de 24 caracteres
        if (doc["_id"].IsString && ObjectId.TryParse(doc["_id"].AsString, out var docOid))
        {
            doc["_id"] = docOid;
        }

        // Normaliza parent para ObjectId se for string de 24 caracteres
        if (doc.Contains("parent") && doc["parent"].IsString && ObjectId.TryParse(doc["parent"].AsString, out var parentOid))
        {
            doc["parent"] = parentOid;
        }

        var idVal = doc["_id"];
        var filter = Builders<BsonDocument>.Filter.Eq("_id", idVal);
        await _filesCollection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Mescla documentos de arquivo vindos do Supabase no MongoDB (restauração).
    /// A operação é um <c>$set</c> campo a campo: o que existe apenas no cache local
    /// (capas, imagens, metadados, partes do Telegram, caminho local) é preservado,
    /// nunca substituído por um documento remoto mais pobre.
    /// </summary>
    /// <param name="docs">Documentos BSON recuperados do Supabase.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Quantidade de documentos mesclados.</returns>
    public async Task<int> BulkUpsertRawDocsAsync(IReadOnlyCollection<BsonDocument> docs, CancellationToken cancellationToken = default)
    {
        if (docs == null || docs.Count == 0)
        {
            return 0;
        }

        var writes = new List<WriteModel<BsonDocument>>(docs.Count);
        foreach (var doc in docs)
        {
            if (doc == null || !doc.Contains("_id"))
            {
                continue;
            }

            if (doc["_id"].IsString && ObjectId.TryParse(doc["_id"].AsString, out var docOid))
            {
                doc["_id"] = docOid;
            }

            if (doc.Contains("parent") && doc["parent"].IsString && ObjectId.TryParse(doc["parent"].AsString, out var parentOid))
            {
                doc["parent"] = parentOid;
            }

            var fields = new BsonDocument();
            foreach (var element in doc.Elements)
            {
                if (!string.Equals(element.Name, "_id", StringComparison.Ordinal))
                {
                    fields[element.Name] = element.Value;
                }
            }

            if (fields.ElementCount == 0)
            {
                continue;
            }

            writes.Add(new UpdateOneModel<BsonDocument>(
                Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]),
                new BsonDocument("$set", fields))
            {
                IsUpsert = true
            });
        }

        if (writes.Count == 0)
        {
            return 0;
        }

        await _filesCollection.BulkWriteAsync(
            writes,
            new BulkWriteOptions { IsOrdered = false },
            cancellationToken).ConfigureAwait(false);
        return writes.Count;
    }

    /// <summary>
    /// Faz upsert de um documento de usuário bruto restaurado do Supabase para o MongoDB.
    /// </summary>
    /// <param name="doc">Documento BSON do usuário.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task UpsertRawUserDocAsync(BsonDocument doc, CancellationToken cancellationToken = default)
    {
        if (doc == null || !doc.Contains("_id"))
        {
            return;
        }

        var idVal = doc["_id"];
        var filter = Builders<BsonDocument>.Filter.Eq("_id", idVal);
        await _usersCollection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Faz upsert de um documento de token de bot no MongoDB.
    /// </summary>
    /// <param name="doc">Documento BSON do token de bot.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task UpsertRawTokenDocAsync(BsonDocument doc, CancellationToken cancellationToken = default)
    {
        if (doc == null)
        {
            return;
        }

        if (doc.Contains("token") && doc["token"].IsString)
        {
            var filter = Builders<BsonDocument>.Filter.Eq("token", doc["token"].AsString);
            await _botTokensCollection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        }
        else if (doc.Contains("_id"))
        {
            var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
            await _botTokensCollection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Busca documento de arquivo existente para envio ou retomada de upload.
    /// </summary>
    /// <param name="name">Nome do arquivo.</param>
    /// <param name="parentId">ID da pasta pai.</param>
    /// <param name="localFilePath">Caminho local do arquivo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Documento encontrado ou null.</returns>
    public async Task<BsonDocument?> FindFileForUploadAsync(string name, string? parentId, string? localFilePath = null, CancellationToken cancellationToken = default)
    {
        var parentFilters = new List<FilterDefinition<BsonDocument>>();
        if (string.IsNullOrWhiteSpace(parentId))
        {
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", BsonNull.Value));
            parentFilters.Add(Builders<BsonDocument>.Filter.Exists("parent", false));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", string.Empty));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/"));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "/raphael"));
            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", "null"));
        }
        else
        {
            if (ObjectId.TryParse(parentId, out var pOid))
            {
                parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", pOid));
            }

            parentFilters.Add(Builders<BsonDocument>.Filter.Eq("parent", parentId));
        }

        var filterByParent = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("name", name),
            Builders<BsonDocument>.Filter.Or(parentFilters));

        using (var cursor = await _filesCollection.FindAsync(filterByParent, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var match = await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (match != null)
            {
                return match;
            }
        }

        if (!string.IsNullOrWhiteSpace(localFilePath))
        {
            var filterByPath = Builders<BsonDocument>.Filter.Eq("local_path", localFilePath);
            using var cursor = await _filesCollection.FindAsync(filterByPath, cancellationToken: cancellationToken).ConfigureAwait(false);
            var match = await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>
    /// Verifica se uma mídia já foi enviada e concluída no Telegram (status='completed' com partes enviadas).
    /// Suporta correspondência por nome exato, stem, identidade de filme (Título + Ano) e episódio (Série + Temporada + Episódio).
    /// </summary>
    public async Task<BsonDocument?> FindCompletedMediaAsync(
        string fileName,
        string? localFilePath = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var dirName = !string.IsNullOrWhiteSpace(localFilePath)
            ? Path.GetFileName(Path.GetDirectoryName(localFilePath) ?? string.Empty)
            : string.Empty;
        var episodeIdent = NebulaDownloaderEngine.EpisodeIdentity(dirName, fileName);

        // 1. Busca direta por nome exato ou stem em arquivos concluídos
        var filterByName = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Ne("type", "dir"),
            Builders<BsonDocument>.Filter.Eq("status", "completed"),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Eq("name", fileName),
                Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression($"^{Regex.Escape(fileName)}$", "i")),
                Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression($"^{Regex.Escape(stem)}\\.[a-zA-Z0-9]+$", "i"))));

        using (var cursor = await _filesCollection.FindAsync(filterByName, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            var matches = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var match in matches)
            {
                if (HasTelegramParts(match))
                {
                    return match;
                }
            }
        }

        // 2. Busca por Identidade de Filme (Título normalizado + Ano)
        var movieIdent = !episodeIdent.HasValue
            ? NebulaDownloaderEngine.MovieIdentity(stem) ?? NebulaDownloaderEngine.MovieIdentity(dirName)
            : null;
        if (movieIdent.HasValue)
        {
            var yearFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Ne("type", "dir"),
                Builders<BsonDocument>.Filter.Eq("status", "completed"),
                Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression(movieIdent.Value.Year.ToString(CultureInfo.InvariantCulture))));

            using var cursor = await _filesCollection.FindAsync(yearFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
            var candidates = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in candidates)
            {
                var docName = doc.GetValue("name", string.Empty).AsString;
                var docStem = Path.GetFileNameWithoutExtension(docName);
                var docIdent = NebulaDownloaderEngine.MovieIdentity(docStem);
                if (docIdent.HasValue &&
                    docIdent.Value.Year == movieIdent.Value.Year &&
                    string.Equals(docIdent.Value.Title, movieIdent.Value.Title, StringComparison.OrdinalIgnoreCase) &&
                    HasTelegramParts(doc))
                {
                    return doc;
                }
            }
        }

        // 3. Busca por Identidade de Episódio de Série
        var epIdent = NebulaDownloaderEngine.EpisodeIdentity(dirName, fileName);
        if (epIdent.HasValue)
        {
            var sStr = epIdent.Value.Season.ToString("00", CultureInfo.InvariantCulture);
            var eStr = epIdent.Value.Episode.ToString("00", CultureInfo.InvariantCulture);
            var epPattern = $"(?i)s{sStr}e{eStr}|{epIdent.Value.Season}x{eStr}";
            var epFilter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Ne("type", "dir"),
                Builders<BsonDocument>.Filter.Eq("status", "completed"),
                Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression(epPattern)));

            using var cursor = await _filesCollection.FindAsync(epFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
            var candidates = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
            foreach (var doc in candidates)
            {
                var docName = doc.GetValue("name", string.Empty).AsString;
                var docIdent = NebulaDownloaderEngine.EpisodeIdentity(string.Empty, docName);
                if (docIdent.HasValue &&
                    docIdent.Value.Season == epIdent.Value.Season &&
                    docIdent.Value.Episode == epIdent.Value.Episode &&
                    string.Equals(docIdent.Value.Series, epIdent.Value.Series, StringComparison.OrdinalIgnoreCase) &&
                    HasTelegramParts(doc))
                {
                    return doc;
                }
            }
        }

        // 4. Busca por sidecar genérico de Série (tvshow.nfo, season.nfo, poster, etc. em pasta de série)
        if (!string.IsNullOrWhiteSpace(dirName))
        {
            var isGenericSeriesSidecar = string.Equals(stem, "tvshow", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(stem, "season", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(stem, "poster", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(stem, "fanart", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(stem, "banner", StringComparison.OrdinalIgnoreCase);

            if (isGenericSeriesSidecar)
            {
                var seriesName = dirName.StartsWith("Season", StringComparison.OrdinalIgnoreCase) ||
                                 dirName.StartsWith("Temporada", StringComparison.OrdinalIgnoreCase) ||
                                 dirName.StartsWith("Specials", StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(localFilePath) ?? string.Empty) ?? string.Empty)
                    : dirName;

                if (!string.IsNullOrWhiteSpace(seriesName))
                {
                    var seriesFilter = Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Ne("type", "dir"),
                        Builders<BsonDocument>.Filter.Eq("status", "completed"),
                        Builders<BsonDocument>.Filter.Regex("name", new BsonRegularExpression($"^{Regex.Escape(seriesName)}[\\. _-]", "i")));

                    using var cursor = await _filesCollection.FindAsync(seriesFilter, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var matches = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
                    foreach (var match in matches)
                    {
                        if (HasTelegramParts(match))
                        {
                            return match;
                        }
                    }
                }
            }
        }

        return null;
    }

    internal static bool HasTelegramParts(BsonDocument doc)
    {
        if (doc.TryGetValue("parts", out var partsVal) && partsVal.IsBsonArray && partsVal.AsBsonArray.Count > 0)
        {
            return partsVal.AsBsonArray.Any(p => p is BsonDocument pDoc &&
                (!string.IsNullOrWhiteSpace(pDoc.GetValue("tg_file_id", string.Empty).AsString) ||
                 !string.IsNullOrWhiteSpace(pDoc.GetValue("tg_file", string.Empty).AsString)));
        }

        return !string.IsNullOrWhiteSpace(doc.GetValue("tg_file_id", string.Empty).AsString) ||
               !string.IsNullOrWhiteSpace(doc.GetValue("file_id", string.Empty).AsString);
    }

    /// <summary>
    /// Garante que a estrutura de diretórios relativos informada existe no MongoDB e retorna o ID da pasta pai folha.
    /// </summary>
    /// <param name="relDir">Diretório relativo.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>ID da pasta pai.</returns>
    public Task<string?> EnsureDirectoryStructureAsync(string? relDir, CancellationToken cancellationToken = default)
        => EnsureDirectoryStructureAsync(relDir, null, cancellationToken);

    /// <summary>
    /// Garante que a estrutura de diretórios relativos informada existe no MongoDB e retorna o ID da pasta pai folha.
    /// </summary>
    /// <param name="relDir">Diretório relativo.</param>
    /// <param name="onDirectoryCreated">Callback opcional acionado quando um diretório é criado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>ID da pasta pai.</returns>
    public async Task<string?> EnsureDirectoryStructureAsync(
        string? relDir,
        Func<BsonDocument, Task>? onDirectoryCreated,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relDir))
        {
            return null;
        }

        var rawSegments = relDir.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        var segments = rawSegments
            .Where((s, index) => !(index == 0 && string.Equals(s, "strm", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (segments.Length == 0)
        {
            return null;
        }

        string? currentParent = null;
        string currentVirtualPath = string.Empty;

        foreach (var segment in segments)
        {
            var parentVirtualPath = string.IsNullOrEmpty(currentVirtualPath) ? "/" : $"/{currentVirtualPath}";
            var existing = await FindByNameAndParentAsync(segment, currentParent, parentVirtualPath, cancellationToken).ConfigureAwait(false);
            if (existing != null)
            {
                currentParent = existing.GetValue("_id").ToString();
            }
            else
            {
                var dirId = ObjectId.GenerateNewId();
                var dirDoc = new BsonDocument
                {
                    { "_id", dirId },
                    { "name", segment },
                    { "type", "dir" },
                    { "is_directory", true },
                    { "status", "completed" },
                    { "parent", string.IsNullOrEmpty(currentParent) ? BsonNull.Value : (ObjectId.TryParse(currentParent, out var cpOid) ? (BsonValue)cpOid : currentParent) },
                    { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                    { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                };

                await InsertFileDocAsync(dirDoc, cancellationToken).ConfigureAwait(false);

                if (onDirectoryCreated != null)
                {
                    try
                    {
                        await onDirectoryCreated(dirDoc).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-MONGO] Aviso ao invocar callback de diretório criado.");
                    }
                }

                currentParent = dirId.ToString();
            }

            currentVirtualPath = string.IsNullOrEmpty(currentVirtualPath) ? segment : $"{currentVirtualPath}/{segment}";
        }

        return currentParent;
    }

    /// <summary>
    /// Atualiza o progresso em tempo real do upload gravando partes enviadas no MongoDB.
    /// </summary>
    /// <param name="id">ID do arquivo.</param>
    /// <param name="parts">Partes enviadas.</param>
    /// <param name="uploadedBytes">Total de bytes enviados.</param>
    /// <param name="lastBotIndex">Último bot usado.</param>
    /// <param name="workerId">Identificador do worker de envio.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Verdadeiro quando o worker ainda possui o upload; falso quando outro worker assumiu a posse.</returns>
    public async Task<bool> UpdateUploadProgressAsync(
        ObjectId id,
        BsonArray parts,
        long uploadedBytes,
        int lastBotIndex,
        string workerId = "1",
        CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            BuildWorkerOwnershipFilter(workerId));
        var update = Builders<BsonDocument>.Update
            .Set("status", "uploading")
            .Set("parts", parts)
            .Set("uploaded_bytes", uploadedBytes)
            .Set("last_bot_index", lastBotIndex)
            .Set("bot_index", lastBotIndex + 1)
            .Set("worker_id", workerId)
            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var result = await _filesCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount > 0;
    }

    /// <summary>
    /// Marca uma tentativa de upload como falha para que o scanner possa
    /// reencaminhá-la, em vez de deixá-la permanentemente em <c>uploading</c>.
    /// </summary>
    /// <param name="id">ID do arquivo.</param>
    /// <param name="reason">Motivo da falha.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <param name="workerId">Worker que deve possuir o arquivo; nulo desativa a verificação.</param>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task MarkUploadFailedAsync(
        ObjectId id,
        string reason,
        CancellationToken cancellationToken = default,
        string? workerId = null)
    {
        var retryAt = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            BuildWorkerOwnershipFilter(workerId));
        var update = Builders<BsonDocument>.Update
            .Set("status", "failed")
            .Set("failed_reason", reason.Length > 1000 ? reason[..1000] : reason)
            .Set("retry_after", retryAt)
            .Inc("retry_count", 1)
            .Unset("worker_id")
            .Unset("started_at")
            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        await _filesCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Finaliza o upload de um arquivo marcando-o como completed.
    /// </summary>
    /// <param name="id">ID do arquivo.</param>
    /// <param name="finalFields">Campos finais do documento.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <param name="workerId">Worker que deve possuir o arquivo; nulo desativa a verificação.</param>
    /// <returns>Verdadeiro quando o worker ainda possui o upload e a conclusão foi gravada.</returns>
    public async Task<bool> CompleteFileUploadAsync(
        ObjectId id,
        BsonDocument finalFields,
        CancellationToken cancellationToken = default,
        string? workerId = null)
    {
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            BuildWorkerOwnershipFilter(workerId));
        var update = Builders<BsonDocument>.Update.Combine(
            new BsonDocument("$set", finalFields),
            Builders<BsonDocument>.Update.Unset("retry_after"));
        var result = await _filesCollection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.MatchedCount > 0;
    }

    /// <summary>
    /// Finaliza um registro reivindicado quando a mídia equivalente já existe
    /// concluída em outro registro canônico.
    /// </summary>
    public async Task<bool> CompleteDuplicateUploadAsync(
        ObjectId id,
        BsonDocument completedMedia,
        long uploadedBytes,
        CancellationToken cancellationToken = default)
    {
        var update = Builders<BsonDocument>.Update
            .Set("status", "completed")
            .Set("uploaded_bytes", uploadedBytes)
            .Set("completed_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .Unset("worker_id")
            .Unset("started_at")
            .Unset("retry_after");

        if (completedMedia.TryGetValue("parts", out var parts))
        {
            update = update.Set("parts", parts);
        }

        if (completedMedia.TryGetValue("tg_file_id", out var telegramFileId))
        {
            update = update.Set("tg_file_id", telegramFileId);
        }

        var result = await _filesCollection.UpdateOneAsync(
            Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Filter.Ne("status", "completed")),
            update,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result.ModifiedCount > 0;
    }

    private static FilterDefinition<BsonDocument> BuildWorkerOwnershipFilter(string? workerId)
    {
        if (string.IsNullOrWhiteSpace(workerId))
        {
            return Builders<BsonDocument>.Filter.Empty;
        }

        // A document without an owner is allowed to acquire one exactly once;
        // once claimed, all later writes must come from that same worker.
        var ownerFilters = new List<FilterDefinition<BsonDocument>>
        {
            Builders<BsonDocument>.Filter.Eq("worker_id", workerId),
            Builders<BsonDocument>.Filter.Exists("worker_id", false),
            Builders<BsonDocument>.Filter.Eq("worker_id", string.Empty)
        };

        // Older documents may have persisted the worker id as an integer.
        if (int.TryParse(workerId, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var numericWorkerId))
        {
            ownerFilters.Add(Builders<BsonDocument>.Filter.Eq("worker_id", numericWorkerId));
        }

        return Builders<BsonDocument>.Filter.Or(ownerFilters);
    }

    private static FilterDefinition<BsonDocument> BuildRetryReadyFilter(long now)
    {
        return Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Exists("retry_after", false),
            Builders<BsonDocument>.Filter.Lte("retry_after", now));
    }

    /// <summary>
    /// Obtém todos os arquivos com upload ativo ou pendente no MongoDB (uploading, queued, staging, falhas prontas para retry ou com partes parciais não concluídas).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de documentos BSON ordenados por mtime/ctime/progresso.</returns>
    public async Task<List<BsonDocument>> GetActiveOrPendingUploadsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var retryReady = BuildRetryReadyFilter(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", "file"),
                Builders<BsonDocument>.Filter.Or(
                    Builders<BsonDocument>.Filter.In("status", new[] { "uploading", "queued", "staging" }),
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq("status", "failed"),
                        retryReady),
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Exists("parts"),
                        Builders<BsonDocument>.Filter.Ne("parts", new BsonArray()),
                        Builders<BsonDocument>.Filter.Ne("status", "completed"))));

            var sort = Builders<BsonDocument>.Sort
                .Ascending("queued_at")
                .Ascending("created_at")
                .Ascending("mtime")
                .Ascending("ctime")
                .Ascending("_id");

            using var cursor = await _filesCollection.FindAsync(filter, new FindOptions<BsonDocument> { Sort = sort }, cancellationToken).ConfigureAwait(false);
            return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[NEBULA-MONGO] Erro ao buscar uploads pendentes no MongoDB.");
            return [];
        }
    }

    /// <summary>
    /// Tenta resolver o caminho físico local de um arquivo no disco a partir dos diretórios de staging configurados.
    /// </summary>
    /// <param name="doc">Documento do arquivo no MongoDB.</param>
    /// <param name="stagingDirs">Diretórios de staging configurados.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Caminho absoluto do arquivo no disco ou null se não encontrado.</returns>
    public async Task<string?> ResolveLocalPathAsync(BsonDocument doc, IEnumerable<string> stagingDirs, CancellationToken cancellationToken = default)
    {
        var validStageRoots = new List<string>();
        foreach (var stageRoot in stagingDirs)
        {
            if (string.IsNullOrWhiteSpace(stageRoot))
            {
                continue;
            }

            try
            {
                var fullRoot = Path.GetFullPath(stageRoot);
                if (Directory.Exists(fullRoot) && !validStageRoots.Contains(fullRoot, StringComparer.OrdinalIgnoreCase))
                {
                    validStageRoots.Add(fullRoot);
                }
            }
            catch (ArgumentException)
            {
                _logger.LogWarning("[NEBULA-MONGO] Diretório de staging inválido ignorado: '{Path}'.", stageRoot);
            }
            catch (IOException)
            {
                _logger.LogWarning("[NEBULA-MONGO] Diretório de staging inacessível ignorado: '{Path}'.", stageRoot);
            }
        }

        if (validStageRoots.Count == 0)
        {
            return null;
        }

        if (doc.TryGetValue("local_path", out var lpVal) && lpVal.IsString && !string.IsNullOrWhiteSpace(lpVal.AsString))
        {
            var lp = lpVal.AsString;
            if (File.Exists(lp) && validStageRoots.Any(root => IsPathWithinRoot(lp, root)))
            {
                return Path.GetFullPath(lp);
            }
        }

        var fileName = doc.TryGetValue("name", out var nVal) && nVal.IsString ? nVal.AsString : null;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var exactFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(exactFileName) || !string.Equals(exactFileName, fileName, StringComparison.Ordinal))
        {
            return null;
        }

        foreach (var stageRoot in validStageRoots)
        {
            var candidate = Path.Combine(stageRoot, exactFileName);
            if (File.Exists(candidate) && IsPathWithinRoot(candidate, stageRoot))
            {
                var fullCand = Path.GetFullPath(candidate);
                try
                {
                    if (doc.TryGetValue("_id", out var idVal))
                    {
                        var oid = idVal.IsObjectId ? idVal.AsObjectId : (ObjectId.TryParse(idVal.ToString(), out var pOid) ? pOid : ObjectId.Empty);
                        if (oid != ObjectId.Empty)
                        {
                            var update = Builders<BsonDocument>.Update.Set("local_path", fullCand);
                            await _filesCollection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", oid), update, cancellationToken: cancellationToken).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[NEBULA-MONGO] Aviso ao atualizar local_path de '{File}'.", fileName);
                }

                return fullCand;
            }

            try
            {
                var matches = Directory.EnumerateFiles(stageRoot, "*", SearchOption.AllDirectories)
                    .Where(path => string.Equals(Path.GetFileName(path), exactFileName, StringComparison.OrdinalIgnoreCase));
                var match = matches.FirstOrDefault();
                if (match != null && File.Exists(match) && IsPathWithinRoot(match, stageRoot))
                {
                    var fullMatch = Path.GetFullPath(match);
                    try
                    {
                        if (doc.TryGetValue("_id", out var idVal))
                        {
                            var oid = idVal.IsObjectId ? idVal.AsObjectId : (ObjectId.TryParse(idVal.ToString(), out var pOid) ? pOid : ObjectId.Empty);
                            if (oid != ObjectId.Empty)
                            {
                                var update = Builders<BsonDocument>.Update.Set("local_path", fullMatch);
                                await _filesCollection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", oid), update, cancellationToken: cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "[NEBULA-MONGO] Aviso ao atualizar local_path de '{File}'.", fileName);
                    }

                    return fullMatch;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[NEBULA-MONGO] Falha ao varrer '{Dir}' procurando '{File}'.", stageRoot, fileName);
            }
        }

        return null;
    }

    internal static bool IsPathWithinRoot(string path, string root)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reivindica atomicamente um arquivo para processamento por um worker de upload.
    /// </summary>
    /// <param name="id">ID do arquivo.</param>
    /// <param name="workerId">Número do worker.</param>
    /// <param name="botIndex">Índice inicial do bot.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Documento atualizado se a reivindicação teve sucesso, ou null.</returns>
    public async Task<BsonDocument?> ClaimFileForUploadAsync(ObjectId id, int workerId, int botIndex, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var staleBefore = now - 3600;
        var filter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("_id", id),
            Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.In("status", new[] { "queued", "staging" }),
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("status", "failed"),
                    BuildRetryReadyFilter(now)),
                Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq("status", "uploading"),
                    Builders<BsonDocument>.Filter.Lt("modified_at", staleBefore))));

        var update = Builders<BsonDocument>.Update
            .Set("status", "uploading")
            .Set("worker_id", workerId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Set("bot_index", botIndex)
            .Set("started_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        return await _filesCollection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Varredura delta dos diretórios de staging: registra no MongoDB apenas os
    /// arquivos novos ou alterados desde a última passagem, tanto os que já foram
    /// enviados quanto os que ainda não foram. Arquivos já tratados e inalterados
    /// não voltam a consultar o catálogo.
    /// </summary>
    /// <param name="stagingDirs">Diretórios de staging.</param>
    /// <param name="deleteCompletedFromStaging">Se verdadeiro, tenta remover arquivos que já foram concluídos no Telegram mas permaneceram no stage.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task SyncStagingDirectoryAsync(IEnumerable<string> stagingDirs, bool deleteCompletedFromStaging = false, CancellationToken cancellationToken = default)
    {
        var roots = stagingDirs
            .Where(static dir => !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            .ToList();

        if (roots.Count == 0)
        {
            return;
        }

        // Índice do delta: uma única consulta por varredura alimenta o atalho de
        // arquivos já concluídos e dos que estão na fila de envio.
        var index = await GetStagingSyncIndexAsync(roots, cancellationToken).ConfigureAwait(false);
        PruneStagingScanJournal();

        foreach (var stageRoot in roots)
        {
            try
            {
                var files = Directory.EnumerateFiles(stageRoot, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file);
                    if (fileName.StartsWith('.') || fileName.Contains(".part", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".download", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Length <= 0)
                    {
                        continue;
                    }

                    var fullPath = fileInfo.FullName;
                    var scanStamp = new StagingScanStamp(fileInfo.Length, fileInfo.LastWriteTimeUtc.Ticks);
                    if (_stagingScanJournal.TryGetValue(fullPath, out var scanned) && scanned.Handled && scanned.Stamp == scanStamp)
                    {
                        // Delta: esta exata versão do arquivo já foi tratada.
                        continue;
                    }

                    var isStrm = string.Equals(Path.GetExtension(file), ".strm", StringComparison.OrdinalIgnoreCase);
                    if (isStrm)
                    {
                        // Arquivos .strm no staging não são payloads de upload; se a mídia correspondente já foi concluída no Telegram, exclui o .strm do disco
                        var completedStrm = await FindCompletedMediaAsync(fileName, fileInfo.FullName, cancellationToken).ConfigureAwait(false);
                        if (completedStrm != null)
                        {
                            try
                            {
                                if (File.Exists(fileInfo.FullName))
                                {
                                    File.Delete(fileInfo.FullName);
                                    _logger.LogInformation("[NEBULA-MONGO] Arquivo .strm de mídia já concluída no Telegram removido do staging: {Path}", fileInfo.FullName);
                                    CleanEmptyParentDirectories(fileInfo.FullName, stageRoot);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "[NEBULA-MONGO] Não foi possível remover .strm já concluído no staging: {Path}", fileInfo.FullName);
                            }
                        }

                        continue;
                    }

                    if (!NebulaMetadataExportService.IsUploadablePath(file))
                    {
                        continue;
                    }

                    // Delta: arquivo cujo caminho já está concluído no catálogo já foi
                    // enviado e sai do stage; pastas vazias são removidas em seguida.
                    if (index.CompletedPaths.Contains(fullPath))
                    {
                        RemoveCompletedStagingFile(fileInfo, stageRoot, completedReference: null);
                        _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                        continue;
                    }

                    // Delta: arquivo que já está na fila ou em envio (mesmo tamanho)
                    // pertence ao worker de upload, não à varredura.
                    if (index.PendingSizes.TryGetValue(fullPath, out var pendingSize) && pendingSize == fileInfo.Length)
                    {
                        _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                        continue;
                    }

                    var rawRel = Path.GetRelativePath(stageRoot, Path.GetDirectoryName(file) ?? stageRoot);
                    var routedRel = NebulaUploadEngine.RouteMediaRelativeDirectory(rawRel, fileName);
                    var parentId = await EnsureDirectoryStructureAsync(routedRel, cancellationToken).ConfigureAwait(false);
                    var parentValue = string.IsNullOrEmpty(parentId)
                        ? (BsonValue)BsonNull.Value
                        : ObjectId.TryParse(parentId, out var existingParentOid)
                            ? existingParentOid
                            : parentId;
                    var filter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("local_path", fileInfo.FullName),
                        Builders<BsonDocument>.Filter.And(
                            Builders<BsonDocument>.Filter.Eq("name", fileName),
                            Builders<BsonDocument>.Filter.Eq("parent", parentValue)));

                    using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
                    var existing = await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

                    if (existing != null)
                    {
                        var st = existing.TryGetValue("status", out var stVal) && stVal.IsString ? stVal.AsString : "unknown";
                        if (string.Equals(st, "completed", StringComparison.OrdinalIgnoreCase))
                        {
                            RemoveCompletedStagingFile(fileInfo, stageRoot, existing.GetValue("name", fileName).AsString);
                            _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                            continue;
                        }

                        if (string.Equals(st, "queued", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(st, "staging", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(st, "uploading", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!existing.TryGetValue("local_path", out var elp) || !elp.IsString || !string.Equals(elp.AsString, fileInfo.FullName, StringComparison.OrdinalIgnoreCase))
                            {
                                var updatePath = Builders<BsonDocument>.Update
                                    .Set("local_path", fileInfo.FullName)
                                    .Set("size", fileInfo.Length);
                                await _filesCollection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]), updatePath, cancellationToken: cancellationToken).ConfigureAwait(false);
                            }

                            _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                            continue;
                        }

                        // Um registro failed também pode ser um upload que já
                        // recebeu partes do Telegram antes da queda. Nunca o
                        // reative nesse caso, pois isso duplica a mídia.
                        if (HasTelegramParts(existing))
                        {
                            RemoveCompletedStagingFile(fileInfo, stageRoot, existing.GetValue("name", fileName).AsString);
                            _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                            continue;
                        }

                        // Se estava failed, reativa para queued
                        var reactivate = Builders<BsonDocument>.Update
                            .Set("status", "queued")
                            .Set("local_path", fileInfo.FullName)
                            .Set("size", fileInfo.Length)
                            .Set("queued_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                            .Set("mtime", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                            .Set("failed_reason", BsonNull.Value);

                        await _filesCollection.UpdateOneAsync(Builders<BsonDocument>.Filter.Eq("_id", existing["_id"]), reactivate, cancellationToken: cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("[NEBULA-MONGO] Arquivo reativado na fila: {File}", fileName);
                        _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                    }
                    else
                    {
                        // Verifica se esta mídia já foi enviada e concluída anteriormente no Telegram
                        var alreadyCompleted = await FindCompletedMediaAsync(fileName, fileInfo.FullName, cancellationToken).ConfigureAwait(false);
                        if (alreadyCompleted != null)
                        {
                            var compName = alreadyCompleted.GetValue("name", fileName).AsString;
                            RemoveCompletedStagingFile(fileInfo, stageRoot, compName);
                            _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                            continue;
                        }

                        // Arquivo novo detectado
                        var newDoc = new BsonDocument
                        {
                            { "_id", ObjectId.GenerateNewId() },
                            { "name", fileName },
                            { "type", "file" },
                            { "is_directory", false },
                            { "status", "queued" },
                            { "size", fileInfo.Length },
                            { "local_path", fileInfo.FullName },
                            { "parent", parentValue },
                            { "queued_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                            { "mtime", new DateTimeOffset(fileInfo.LastWriteTimeUtc).ToUnixTimeSeconds() },
                            { "ctime", new DateTimeOffset(fileInfo.CreationTimeUtc).ToUnixTimeSeconds() },
                            { "parts", new BsonArray() },
                            { "delete_source", false },
                            { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                            { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
                        };

                        if (await InsertFileDocIfAbsentAsync(newDoc, cancellationToken).ConfigureAwait(false))
                        {
                            _logger.LogInformation("[NEBULA-MONGO] Novo arquivo detectado no stage e enfileirado: {File}", fileName);
                        }
                        else
                        {
                            _logger.LogDebug("[NEBULA-MONGO] Arquivo já enfileirado por outro produtor: {File}", fileName);
                        }
                        _stagingScanJournal[fullPath] = new StagingScanEntry(scanStamp, Handled: true);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao sincronizar diretório de staging: {Dir}", stageRoot);
            }
        }
    }

    /// <summary>
    /// Carrega, em uma única consulta, o índice de staging usado pelo delta: os
    /// caminhos já concluídos no Telegram e os que estão na fila/envio (com o
    /// tamanho registrado, para detectar arquivos trocados no disco).
    /// </summary>
    /// <param name="stagingRoots">Raízes de staging monitoradas.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Índice de sincronização do staging.</returns>
    internal async Task<StagingSyncIndex> GetStagingSyncIndexAsync(IReadOnlyCollection<string> stagingRoots, CancellationToken cancellationToken = default)
    {
        var completedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingSizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var rootFilters = stagingRoots
                .Where(static root => !string.IsNullOrWhiteSpace(root))
                .Select(static root => new BsonRegularExpression(
                    $"^{Regex.Escape(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}[\\\\/]",
                    "i"))
                .Select(static regex => Builders<BsonDocument>.Filter.Regex("local_path", regex))
                .ToList();

            if (rootFilters.Count == 0)
            {
                return new StagingSyncIndex(completedPaths, pendingSizes);
            }

            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", "file"),
                Builders<BsonDocument>.Filter.Exists("local_path", true),
                Builders<BsonDocument>.Filter.Or(rootFilters));

            var projection = Builders<BsonDocument>.Projection
                .Include("status")
                .Include("local_path")
                .Include("size")
                .Include("parts")
                .Include("tg_file_id")
                .Include("file_id")
                .Include("tg_file");

            using var cursor = await _filesCollection.FindAsync(
                filter,
                new FindOptions<BsonDocument> { Projection = projection },
                cancellationToken).ConfigureAwait(false);
            var docs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in docs)
            {
                if (!doc.TryGetValue("local_path", out var localPathValue) || !localPathValue.IsString)
                {
                    continue;
                }

                string fullPath;
                try
                {
                    fullPath = Path.GetFullPath(localPathValue.AsString);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                {
                    continue;
                }

                var status = doc.TryGetValue("status", out var statusValue) && statusValue.IsString ? statusValue.AsString : string.Empty;
                if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) && HasTelegramParts(doc))
                {
                    completedPaths.Add(fullPath);
                    continue;
                }

                if (InFlightStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                {
                    pendingSizes[fullPath] = doc.TryGetValue("size", out var sizeValue) && sizeValue.IsNumeric ? sizeValue.ToInt64() : -1L;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao montar o índice de staging do delta.");
        }

        return new StagingSyncIndex(completedPaths, pendingSizes);
    }

    /// <summary>
    /// Tira do staging um arquivo cujo conteúdo já foi enviado ao Telegram. O stage é
    /// uma fila transitória: o que já subiu sai dele (inclusive capas, imagens e NFO,
    /// que continuam disponíveis no cache local do servidor), e as pastas que ficarem
    /// vazias são removidas.
    /// </summary>
    /// <param name="fileInfo">Arquivo de staging.</param>
    /// <param name="stageRoot">Raiz de staging (limite da limpeza de pastas vazias).</param>
    /// <param name="completedReference">Nome do registro já concluído que casou com este arquivo.</param>
    private void RemoveCompletedStagingFile(FileInfo fileInfo, string? stageRoot, string? completedReference)
    {
        if (!File.Exists(fileInfo.FullName))
        {
            return;
        }

        var detail = string.IsNullOrEmpty(completedReference)
            ? fileInfo.FullName
            : $"{fileInfo.FullName} ('{completedReference}')";

        try
        {
            File.Delete(fileInfo.FullName);

            if (NebulaProtectedContent.IsProtectedPath(fileInfo.Name))
            {
                _logger.LogInformation(
                    "[NEBULA-MONGO] Capa/metadado já enviado removido do stage: {Detail}",
                    detail);
            }
            else
            {
                _logger.LogInformation(
                    "[NEBULA-MONGO] Arquivo de staging já concluído no Telegram removido do disco: {Detail}",
                    detail);
            }

            CleanEmptyParentDirectories(fileInfo.FullName, stageRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Não foi possível remover staging file já concluído (arquivo em uso): {Path}", fileInfo.FullName);
        }
    }

    /// <summary>
    /// Descarta do diário de varredura os arquivos que já não existem no disco.
    /// </summary>
    private void PruneStagingScanJournal()
    {
        foreach (var key in _stagingScanJournal.Keys)
        {
            if (!File.Exists(key))
            {
                _stagingScanJournal.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Assinatura de uma versão do arquivo no disco (tamanho + data de modificação).
    /// </summary>
    private readonly record struct StagingScanStamp(long Length, long LastWriteUtcTicks);

    /// <summary>
    /// Entrada do diário de varredura de staging.
    /// </summary>
    private readonly record struct StagingScanEntry(StagingScanStamp Stamp, bool Handled);

    /// <summary>
    /// Índice de delta do staging: caminhos concluídos e tamanhos dos pendentes.
    /// </summary>
    internal readonly record struct StagingSyncIndex(HashSet<string> CompletedPaths, Dictionary<string, long> PendingSizes);

    /// <summary>
    /// Computa estatísticas detalhadas da fila de envio do MongoDB e do alimentador de disco.
    /// </summary>
    public async Task<(int Staging, int Queued, int Uploading, int Completed, int Failed, int Pending, int PendingDisk)> GetQueueStatsAsync(CancellationToken cancellationToken = default)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["queued"] = 0,
            ["staging"] = 0,
            ["uploading"] = 0,
            ["completed"] = 0,
            ["failed"] = 0
        };

        try
        {
            var match = new BsonDocument("$match", new BsonDocument("type", "file"));
            var group = new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$status" },
                { "count", new BsonDocument("$sum", 1) }
            });

            var pipeline = new[] { match, group };
            using var cursor = await _filesCollection.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken).ConfigureAwait(false);
            var results = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var row in results)
            {
                if (row.TryGetValue("_id", out var statusVal) && !statusVal.IsBsonNull && row.TryGetValue("count", out var countVal))
                {
                    var status = statusVal.AsString;
                    if (counts.ContainsKey(status))
                    {
                        counts[status] = countVal.ToInt32();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-MONGO] Erro ao agregar status da fila.");
        }

        var queued = counts["queued"];
        var staging = counts["staging"];
        var uploading = counts["uploading"];
        var completed = counts["completed"];
        var failed = counts["failed"];
        var pending = queued + staging + uploading;

        var pendingDisk = 0;
        try
        {
            var statsColl = _database.GetCollection<BsonDocument>("stats");
            using var cur = await statsColl.FindAsync(Builders<BsonDocument>.Filter.Eq("_id", "feeder"), cancellationToken: cancellationToken).ConfigureAwait(false);
            var feederDoc = await cur.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (feederDoc != null && feederDoc.TryGetValue("pending_disk_files", out var pdfVal))
            {
                pendingDisk = pdfVal.ToInt32();
            }
        }
        catch (Exception)
        {
            // Coleção stats opcional
        }

        return (staging, queued, uploading, completed, failed, pending, pendingDisk);
    }

    /// <summary>
    /// Retorna conjunto de identificadores normalizados de arquivos concluídos no MongoDB para limpeza.
    /// </summary>
    public async Task<HashSet<string>> GetCompletedTelegramItemsAsync(CancellationToken cancellationToken = default)
    {
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", "file"),
                Builders<BsonDocument>.Filter.Eq("status", "completed"));

            var projection = Builders<BsonDocument>.Projection
                .Include("name")
                .Include("parent")
                .Include("parts")
                .Include("tg_file_id")
                .Include("file_id")
                .Include("tg_file");

            using var cursor = await _filesCollection.FindAsync(filter, new FindOptions<BsonDocument> { Projection = projection }, cancellationToken).ConfigureAwait(false);
            var docs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in docs)
            {
                if (!HasTelegramParts(doc))
                {
                    continue;
                }

                if (doc.TryGetValue("parent", out var parentVal) && !parentVal.IsBsonNull)
                {
                    var pStr = parentVal.ToString()?.TrimEnd('/', '\\');
                    if (!string.IsNullOrEmpty(pStr))
                    {
                        var folderName = Path.GetFileName(pStr);
                        if (!string.IsNullOrEmpty(folderName)
                            && !folderName.Equals("Filmes", StringComparison.OrdinalIgnoreCase)
                            && !folderName.Equals("Series", StringComparison.OrdinalIgnoreCase)
                            && !folderName.Equals("Novelas", StringComparison.OrdinalIgnoreCase)
                            && !folderName.Equals("Animações", StringComparison.OrdinalIgnoreCase))
                        {
                            var normFolder = NormalizeCleanupString(folderName);
                            if (!string.IsNullOrEmpty(normFolder))
                            {
                                completed.Add(normFolder);
                            }
                        }
                    }
                }

                if (doc.TryGetValue("name", out var nameVal) && nameVal.IsString)
                {
                    var stem = Path.GetFileNameWithoutExtension(nameVal.AsString);
                    if (!string.IsNullOrEmpty(stem))
                    {
                        var normStem = NormalizeCleanupString(stem);
                        if (!string.IsNullOrEmpty(normStem))
                        {
                            completed.Add(normStem);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao listar itens concluídos para limpeza.");
        }

        return completed;
    }

    /// <summary>
    /// Retorna os caminhos locais exatos dos arquivos concluídos no MongoDB.
    /// Registros sem <c>local_path</c> são ignorados para evitar apagar um arquivo
    /// com o mesmo nome em outro diretório.
    /// </summary>
    public async Task<HashSet<string>> GetCompletedTelegramLocalPathsAsync(CancellationToken cancellationToken = default)
    {
        var completedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("type", "file"),
                Builders<BsonDocument>.Filter.Eq("status", "completed"),
                Builders<BsonDocument>.Filter.Exists("local_path", true));

            var projection = Builders<BsonDocument>.Projection
                .Include("local_path")
                .Include("parts")
                .Include("tg_file_id")
                .Include("file_id")
                .Include("tg_file");
            using var cursor = await _filesCollection.FindAsync(
                filter,
                new FindOptions<BsonDocument> { Projection = projection },
                cancellationToken).ConfigureAwait(false);
            var docs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in docs)
            {
                if (!HasTelegramParts(doc))
                {
                    continue;
                }

                if (!doc.TryGetValue("local_path", out var localPathValue) || !localPathValue.IsString)
                {
                    continue;
                }

                var localPath = localPathValue.AsString;
                if (string.IsNullOrWhiteSpace(localPath))
                {
                    continue;
                }

                try
                {
                    completedPaths.Add(Path.GetFullPath(localPath));
                }
                catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
                {
                    _logger.LogDebug(ex, "[NEBULA-MONGO] Ignorando local_path inválido durante a limpeza: {Path}", localPath);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[NEBULA-MONGO] Erro ao listar caminhos concluídos para limpeza.");
        }

        return completedPaths;
    }

    /// <summary>
    /// Normaliza string removendo acentos e caracteres não-alfanuméricos para comparação na limpeza.
    /// </summary>
    public static string NormalizeCleanupString(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        var normalized = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            if (char.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Limpa recursivamente diretórios pais vazios a partir do arquivo excluído até atingir a raiz de staging.
    /// </summary>
    internal void CleanEmptyParentDirectories(string? filePath, string? stageRoot)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            var currentDir = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(currentDir) || !Directory.Exists(currentDir))
            {
                return;
            }

            var fullStageRoot = !string.IsNullOrWhiteSpace(stageRoot)
                ? Path.GetFullPath(stageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : null;

            var isImmediateParent = true;

            while (!string.IsNullOrWhiteSpace(currentDir) && Directory.Exists(currentDir))
            {
                var fullCurrentDir = Path.GetFullPath(currentDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                if (fullStageRoot != null && string.Equals(fullCurrentDir, fullStageRoot, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                var pathRoot = Path.GetPathRoot(fullCurrentDir)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(pathRoot, fullCurrentDir, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (fullStageRoot == null && !isImmediateParent)
                {
                    break;
                }

                isImmediateParent = false;

                if (NebulaMetadataExportService.IsOrphanPendingMarkerDirectory(currentDir))
                {
                    NebulaMetadataExportService.RemovePendingMarker(currentDir);
                }

                var hasFiles = Directory.EnumerateFiles(currentDir, "*", SearchOption.AllDirectories).Any();
                if (!hasFiles)
                {
                    Directory.Delete(currentDir, true);
                    _logger.LogInformation("[NEBULA-MONGO] Diretório de staging vazio removido: {Dir}", currentDir);
                    currentDir = Path.GetDirectoryName(currentDir);
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[NEBULA-MONGO] Não foi possível verificar/remover diretórios vazios para {Path}", filePath);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
