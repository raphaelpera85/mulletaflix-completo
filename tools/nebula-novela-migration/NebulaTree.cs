using MongoDB.Bson;
using MongoDB.Driver;

namespace MulletaFlix.Tools.NebulaNovelaMigration;

/// <summary>
/// Tipo do valor armazenado no campo 'parent' de um nó do catálogo do Nebula.
/// </summary>
internal enum ParentKind
{
    /// <summary>Sem pai declarado (raiz).</summary>
    None,

    /// <summary>Pai referenciado por ObjectId (formato moderno).</summary>
    ObjectId,

    /// <summary>Pai referenciado por caminho virtual POSIX (formato legado).</summary>
    Path
}

/// <summary>
/// Nó do catálogo virtual do Nebula, com o caminho virtual já resolvido.
/// </summary>
internal sealed class NebulaNode
{
    /// <summary>Inicializa o nó com o documento BSON e o caminho virtual resolvido.</summary>
    public NebulaNode(BsonDocument document, string path)
    {
        Document = document;
        Path = path;
    }

    /// <summary>Documento BSON original.</summary>
    public BsonDocument Document { get; }

    /// <summary>Identificador do nó (hex do ObjectId).</summary>
    public string Id => Document["_id"].AsObjectId.ToString();

    /// <summary>Nome exibido do nó.</summary>
    public string Name => Document.GetValue("name", string.Empty).AsString;

    /// <summary>Caminho virtual no banco (ex.: 'raphael/Series/Avenida Brasil').</summary>
    public string Path { get; }

    /// <summary>Tipo do valor do campo 'parent'.</summary>
    public ParentKind ParentKind => Document.TryGetValue("parent", out var parent)
        ? parent switch
        {
            { IsBsonNull: true } => ParentKind.None,
            { IsObjectId: true } => ParentKind.ObjectId,
            { IsString: true } when !string.IsNullOrEmpty(parent.AsString) => ParentKind.Path,
            _ => ParentKind.None
        }
        : ParentKind.None;

    /// <summary>Valor textual do campo 'parent' (hex do ObjectId ou caminho legado).</summary>
    public string ParentText => Document.TryGetValue("parent", out var parent) && !parent.IsBsonNull
        ? parent.ToString() ?? string.Empty
        : string.Empty;

    /// <summary>Indica se o nó é um diretório.</summary>
    public bool IsDirectory => Document.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
        || Document.GetValue("is_directory", false).AsBoolean;

    /// <summary>Tipo exibido ('dir' ou 'file').</summary>
    public string Kind => IsDirectory ? "dir" : "file";

    /// <summary>
    /// Caminho como o NebulaFileSystem enxerga (a sessão FTP não expõe o prefixo '/raphael').
    /// </summary>
    public string FileSystemPath
    {
        get
        {
            var trimmed = Path;
            if (trimmed.Equals("raphael", StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }

            if (trimmed.StartsWith("raphael/", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed["raphael".Length..];
            }

            return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
        }
    }

    /// <summary>
    /// Valores de 'parent' que fazem um documento ser filho direto deste nó,
    /// replicando exatamente os filtros de GetChildrenAsync do NebulaMongoContext.
    /// </summary>
    public IEnumerable<string> ParentKeysForChildren()
    {
        var path = FileSystemPath;
        if (path is "/" or "/raphael")
        {
            yield return "/";
            yield return "/raphael";
            yield return string.Empty;
            yield return "null";
        }
        else
        {
            yield return path;
            if (!path.StartsWith("/raphael/", StringComparison.OrdinalIgnoreCase))
            {
                yield return "/raphael" + path;
            }
        }

        yield return Id;
    }
}

/// <summary>
/// Árvore do catálogo do Nebula carregada da coleção 'files' (somente leitura em memória).
/// </summary>
internal sealed class NebulaTree
{
    private readonly List<NebulaNode> _nodes = [];
    private readonly Dictionary<string, NebulaNode> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NebulaNode> _byPath = new(StringComparer.OrdinalIgnoreCase);

    private NebulaTree(BsonDocument rootDocument)
    {
        Root = new NebulaNode(rootDocument, "raphael");
    }

    /// <summary>Nó raiz sintético que representa a raiz do drive (sessão FTP).</summary>
    public NebulaNode Root { get; }

    /// <summary>Quantidade de documentos carregados.</summary>
    public int Count => _nodes.Count;

    /// <summary>Carrega todos os documentos da coleção e resolve os caminhos virtuais.</summary>
    public static async Task<NebulaTree> LoadAsync(IMongoCollection<BsonDocument> files)
    {
        var documents = await files.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync().ConfigureAwait(false);
        var rootDocument = new BsonDocument
        {
            { "_id", ObjectId.Empty },
            { "name", "raphael" },
            { "type", "dir" },
            { "is_directory", true }
        };

        var tree = new NebulaTree(rootDocument);
        foreach (var document in documents)
        {
            if (document.Contains("_id") && document["_id"].IsObjectId)
            {
                tree._byId[document["_id"].AsObjectId.ToString()] = new NebulaNode(document, string.Empty);
            }
        }

        // O caminho só pode ser resolvido depois que todos os nós estão indexados por id.
        foreach (var node in tree._byId.Values)
        {
            var resolved = tree.ResolvePath(node);
            var finalNode = new NebulaNode(node.Document, resolved);
            tree._nodes.Add(finalNode);
            tree._byPath[resolved] = finalNode;
            tree._byId[finalNode.Id] = finalNode;
        }

        return tree;
    }

    /// <summary>Adiciona um nó criado durante a execução (ex.: a raiz 'Novelas').</summary>
    public void Add(NebulaNode node)
    {
        _nodes.Add(node);
        _byId[node.Id] = node;
        _byPath[node.Path] = node;
    }

    /// <summary>Busca um nó pelo caminho virtual resolvido.</summary>
    public NebulaNode? FindByPath(string path)
        => _byPath.TryGetValue(path.Trim('/'), out var node) ? node : null;

    /// <summary>Busca um nó pelo identificador.</summary>
    public NebulaNode? FindById(string id)
        => _byId.TryGetValue(id, out var node) ? node : null;

    /// <summary>Todos os nós resolvidos da árvore.</summary>
    public IEnumerable<NebulaNode> Nodes => _nodes;

    /// <summary>Filhos diretos segundo os filtros reais do NebulaFileSystem.</summary>
    public List<NebulaNode> GetResolvableChildren(NebulaNode parent)
    {
        var keys = new HashSet<string>(parent.ParentKeysForChildren(), StringComparer.OrdinalIgnoreCase);
        var children = new List<NebulaNode>();

        if (parent.Id == ObjectId.Empty.ToString())
        {
            // A raiz do drive não existe como documento: vale apenas o critério por caminho.
            foreach (var node in _nodes)
            {
                if (node.ParentKind != ParentKind.ObjectId && keys.Contains(node.ParentText))
                {
                    children.Add(node);
                }
            }

            return children;
        }

        foreach (var node in _nodes)
        {
            if (node.Id == parent.Id)
            {
                continue;
            }

            if (keys.Contains(node.ParentText))
            {
                children.Add(node);
            }
        }

        return children;
    }

    /// <summary>Todos os descendentes do nó, em qualquer profundidade.</summary>
    public List<NebulaNode> GetDescendants(NebulaNode node)
    {
        var prefix = node.Path + "/";
        return _nodes
            .Where(candidate => candidate.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Reconstrói o caminho virtual subindo pelos pais, aceitando tanto ObjectId
    /// quanto caminho legado (mesma lógica de ResolvePath do NebulaMongoContext).
    /// </summary>
    private string ResolvePath(NebulaNode node)
    {
        var segments = new Stack<string>();
        var current = node.Document;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (current is not null && current.Contains("name") && visited.Add(current["_id"].AsObjectId.ToString()))
        {
            segments.Push(current.GetValue("name", string.Empty).AsString);
            if (!current.TryGetValue("parent", out var parent) || parent.IsBsonNull)
            {
                break;
            }

            if (parent.IsObjectId && _byId.TryGetValue(parent.AsObjectId.ToString(), out var parentNode))
            {
                current = parentNode.Document;
                continue;
            }

            if (parent.IsString)
            {
                var legacy = parent.AsString.Replace('\\', '/').Trim('/');
                if (!string.IsNullOrEmpty(legacy))
                {
                    foreach (var segment in legacy.Split('/', StringSplitOptions.RemoveEmptyEntries).Reverse())
                    {
                        segments.Push(segment);
                    }
                }
            }

            break;
        }

        return string.Join('/', segments);
    }
}

/// <summary>
/// Alteração de 'parent' registrada para execução e rollback.
/// </summary>
internal sealed record ParentChange(string Id, BsonValue OldParent, BsonValue NewParent);

/// <summary>
/// Linha do plano de migração, gravada em arquivo antes de qualquer gravação.
/// </summary>
internal sealed record PlanRow(string Id, string NodePath, string OldParent, string NewParent, bool IsTitle);
