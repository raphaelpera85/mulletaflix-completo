#pragma warning disable CA1707 // Identifiers should not contain underscores

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer.BackgroundTransfer;
using FubarDev.FtpServer.FileSystem;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

namespace Jellyfin.Server.Implementations.Nebula;

/// <summary>
/// Provedor do sistema de arquivos virtual do Nebula no FTP.
/// </summary>
public sealed class NebulaFileSystemProvider : IFileSystemClassFactory
{
    private readonly NebulaMongoContext _mongoContext;
    private readonly NebulaTelegramPool _telegramPool;
    private readonly NebulaPlaybackCache? _playbackCache;
    private readonly NebulaUploadEngine? _uploadEngine; // pode ser null em modo streamOnly
    private readonly ILogger<NebulaFileSystem> _logger;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaFileSystemProvider"/>.
    /// </summary>
    public NebulaFileSystemProvider(NebulaMongoContext mongoContext, NebulaTelegramPool telegramPool, NebulaUploadEngine? uploadEngine, NebulaPlaybackCache? playbackCache, ILogger<NebulaFileSystem> logger)
    {
        _mongoContext = mongoContext;
        _telegramPool = telegramPool;
        _playbackCache = playbackCache;
        _uploadEngine = uploadEngine;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<IUnixFileSystem> Create(FubarDev.FtpServer.IAccountInformation accountInformation)
    {
        var username = accountInformation?.FtpUser?.Identity?.Name ?? "raphael";
        IUnixFileSystem fs = new NebulaFileSystem(_mongoContext, _telegramPool, _uploadEngine, _playbackCache, _logger, username);
        return Task.FromResult(fs);
    }
}

/// <summary>
/// Sistema de arquivos virtual que lê a árvore de nós diretamente do MongoDB.
/// </summary>
public sealed class NebulaFileSystem : IUnixFileSystem
{
    private readonly NebulaMongoContext _mongoContext;
    private readonly NebulaTelegramPool _telegramPool;
    private readonly NebulaPlaybackCache? _playbackCache;
    private readonly NebulaUploadEngine? _uploadEngine; // pode ser null em modo streamOnly
    private readonly ILogger<NebulaFileSystem> _logger;
    private readonly string _username;
    private readonly string _userHomePath;

    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaFileSystem"/>.
    /// </summary>
    public NebulaFileSystem(
        NebulaMongoContext mongoContext,
        NebulaTelegramPool telegramPool,
        NebulaUploadEngine? uploadEngine,
        NebulaPlaybackCache? playbackCache,
        ILogger<NebulaFileSystem> logger,
        string username = "raphael")
    {
        _mongoContext = mongoContext;
        _telegramPool = telegramPool;
        _uploadEngine = uploadEngine;
        _playbackCache = playbackCache;
        _logger = logger;
        _username = string.IsNullOrWhiteSpace(username) ? "raphael" : username.Trim();
        _userHomePath = $"/{_username.Trim('/')}";
        // O login FTP identifica a conta, mas não deve virar parte do caminho
        // virtual. O catálogo compartilhado está organizado sob /raphael/;
        // iniciar a sessão em /mulleta faria a consulta procurar /raphael/mulleta
        // e deixaria a unidade montada vazia.
        Root = new NebulaDirectoryEntry(this, null, "/", "/", null);
    }

    /// <inheritdoc />
    public bool SupportsAppend => false;

    /// <inheritdoc />
    public bool SupportsResume => true;

    /// <inheritdoc />
    public bool SupportsNonEmptyDirectoryDelete => false;

    /// <inheritdoc />
    public StringComparer FileSystemEntryComparer => StringComparer.OrdinalIgnoreCase;

    /// <inheritdoc />
    public IUnixDirectoryEntry Root { get; }

    /// <inheritdoc />
    public async Task<IUnixFileSystemEntry?> GetEntryByNameAsync(IUnixDirectoryEntry directoryEntry, string name, CancellationToken cancellationToken)
    {
        var dir = (NebulaDirectoryEntry)directoryEntry;
        if ((dir.IsRoot || dir.FullVirtualPath == "/" || dir.FullVirtualPath.Equals(_userHomePath, StringComparison.OrdinalIgnoreCase)) &&
            string.Equals(name, "strm", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var doc = await _mongoContext.FindByNameAndParentAsync(name, dir.NodeId, dir.FullVirtualPath, cancellationToken).ConfigureAwait(false);
        if (doc == null)
        {
            return null;
        }

        var isDir = doc.GetValue("is_directory", false).AsBoolean || doc.GetValue("type", string.Empty).AsString == "dir";
        var id = doc.GetValue("_id").ToString()!;
        var childVirtualPath = dir.FullVirtualPath == "/" ? $"/{name}" : $"{dir.FullVirtualPath.TrimEnd('/')}/{name}";

        if (isDir)
        {
            return new NebulaDirectoryEntry(this, id, name, childVirtualPath, dir);
        }

        if (!HasTelegramPayload(doc))
        {
            return null;
        }

        var size = doc.GetValue("size", 0L).ToInt64();
        return new NebulaFileEntry(this, id, name, size, childVirtualPath);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IUnixFileSystemEntry>> GetEntriesAsync(IUnixDirectoryEntry directoryEntry, CancellationToken cancellationToken)
    {
        var dir = (NebulaDirectoryEntry)directoryEntry;
        var docs = await _mongoContext.GetChildrenAsync(dir.NodeId, dir.FullVirtualPath, cancellationToken).ConfigureAwait(false);
        var entries = new List<IUnixFileSystemEntry>();

        var isRoot = dir.IsRoot || dir.FullVirtualPath == "/" || dir.FullVirtualPath.Equals(_userHomePath, StringComparison.OrdinalIgnoreCase);

        // Deduplica entradas pelo nome para evitar registros duplicados retornados do MongoDB
        var distinctDocs = docs
            .GroupBy(d => d.GetValue("name", string.Empty).AsString, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(d =>
                HasTelegramPayload(d) ||
                d.GetValue("is_directory", false).AsBoolean ||
                d.GetValue("type", string.Empty).AsString == "dir").First());

        foreach (var doc in distinctDocs)
        {
            var name = doc.GetValue("name", string.Empty).AsString;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var isDir = doc.GetValue("is_directory", false).AsBoolean || doc.GetValue("type", string.Empty).AsString == "dir";
            if (isDir && isRoot && string.Equals(name, "strm", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var id = doc.GetValue("_id").ToString()!;
            var childVirtualPath = dir.FullVirtualPath == "/" ? $"/{name}" : $"{dir.FullVirtualPath.TrimEnd('/')}/{name}";

            if (isDir)
            {
                // Oculta pastas que não têm nenhum arquivo com payload do Telegram (direto ou aninhado).
                // Isso evita que pastas "fantasma" apareçam no disco N: quando ainda não há mídia publicada.
                var hasFiles = await _mongoContext.HasAnyFileDescendantAsync(childVirtualPath, cancellationToken).ConfigureAwait(false);
                if (!hasFiles)
                {
                    continue;
                }

                entries.Add(new NebulaDirectoryEntry(this, id, name, childVirtualPath, dir));
            }
            else if (HasTelegramPayload(doc))
            {
                var size = doc.GetValue("size", 0L).ToInt64();
                entries.Add(new NebulaFileEntry(this, id, name, size, childVirtualPath));
            }
        }

        return entries;
    }

    /// <inheritdoc />
    public Task<IUnixFileSystemEntry> MoveAsync(IUnixDirectoryEntry parent, IUnixFileSystemEntry source, IUnixDirectoryEntry target, string fileName, CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Movimentação dinâmica não suportada no virtual filesystem.");
    }

    /// <inheritdoc />
    public Task UnlinkAsync(IUnixFileSystemEntry entry, CancellationToken cancellationToken)
    {
        if (entry is not NebulaFileEntry file)
        {
            return Task.FromException(new NotSupportedException("Somente arquivos Nebula podem ser removidos."));
        }

        return Task.FromException(new NotSupportedException($"Arquivos Telegram são imutáveis e não podem ser removidos via FTP: {file.Name}"));
    }

    /// <inheritdoc />
    public async Task<IUnixDirectoryEntry> CreateDirectoryAsync(IUnixDirectoryEntry targetDirectory, string directoryName, CancellationToken cancellationToken)
    {
        if (targetDirectory is not NebulaDirectoryEntry parent || string.IsNullOrWhiteSpace(directoryName) || directoryName is "." or ".." || directoryName.Contains('/', StringComparison.Ordinal) || directoryName.Contains('\\', StringComparison.Ordinal))
        {
            throw new ArgumentException("Nome de diretório FTP inválido.", nameof(directoryName));
        }

        var existing = await _mongoContext.FindByNameAndParentAsync(directoryName, parent.NodeId, parent.FullVirtualPath, cancellationToken).ConfigureAwait(false);
        if (existing != null)
        {
            var isDir = existing.GetValue("is_directory", false).AsBoolean || existing.GetValue("type", string.Empty).AsString == "dir";
            if (isDir)
            {
                var existingPath = parent.FullVirtualPath == "/" ? $"/{directoryName}" : $"{parent.FullVirtualPath.TrimEnd('/')}/{directoryName}";
                return new NebulaDirectoryEntry(this, existing.GetValue("_id").ToString()!, directoryName, existingPath, parent);
            }

            throw new IOException($"Já existe um arquivo chamado '{directoryName}'.");
        }

        var nodeId = ObjectId.GenerateNewId();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var targetPath = parent.FullVirtualPath == "/" ? $"/{directoryName}" : $"{parent.FullVirtualPath.TrimEnd('/')}/{directoryName}";

        var document = new BsonDocument
        {
            ["_id"] = nodeId,
            ["name"] = directoryName,
            ["type"] = "dir",
            ["is_directory"] = true,
            ["parent"] = parent.FullVirtualPath,
            ["status"] = "completed",
            ["created_at"] = now,
            ["modified_at"] = now
        };

        await _mongoContext.InsertFileDocAsync(document, cancellationToken).ConfigureAwait(false);
        return new NebulaDirectoryEntry(this, nodeId.ToString(), directoryName, targetPath, parent);
    }

    /// <inheritdoc />
    public async Task<Stream> OpenReadAsync(IUnixFileEntry fileEntry, long startPosition, CancellationToken cancellationToken)
    {
        if (fileEntry is not NebulaFileEntry nebulaFile)
        {
            throw new FileNotFoundException("Entrada de arquivo Nebula inválida.");
        }

        var doc = await _mongoContext.FindByIdAsync(nebulaFile.NodeId, cancellationToken).ConfigureAwait(false);
        if (doc == null && !string.IsNullOrEmpty(nebulaFile.FullVirtualPath))
        {
            var parentPath = Path.GetDirectoryName(nebulaFile.FullVirtualPath.Replace('\\', '/'))?.Replace('\\', '/');
            doc = await _mongoContext.FindByNameAndParentAsync(nebulaFile.Name, null, parentPath, cancellationToken).ConfigureAwait(false);
        }

        if (doc == null)
        {
            throw new FileNotFoundException("Referência do arquivo Nebula não encontrada no banco de dados.", nebulaFile.Name);
        }

        if (!HasTelegramPayload(doc))
        {
            throw new FileNotFoundException("Arquivo Nebula ainda não possui partes publicadas no Telegram.", nebulaFile.Name);
        }

        var localPath = doc.TryGetValue("local_path", out var lpVal) && lpVal.IsString ? lpVal.AsString : null;
        var partsList = new List<NebulaStreamPart>();

        if (doc.TryGetValue("parts", out var partsValue) && partsValue.IsBsonArray && partsValue.AsBsonArray.Count > 0)
        {
            var partsArray = partsValue.AsBsonArray;
            foreach (var partElement in partsArray)
            {
                if (partElement is not BsonDocument partDoc)
                {
                    continue;
                }

                var partFileId = partDoc.Contains("tg_file_id") ? partDoc.GetValue("tg_file_id").AsString : (partDoc.Contains("tg_file") ? partDoc.GetValue("tg_file").AsString : string.Empty);
                var botIndex = partDoc.Contains("bot_index") ? partDoc.GetValue("bot_index").ToInt32() : -1;
                var chatId = partDoc.Contains("tg_chat_id") ? partDoc.GetValue("tg_chat_id").ToInt64() : (partDoc.Contains("tg_chat") ? partDoc.GetValue("tg_chat").ToInt64() : 0L);
                var messageId = partDoc.Contains("tg_message_id") ? checked((int)partDoc.GetValue("tg_message_id").ToInt64()) : (partDoc.Contains("tg_message") ? checked((int)partDoc.GetValue("tg_message").ToInt64()) : 0);
                var partNum = partDoc.Contains("part_number") ? partDoc.GetValue("part_number").ToInt32() : (partDoc.Contains("part_id") ? partDoc.GetValue("part_id").ToInt32() : partsList.Count);
                var partSize = partDoc.Contains("size") ? partDoc.GetValue("size").ToInt64() : (partDoc.Contains("file_size") ? partDoc.GetValue("file_size").ToInt64() : 16L * 1024L * 1024L);

                partsList.Add(new NebulaStreamPart
                {
                    PartIndex = partNum,
                    FileOffset = 0,
                    Size = partSize,
                    FileId = partFileId,
                    BotIndex = botIndex,
                    ChatId = chatId,
                    MessageId = messageId,
                    LocalPath = localPath
                });
            }

            long sortedOffset = 0;
            foreach (var part in partsList.OrderBy(part => part.PartIndex))
            {
                part.FileOffset = sortedOffset;
                sortedOffset += part.Size;
            }
        }
        else
        {
            var chatId = doc.Contains("tg_chat_id") ? doc.GetValue("tg_chat_id").ToInt64() : (doc.Contains("tg_chat") ? doc.GetValue("tg_chat").ToInt64() : 0L);
            var messageId = doc.Contains("tg_message_id") ? checked((int)doc.GetValue("tg_message_id").ToInt64()) : (doc.Contains("tg_message") ? checked((int)doc.GetValue("tg_message").ToInt64()) : 0);
            var fileId = doc.Contains("tg_file_id") ? doc.GetValue("tg_file_id").AsString : (doc.Contains("tg_file") ? doc.GetValue("tg_file").AsString : string.Empty);
            var botIndex = doc.Contains("bot_index") ? doc.GetValue("bot_index").ToInt32() : -1;
            var fileSize = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : 0L);

            partsList.Add(new NebulaStreamPart
            {
                PartIndex = 0,
                FileOffset = 0,
                Size = fileSize,
                FileId = fileId,
                BotIndex = botIndex,
                ChatId = chatId,
                MessageId = messageId,
                LocalPath = localPath
            });
        }

        var totalSize = doc.Contains("size") ? doc.GetValue("size").ToInt64() : (doc.Contains("file_size") ? doc.GetValue("file_size").ToInt64() : partsList.Sum(part => part.Size));
        var stream = new NebulaChunkedStream(_telegramPool, partsList, totalSize, _logger, _playbackCache, nebulaFile.NodeId);
        if (startPosition > 0)
        {
            stream.Seek(startPosition, SeekOrigin.Begin);
        }

        return stream;
    }

    private static bool HasTelegramPayload(BsonDocument doc)
    {
        if (doc.TryGetValue("parts", out var partsValue) && partsValue.IsBsonArray)
        {
            var parts = partsValue.AsBsonArray;
            if (parts.Count > 0)
            {
                return parts.Count == parts.OfType<BsonDocument>().Count() &&
                    parts.OfType<BsonDocument>().All(part => HasStringValue(part, "tg_file_id") || HasStringValue(part, "tg_file"));
            }
        }

        return HasStringValue(doc, "tg_file_id") || HasStringValue(doc, "tg_file");
    }

    private static bool HasStringValue(BsonDocument doc, string key)
    {
        return doc.TryGetValue(key, out var value) && value.IsString && !string.IsNullOrWhiteSpace(value.AsString);
    }

    /// <inheritdoc />
    public Task<IBackgroundTransfer?> CreateAsync(IUnixDirectoryEntry targetDirectory, string fileName, Stream data, CancellationToken cancellationToken)
    {
        if (targetDirectory is not NebulaDirectoryEntry directory || string.IsNullOrWhiteSpace(fileName))
        {
            return Task.FromResult<IBackgroundTransfer?>(null);
        }

        // Modo streamOnly: uploads não permitidos
        if (_uploadEngine == null)
        {
            return Task.FromResult<IBackgroundTransfer?>(null);
        }

        return Task.FromResult<IBackgroundTransfer?>(new NebulaBackgroundTransfer(
            fileName,
            data,
            directory.NodeId,
            _uploadEngine));
    }

    /// <inheritdoc />
    public Task<IBackgroundTransfer?> ReplaceAsync(IUnixFileEntry fileEntry, Stream data, CancellationToken cancellationToken)
    {
        return Task.FromException<IBackgroundTransfer?>(new NotSupportedException("Substituição FTP ainda não é suportada para arquivos Telegram imutáveis."));
    }

    /// <inheritdoc />
    public Task<IBackgroundTransfer?> AppendAsync(IUnixFileEntry fileEntry, long? startPosition, Stream data, CancellationToken cancellationToken)
    {
        return Task.FromException<IBackgroundTransfer?>(new NotSupportedException("Append FTP ainda não é suportado para arquivos Telegram imutáveis."));
    }

    /// <inheritdoc />
    public Task<IUnixFileSystemEntry> SetMacTimeAsync(IUnixFileSystemEntry entry, DateTimeOffset? modify, DateTimeOffset? access, DateTimeOffset? create, CancellationToken cancellationToken)
    {
        return Task.FromResult(entry);
    }
}

internal sealed class NebulaBackgroundTransfer : IBackgroundTransfer
{
    private readonly string _fileName;
    private readonly Stream _data;
    private readonly string? _parentId;
    private readonly NebulaUploadEngine? _uploadEngine; // pode ser null em modo streamOnly
    private bool _disposed;

    public NebulaBackgroundTransfer(string fileName, Stream data, string? parentId, NebulaUploadEngine? uploadEngine)
    {
        TransferId = Guid.NewGuid().ToString("N");
        _fileName = Path.GetFileName(fileName);
        _data = data;
        _parentId = parentId;
        _uploadEngine = uploadEngine;
    }

    public string TransferId { get; }

    public async Task Start(IProgress<long> progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_fileName))
        {
            throw new InvalidOperationException("Nome de arquivo FTP inválido.");
        }

        // Modo streamOnly: uploads não permitidos
        if (_uploadEngine == null)
        {
            throw new InvalidOperationException("Uploads não suportados no modo Somente Streaming. Use o modo Envio para enviar arquivos.");
        }

        var stagingDirectory = Path.Combine(Path.GetTempPath(), "MulletaFlix", "NebulaFtp");
        Directory.CreateDirectory(stagingDirectory);
        var stagingPath = Path.Combine(stagingDirectory, $"{Guid.NewGuid():N}.{_fileName}");

        try
        {
            long fileLength = 0;
            await using (var output = new FileStream(stagingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, true))
            {
                await _data.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                fileLength = output.Length;
            }

            var uploaded = await _uploadEngine.ProcessFileUploadAsync(stagingPath, _fileName, _parentId, cancellationToken).ConfigureAwait(false);
            if (!uploaded)
            {
                throw new IOException($"Falha ao publicar o arquivo FTP '{_fileName}'.");
            }

            progress?.Report(fileLength);
        }
        finally
        {
            _data.Dispose();
            if (File.Exists(stagingPath))
            {
                File.Delete(stagingPath);
                _uploadEngine?.CleanEmptyParentDirectories(stagingPath);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _data.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Entrada de diretório virtual.
/// </summary>
public sealed class NebulaDirectoryEntry : IUnixDirectoryEntry
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaDirectoryEntry"/>.
    /// </summary>
    public NebulaDirectoryEntry(IUnixFileSystem fileSystem, string? nodeId, string name, string fullVirtualPath, NebulaDirectoryEntry? parent = null)
    {
        FileSystem = fileSystem;
        NodeId = nodeId;
        Name = name;
        FullVirtualPath = fullVirtualPath;
        Parent = parent;
    }

    /// <summary>
    /// ID do nó no MongoDB.
    /// </summary>
    public string? NodeId { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Caminho POSIX completo deste diretório.
    /// </summary>
    public string FullVirtualPath { get; }

    /// <inheritdoc />
    public IUnixFileSystem FileSystem { get; }

    /// <inheritdoc />
    public IUnixFileSystemEntry? Parent { get; }

    /// <inheritdoc />
    public bool IsRoot => Parent == null;

    /// <inheritdoc />
    public bool IsDeletable => false;

    /// <inheritdoc />
    public DateTimeOffset? LastWriteTime => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? CreatedTime => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public long NumberOfLinks => 1;

    /// <inheritdoc />
    public IUnixPermissions Permissions => new SimpleUnixPermissions();

    /// <inheritdoc />
    public string Owner => "nebula";

    /// <inheritdoc />
    public string Group => "nebula";
}

/// <summary>
/// Entrada de arquivo virtual.
/// </summary>
public sealed class NebulaFileEntry : IUnixFileEntry
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="NebulaFileEntry"/>.
    /// </summary>
    public NebulaFileEntry(IUnixFileSystem fileSystem, string nodeId, string name, long size, string fullVirtualPath)
    {
        FileSystem = fileSystem;
        NodeId = nodeId;
        Name = name;
        Size = size;
        FullVirtualPath = fullVirtualPath;
    }

    /// <summary>
    /// ID do nó no MongoDB.
    /// </summary>
    public string NodeId { get; }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Caminho POSIX completo deste arquivo.
    /// </summary>
    public string FullVirtualPath { get; }

    /// <inheritdoc />
    public IUnixFileSystem FileSystem { get; }

    /// <inheritdoc />
    public long Size { get; }

    /// <inheritdoc />
    public bool IsDeletable => false;

    /// <inheritdoc />
    public DateTimeOffset? LastWriteTime => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public DateTimeOffset? CreatedTime => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public long NumberOfLinks => 1;

    /// <inheritdoc />
    public IUnixPermissions Permissions => new SimpleUnixPermissions();

    /// <inheritdoc />
    public string Owner => "nebula";

    /// <inheritdoc />
    public string Group => "nebula";
}

/// <summary>
/// Permissões Unix simples de leitura e escrita.
/// </summary>
public sealed class SimpleUnixPermissions : IUnixPermissions
{
    /// <inheritdoc />
    public IAccessMode User => new SimpleAccessMode(true, true, true);

    /// <inheritdoc />
    public IAccessMode Group => new SimpleAccessMode(true, false, true);

    /// <inheritdoc />
    public IAccessMode Other => new SimpleAccessMode(true, false, true);
}

/// <summary>
/// Modo de acesso.
/// </summary>
public sealed class SimpleAccessMode : IAccessMode
{
    /// <summary>
    /// Inicializa uma nova instância de <see cref="SimpleAccessMode"/>.
    /// </summary>
    public SimpleAccessMode(bool read, bool write, bool execute)
    {
        Read = read;
        Write = write;
        Execute = execute;
    }

    /// <inheritdoc />
    public bool Read { get; }

    /// <inheritdoc />
    public bool Write { get; }

    /// <inheritdoc />
    public bool Execute { get; }
}
