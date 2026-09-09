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
using MongoDB.Driver;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Camada de acesso a dados nativa em C# para o MongoDB do Nebula.
/// </summary>
public sealed class NebulaMongoContext : IDisposable
{
    private readonly ILogger<NebulaMongoContext> _logger;
    private readonly MongoClient _client;
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<BsonDocument> _filesCollection;
    private readonly IMongoCollection<BsonDocument> _usersCollection;
    private readonly IMongoCollection<BsonDocument> _botTokensCollection;
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
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);

        _client = new MongoClient(settings);
        _database = _client.GetDatabase(dbName);
        _filesCollection = _database.GetCollection<BsonDocument>("files");
        _usersCollection = _database.GetCollection<BsonDocument>("users");
        _botTokensCollection = _database.GetCollection<BsonDocument>("bot_tokens");
    }

    /// <summary>
    /// Garante que o banco compartilhado tenha as coleções básicas do Nebula.
    /// O MongoDB cria o banco na primeira coleção criada; a operação é idempotente
    /// e preserva as coleções existentes para permitir uso compartilhado com outro
    /// cliente Nebula.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        foreach (var collectionName in new[] { "files", "users", "bot_tokens" })
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
            (Builders<BsonDocument>.IndexKeys.Ascending("parent").Ascending("name"), "parent_1_name_1")
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

        await CleanupStrmRootDirectoryAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("[NEBULA-MONGO] Índices do MongoDB verificados com sucesso.");
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
        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
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
        await _filesCollection.InsertOneAsync(doc, cancellationToken: cancellationToken).ConfigureAwait(false);
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
        var filter = Builders<BsonDocument>.Filter.Eq("_id", id);
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

        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("name", pathOrName),
            Builders<BsonDocument>.Filter.Eq("local_path", pathOrName),
            Builders<BsonDocument>.Filter.Eq("tg_file_id", pathOrName),
            Builders<BsonDocument>.Filter.Eq("tg_file", pathOrName));

        using var cursor = await _filesCollection.FindAsync(filter, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
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
            Builders<BsonDocument>.Filter.Eq("status", "completed"));

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
    /// </summary>
    public async Task<long> PruneCompletedAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("status", "error");
        var result = await _filesCollection.DeleteManyAsync(filter, cancellationToken).ConfigureAwait(false);
        return result.DeletedCount;
    }

    /// <summary>
    /// Obtém todos os documentos da coleção de arquivos para sincronização.
    /// </summary>
    public async Task<List<BsonDocument>> GetAllFilesForSyncAsync(CancellationToken cancellationToken = default)
    {
        using var cursor = await _filesCollection.FindAsync(Builders<BsonDocument>.Filter.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);
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

        foreach (var segment in segments)
        {
            var existing = await FindByNameAndParentAsync(segment, currentParent, cancellationToken).ConfigureAwait(false);
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
    /// Varridura de diretórios de staging para sincronizar e registrar arquivos novos/pendentes no MongoDB (replicação de staging_scanner).
    /// </summary>
    /// <param name="stagingDirs">Diretórios de staging.</param>
    /// <param name="deleteCompletedFromStaging">Se verdadeiro, tenta remover arquivos que já foram concluídos no Telegram mas permaneceram no stage.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Uma tarefa assíncrona.</returns>
    public async Task SyncStagingDirectoryAsync(IEnumerable<string> stagingDirs, bool deleteCompletedFromStaging = false, CancellationToken cancellationToken = default)
    {
        var validExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mkv", ".mp4", ".avi", ".mov", ".m4v", ".ts", ".webm"
        };

        foreach (var stageRoot in stagingDirs)
        {
            if (string.IsNullOrWhiteSpace(stageRoot) || !Directory.Exists(stageRoot))
            {
                continue;
            }

            try
            {
                var files = Directory.EnumerateFiles(stageRoot, "*.*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var ext = Path.GetExtension(file);
                    if (!validExts.Contains(ext) && !NebulaMetadataExportService.IsMetadataSidecarPath(file))
                    {
                        continue;
                    }

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
                            if (deleteCompletedFromStaging)
                            {
                                try
                                {
                                    if (File.Exists(fileInfo.FullName))
                                    {
                                        File.Delete(fileInfo.FullName);
                                        _logger.LogInformation("[NEBULA-MONGO] Arquivo de staging já concluído removido do disco: {Path}", fileInfo.FullName);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "[NEBULA-MONGO] Não foi possível remover staging file já concluído (arquivo em uso): {Path}", fileInfo.FullName);
                                }
                            }

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
                    }
                    else
                    {
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

                        await _filesCollection.InsertOneAsync(newDoc, cancellationToken: cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("[NEBULA-MONGO] Novo arquivo detectado no stage e enfileirado: {File}", fileName);
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
                .Include("parent");

            using var cursor = await _filesCollection.FindAsync(filter, new FindOptions<BsonDocument> { Projection = projection }, cancellationToken).ConfigureAwait(false);
            var docs = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

            foreach (var doc in docs)
            {
                if (doc.TryGetValue("parent", out var parentVal) && !parentVal.IsBsonNull)
                {
                    var pStr = parentVal.ToString()?.TrimEnd('/', '\\');
                    if (!string.IsNullOrEmpty(pStr))
                    {
                        var folderName = Path.GetFileName(pStr);
                        if (!string.IsNullOrEmpty(folderName) && !folderName.Equals("Filmes", StringComparison.OrdinalIgnoreCase) && !folderName.Equals("Series", StringComparison.OrdinalIgnoreCase))
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

            var projection = Builders<BsonDocument>.Projection.Include("local_path");
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
