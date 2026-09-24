using System.Globalization;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MulletaFlix.Tools.NebulaNovelaMigration;

/// <summary>
/// Ferramenta operacional de manutenção do catálogo do Nebula (MongoDB, base ftp, coleção files).
/// Move pastas de títulos identificados como novelas da raiz 'Series' para a raiz 'Novelas'.
/// Somente o campo 'parent' dos nós afetados é alterado: IDs, partes do Telegram, local_path
/// e os arquivos físicos permanecem intactos.
/// </summary>
internal static class Program
{
    private const string DefaultConnectionString = "mongodb://localhost:27017";
    private const string DefaultDatabase = "ftp";
    private const string DefaultCollection = "files";
    private const string SeriesPath = "raphael/Series";
    private const string DefaultTargetRoot = "Novelas";

    /// <summary>Caminho virtual da raiz de destino (ex.: 'raphael/Animações').</summary>
    private static string TargetPathFor(string rootName) => $"raphael/{rootName}";

    /// <summary>
    /// Ponto de entrada. Comandos suportados: audit, node, backup, apply, verify, rollback, normalize-animacoes.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var arguments = ParseArguments(args[1..]);
        var connectionString = GetArgument(arguments, "connection", DefaultConnectionString);
        var database = GetArgument(arguments, "database", DefaultDatabase);
        var collection = GetArgument(arguments, "collection", DefaultCollection);

        var client = new MongoClient(connectionString);
        var mongoDatabase = client.GetDatabase(database);
        var files = mongoDatabase.GetCollection<BsonDocument>(collection);

        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "audit":
                    return await AuditAsync(files, GetRequiredArgument(arguments, "list"), GetTargetRoot(arguments)).ConfigureAwait(false);
                case "node":
                    return await NodeAsync(files, GetRequiredArgument(arguments, "path")).ConfigureAwait(false);
                case "backup":
                    return await BackupAsync(mongoDatabase, files, arguments).ConfigureAwait(false);
                case "apply":
                    return await ApplyAsync(files, arguments).ConfigureAwait(false);
                case "verify":
                    return await VerifyAsync(files, GetRequiredArgument(arguments, "list"), GetTargetRoot(arguments)).ConfigureAwait(false);
                case "rollback":
                    return await RollbackAsync(files, GetRequiredArgument(arguments, "map")).ConfigureAwait(false);
                case "normalize-animacoes":
                    return await NormalizeAnimacoesAsync(files).ConfigureAwait(false);
                default:
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[ERRO] {exception.Message}");
            return 1;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Uso: NebulaNovelaMigration <comando> [opções]

            Comandos:
              audit     Analisa a lista de pastas e o estado atual da árvore (somente leitura).
              node      Imprime o documento bruto de um nó pelo caminho virtual.
              backup    Copia a coleção inteira para uma coleção de backup e para um arquivo JSONL.
              apply     Move as pastas da lista para a raiz de destino (idempotente).
              verify    Confere o resultado da migração (somente leitura).
              rollback  Desfaz uma migração usando o mapa gerado pelo 'apply'.

            Opções comuns:
              --list <arquivo>         Arquivo com um nome de pasta por linha.
              --path <caminho>         Caminho virtual de um nó (ex.: raphael/Series).
              --root <nome>            Raiz de destino sob a raiz do drive (padrão: Novelas).
              --map <arquivo>          Mapa de rollback (JSON) gravado pelo 'apply'.
              --plan <arquivo>         Plano de alterações gravado em TSV.
              --out <diretorio>        Diretório de saída do backup.
              --connection <uri>       URI do MongoDB (padrão: mongodb://localhost:27017).
              --database <nome>        Base de dados (padrão: ftp).
              --collection <nome>      Coleção (padrão: files).
              --dry-run                Mostra o que seria feito sem gravar nada.
            """);
    }

    // ---------------------------------------------------------------- audit

    private static async Task<int> NormalizeAnimacoesAsync(IMongoCollection<BsonDocument> files)
    {
        var documents = await files.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync().ConfigureAwait(false);
        static string ParentKey(BsonDocument doc)
            => doc.TryGetValue("parent", out var parent) && !parent.IsBsonNull ? parent.ToString() : string.Empty;

        var moved = 0;
        var removed = 0;
        foreach (var series in documents.Where(d => d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
            && d.GetValue("name", string.Empty).AsString.Equals("Series", StringComparison.OrdinalIgnoreCase)))
        {
            var parent = ParentKey(series);
            var target = documents.FirstOrDefault(d => d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                && d.GetValue("name", string.Empty).AsString.Equals("Animações", StringComparison.OrdinalIgnoreCase)
                && ParentKey(d).Equals(parent, StringComparison.OrdinalIgnoreCase));
            var duplicate = documents.FirstOrDefault(d => d.GetValue("type", string.Empty).AsString.Equals("dir", StringComparison.OrdinalIgnoreCase)
                && d.GetValue("name", string.Empty).AsString.Equals("Animações", StringComparison.OrdinalIgnoreCase)
                && ParentKey(d).Equals(series.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase));
            if (target == null || duplicate == null)
            {
                continue;
            }

            foreach (var child in documents.Where(d => ParentKey(d).Equals(duplicate.GetValue("_id").ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                var update = await files.UpdateOneAsync(
                    Builders<BsonDocument>.Filter.Eq("_id", child.GetValue("_id")),
                    Builders<BsonDocument>.Update.Set("parent", target.GetValue("_id"))
                        .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())).ConfigureAwait(false);
                moved += (int)update.ModifiedCount;
            }

            var delete = await files.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", duplicate.GetValue("_id"))).ConfigureAwait(false);
            removed += (int)delete.DeletedCount;
        }

        Console.WriteLine($"Animações normalizadas: {moved} grupo(s) movido(s), {removed} nó(s) duplicado(s) removido(s).");
        return 0;
    }

    /// <summary>
    /// Analisa a lista de pastas candidatas contra a árvore real: existência, pai atual,
    /// quantidade de descendentes e quantos descendentes usam caminho legado como pai.
    /// </summary>
    private static async Task<int> AuditAsync(
        IMongoCollection<BsonDocument> files,
        string listPath,
        string rootName)
    {
        var tree = await NebulaTree.LoadAsync(files).ConfigureAwait(false);
        var series = tree.FindByPath(SeriesPath);
        if (series is null)
        {
            Console.Error.WriteLine($"[ERRO] Raiz '{SeriesPath}' não encontrada na coleção.");
            return 1;
        }

        var targetPath = TargetPathFor(rootName);
        var target = tree.FindByPath(targetPath);
        Console.WriteLine($"Raiz 'Series'  : id={series.Id} parent={DescribeParent(series.Document)}");
        Console.WriteLine($"Raiz '{rootName}' : {(target is null ? "não existe (será criada)" : $"id={target.Id} parent={DescribeParent(target.Document)}")}");
        Console.WriteLine($"Documentos na coleção: {tree.Count}");
        Console.WriteLine();

        var names = await File.ReadAllLinesAsync(listPath).ConfigureAwait(false);
        names = names.Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var missing = new List<string>();
        var alreadyMoved = new List<string>();
        var legacyDescendants = 0;
        var objectIdDescendants = 0;
        long totalDescendants = 0;

        Console.WriteLine($"{"Pasta",-52} {"Pai atual",-28} {"Desc.",6} {"Legado",7} {"ObjId",6}");
        Console.WriteLine(new string('-', 105));

        foreach (var name in names)
        {
            var node = tree.FindByPath($"{SeriesPath}/{name}") ?? tree.FindByPath($"{targetPath}/{name}");
            if (node is null)
            {
                missing.Add(name);
                continue;
            }

            if (node.Path.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
            {
                alreadyMoved.Add(name);
            }

            var descendants = tree.GetDescendants(node);
            var legacy = descendants.Count(static descendant => descendant.ParentKind == ParentKind.Path);
            var objectIds = descendants.Count(static descendant => descendant.ParentKind == ParentKind.ObjectId);
            totalDescendants += descendants.Count;
            legacyDescendants += legacy;
            objectIdDescendants += objectIds;

            Console.WriteLine(
                $"{Truncate(name, 52),-52} {DescribeParent(node.Document),-28} {descendants.Count,6} {legacy,7} {objectIds,6}");
        }

        Console.WriteLine();
        Console.WriteLine($"Títulos na lista............: {names.Length}");
        Console.WriteLine($"  já em {rootName}...........: {alreadyMoved.Count}");
        Console.WriteLine($"  ausentes na árvore.......: {missing.Count}");
        Console.WriteLine($"Descendentes analisados....: {totalDescendants} (pai legado: {legacyDescendants}, pai ObjectId: {objectIdDescendants})");

        if (alreadyMoved.Count > 0)
        {
            Console.WriteLine($"  já migrados: {string.Join(", ", alreadyMoved.Take(10))}{(alreadyMoved.Count > 10 ? " ..." : string.Empty)}");
        }

        if (missing.Count > 0)
        {
            Console.WriteLine($"  ausentes: {string.Join(", ", missing.Take(20))}{(missing.Count > 20 ? " ..." : string.Empty)}");
        }

        return missing.Count == 0 ? 0 : 2;
    }

    // ----------------------------------------------------------------- node

    /// <summary>
    /// Imprime o documento bruto de um nó e os filhos diretos resolvidos pelo mesmo
    /// critério usado pelo NebulaFileSystem.
    /// </summary>
    private static async Task<int> NodeAsync(IMongoCollection<BsonDocument> files, string path)
    {
        var tree = await NebulaTree.LoadAsync(files).ConfigureAwait(false);
        var node = tree.FindByPath(path);
        if (node is null)
        {
            Console.Error.WriteLine($"[ERRO] Nó não encontrado: {path}");
            return 1;
        }

        Console.WriteLine(node.Document.ToJson(new MongoDB.Bson.IO.JsonWriterSettings { Indent = true }));
        Console.WriteLine();
        Console.WriteLine($"--- Filhos diretos de '{path}' (critério do NebulaFileSystem) ---");
        foreach (var child in tree.GetResolvableChildren(node))
        {
            Console.WriteLine($"  {child.Name,-60} {child.Kind,-8} parent={DescribeParent(child.Document)}");
        }

        return 0;
    }

    // --------------------------------------------------------------- backup

    /// <summary>
    /// Copia a coleção inteira para uma coleção de backup e grava um snapshot JSONL.
    /// A coleção original nunca é tocada por este comando.
    /// </summary>
    private static async Task<int> BackupAsync(
        IMongoDatabase database,
        IMongoCollection<BsonDocument> files,
        IReadOnlyDictionary<string, string> arguments)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var outputDirectory = GetArgument(arguments, "out", Path.Combine("artifacts", $"backup-{timestamp}"));
        Directory.CreateDirectory(outputDirectory);

        var documents = await files.Find(FilterDefinition<BsonDocument>.Empty).ToListAsync().ConfigureAwait(false);
        var snapshotPath = Path.Combine(outputDirectory, "files.jsonl");
        await using (var stream = File.Create(snapshotPath))
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            foreach (var document in documents)
            {
                await writer.WriteLineAsync(document.ToJson()).ConfigureAwait(false);
            }
        }

        var backupCollectionName = GetArgument(arguments, "backup-collection", $"files_backup_{timestamp}");
        var backupCollection = database.GetCollection<BsonDocument>(backupCollectionName);
        foreach (var batch in documents.Chunk(1000))
        {
            await backupCollection.InsertManyAsync(batch, new InsertManyOptions { IsOrdered = false }).ConfigureAwait(false);
        }

        var persisted = await backupCollection.CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
        Console.WriteLine($"Backup: {documents.Count} documentos copiados para '{backupCollectionName}' ({persisted} confirmados).");
        Console.WriteLine($"Snapshot JSONL: {snapshotPath}");
        return documents.Count == (int)persisted ? 0 : 1;
    }

    // ---------------------------------------------------------------- apply

    /// <summary>
    /// Executa a migração: cria/reaproveita a raiz de destino e reaponta o campo 'parent'
    /// das pastas da lista, incluindo descendentes que usam caminho legado como pai.
    /// </summary>
    private static async Task<int> ApplyAsync(
        IMongoCollection<BsonDocument> files,
        IReadOnlyDictionary<string, string> arguments)
    {
        var listPath = GetRequiredArgument(arguments, "list");
        var rootName = GetTargetRoot(arguments);
        var targetPath = TargetPathFor(rootName);
        var slug = Slug(rootName);
        var dryRun = arguments.ContainsKey("dry-run");
        var tree = await NebulaTree.LoadAsync(files).ConfigureAwait(false);
        var series = tree.FindByPath(SeriesPath)
            ?? throw new InvalidOperationException($"Raiz '{SeriesPath}' não encontrada.");

        var names = (await File.ReadAllLinesAsync(listPath).ConfigureAwait(false))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var target = tree.FindByPath(targetPath);
        if (target is null)
        {
            var document = new BsonDocument
            {
                { "_id", ObjectId.GenerateNewId() },
                { "name", rootName },
                { "type", "dir" },
                { "is_directory", true },
                { "status", "completed" },
                { "parent", series.Document.TryGetValue("parent", out var parent) ? parent : BsonNull.Value },
                { "created_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
                { "modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds() }
            };

            Console.WriteLine($"[CRIAR] raiz '{rootName}' parent={DescribeParentValue(document["parent"])}");
            if (!dryRun)
            {
                await files.InsertOneAsync(document).ConfigureAwait(false);
            }

            var created = new NebulaNode(document, targetPath);
            tree.Add(created);
            target = created;
        }
        else
        {
            Console.WriteLine($"[OK] raiz '{rootName}' já existe (id={target.Id})");
        }

        var moves = new List<ParentChange>();
        var plan = new List<PlanRow>();
        var skipped = new List<string>();
        var missing = new List<string>();
        var orphanRisk = new List<string>();
        var titleMoves = 0;

        foreach (var name in names)
        {
            var node = tree.FindByPath($"{SeriesPath}/{name}");
            if (node is null)
            {
                if (tree.FindByPath($"{targetPath}/{name}") is not null)
                {
                    skipped.Add(name);
                }
                else
                {
                    missing.Add(name);
                }

                continue;
            }

            // O nó do título passa a apontar para o ObjectId da raiz de destino.
            moves.Add(new ParentChange(node.Id, node.Document.GetValue("parent", BsonNull.Value), target.Document["_id"]));
            plan.Add(new PlanRow(node.Id, node.Path, node.ParentText, $"ObjectId:{target.Id}", true));
            titleMoves++;

            // Descendentes cujo 'parent' é caminho legado precisam acompanhar o novo prefixo,
            // senão ficam inalcançáveis para a resolução por caminho virtual.
            foreach (var descendant in tree.GetDescendants(node))
            {
                if (descendant.ParentKind != ParentKind.Path)
                {
                    continue;
                }

                var current = descendant.ParentText.Replace('\\', '/');
                if (!TryRewriteLegacyParent(current, node.Name, rootName, out var rewritten))
                {
                    // Caminho legado fora do padrão esperado: reportar em vez de deixar órfão.
                    orphanRisk.Add($"{descendant.Path} (parent={current})");
                    continue;
                }

                moves.Add(new ParentChange(descendant.Id, descendant.Document.GetValue("parent", BsonNull.Value), rewritten));
                plan.Add(new PlanRow(descendant.Id, descendant.Path, current, rewritten, false));
            }
        }

        var planPath = GetArgument(arguments, "plan", Path.Combine("artifacts", $"{slug}-plan.tsv"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(planPath))!);
        await File.WriteAllLinesAsync(
            planPath,
            new[] { "id\tnodePath\toldParent\tnewParent\tisTitle" }
                .Concat(plan.Select(static row => $"{row.Id}\t{row.NodePath}\t{row.OldParent}\t{row.NewParent}\t{row.IsTitle}"))).ConfigureAwait(false);

        Console.WriteLine($"Títulos a mover..........: {titleMoves}");
        Console.WriteLine($"Alterações de 'parent'...: {moves.Count} ({titleMoves} títulos + {moves.Count - titleMoves} descendentes com pai legado)");
        Console.WriteLine($"Já em {rootName}............: {skipped.Count}");
        Console.WriteLine($"Ausentes.................: {missing.Count}");
        Console.WriteLine($"Plano gravado em.........: {planPath}");

        if (orphanRisk.Count > 0)
        {
            Console.Error.WriteLine($"[AVISO] {orphanRisk.Count} descendente(s) com caminho legado não reconhecido:");
            foreach (var risk in orphanRisk.Take(20))
            {
                Console.Error.WriteLine($"  - {risk}");
            }
        }

        if (missing.Count > 0)
        {
            Console.Error.WriteLine($"[ERRO] Pastas não encontradas: {string.Join(", ", missing.Take(20))}");
            return 2;
        }

        if (dryRun)
        {
            Console.WriteLine("[DRY-RUN] Nenhuma alteração foi gravada.");
            foreach (var change in moves.Take(10))
            {
                Console.WriteLine($"  {change.Id}: {DescribeParentValue(change.OldParent)} -> {DescribeParentValue(change.NewParent)}");
            }

            return 0;
        }

        var applied = new List<ParentChange>();
        foreach (var change in moves)
        {
            // O _id é sempre ObjectId no catálogo; filtrar por string não casa com o documento.
            var result = await files.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(change.Id)),
                Builders<BsonDocument>.Update
                    .Set("parent", change.NewParent)
                    .Set("modified_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())).ConfigureAwait(false);

            if (result.ModifiedCount > 0)
            {
                applied.Add(change);
            }
            else if (result.MatchedCount == 0)
            {
                Console.Error.WriteLine($"[AVISO] Documento não encontrado para atualização: {change.Id}");
            }
        }

        var mapPath = GetArgument(
            arguments,
            "map",
            Path.Combine("artifacts", $"{slug}-rollback-{DateTime.Now:yyyyMMdd-HHmmss}.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(mapPath))!);
        var map = new BsonDocument
        {
            { "createdAt", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) },
            { "targetRootName", rootName },
            { "targetRootId", ObjectId.Parse(target.Id) },
            { "changes", new BsonArray(applied.Select(static change => new BsonDocument
                {
                    { "id", ObjectId.Parse(change.Id) },
                    { "old", change.OldParent },
                    { "new", change.NewParent }
                })) }
        };
        await File.WriteAllTextAsync(mapPath, map.ToJson()).ConfigureAwait(false);

        Console.WriteLine($"Alterações efetivadas....: {applied.Count}");
        Console.WriteLine($"Mapa de rollback.........: {mapPath}");
        return 0;
    }

    // --------------------------------------------------------------- verify

    /// <summary>
    /// Confere o estado final: raiz de destino visível na raiz do drive, títulos migrados,
    /// nenhum título remanescente em 'Series' e nenhum descendente inalcançável.
    /// </summary>
    private static async Task<int> VerifyAsync(IMongoCollection<BsonDocument> files, string listPath, string rootName)
    {
        var targetPath = TargetPathFor(rootName);
        var tree = await NebulaTree.LoadAsync(files).ConfigureAwait(false);
        var series = tree.FindByPath(SeriesPath)
            ?? throw new InvalidOperationException($"Raiz '{SeriesPath}' não encontrada.");
        var target = tree.FindByPath(targetPath)
            ?? throw new InvalidOperationException($"Raiz '{targetPath}' não encontrada.");

        var names = (await File.ReadAllLinesAsync(listPath).ConfigureAwait(false))
            .Select(static line => line.Trim())
            .Where(static line => line.Length > 0 && !line.StartsWith('#'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var problems = new List<string>();

        // 1. A raiz de destino precisa ser listada junto de Series na raiz do drive,
        //    ou seja, ser irmã de Series (mesmo pai).
        var rootChildren = tree.GetResolvableChildren(tree.Root);
        if (!rootChildren.Any(child => child.Name.Equals(rootName, StringComparison.OrdinalIgnoreCase)))
        {
            problems.Add($"A raiz '{rootName}' não aparece na listagem da raiz do drive (parent incompatível).");
        }

        if (!string.Equals(series.ParentText, target.ParentText, StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"A raiz '{rootName}' (parent={target.ParentText}) não é irmã de 'Series' (parent={series.ParentText}).");
        }

        var targetRoots = tree.Nodes.Count(node =>
            node.IsDirectory
            && node.Name.Equals(rootName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(node.ParentText, target.ParentText, StringComparison.OrdinalIgnoreCase));
        if (targetRoots != 1)
        {
            problems.Add($"Existem {targetRoots} nós '{rootName}' no mesmo nível de 'Series' (esperado: 1).");
        }

        // 2. Cada título deve estar na raiz de destino, visível, e fora de Series.
        var moved = 0;
        var unreachable = 0;
        foreach (var name in names)
        {
            var inTarget = tree.FindByPath($"{targetPath}/{name}");
            if (inTarget is null)
            {
                problems.Add($"Título não encontrado em {rootName}: {name}");
                continue;
            }

            if (!IsVisibleUnder(target, inTarget))
            {
                problems.Add($"Título invisível sob {rootName}: {name}");
                continue;
            }

            if (tree.FindByPath($"{SeriesPath}/{name}") is not null)
            {
                problems.Add($"Título ainda presente em Series: {name}");
                continue;
            }

            moved++;

            var descendants = tree.GetDescendants(inTarget);
            foreach (var descendant in descendants)
            {
                if (descendant.ParentKind != ParentKind.Path)
                {
                    continue;
                }

                var parent = descendant.ParentText.Replace('\\', '/');
                if (parent.Contains("/Series/", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"Descendente ainda aponta para Series: {descendant.Path} (parent={parent})");
                    unreachable++;
                }
            }
        }

        // 3. Duplicidade de nome entre irmãos derruba a listagem (o filesystem mantém o primeiro).
        var duplicates = tree.GetResolvableChildren(target)
            .GroupBy(static child => child.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            problems.Add($"Nomes duplicados em {rootName}: {string.Join(", ", duplicates.Take(10))}");
        }

        var seriesChildren = tree.GetResolvableChildren(series);
        var targetChildren = tree.GetResolvableChildren(target);
        Console.WriteLine($"Raiz '{rootName}'...........: id={target.Id} parent={DescribeParent(target.Document)}");
        Console.WriteLine($"Títulos migrados.........: {moved}/{names.Length}");
        Console.WriteLine($"Filhos em Series.........: {seriesChildren.Count} documentos / {seriesChildren.Select(static child => child.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()} nomes distintos");
        Console.WriteLine($"Filhos em {rootName}........: {targetChildren.Count} documentos / {targetChildren.Select(static child => child.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()} nomes distintos");
        Console.WriteLine($"Descendentes com pai legado apontando para Series: {unreachable}");

        if (problems.Count == 0)
        {
            Console.WriteLine("VERIFICAÇÃO: OK — nenhum problema encontrado.");
            return 0;
        }

        Console.WriteLine($"VERIFICAÇÃO: {problems.Count} problema(s).");
        foreach (var problem in problems.Take(40))
        {
            Console.WriteLine($"  - {problem}");
        }

        return 2;
    }

    // ------------------------------------------------------------- rollback

    /// <summary>
    /// Restaura o campo 'parent' de cada documento alterado usando o mapa gerado pelo 'apply'.
    /// </summary>
    private static async Task<int> RollbackAsync(IMongoCollection<BsonDocument> files, string mapPath)
    {
        var map = BsonDocument.Parse(await File.ReadAllTextAsync(mapPath).ConfigureAwait(false));
        var changes = map["changes"].AsBsonArray;
        var restored = 0;

        foreach (var change in changes)
        {
            var document = change.AsBsonDocument;
            var id = document["id"].AsObjectId;
            var oldParent = document["old"];
            var result = await files.UpdateOneAsync(
                Builders<BsonDocument>.Filter.Eq("_id", id),
                Builders<BsonDocument>.Update.Set("parent", oldParent)).ConfigureAwait(false);
            if (result.ModifiedCount > 0)
            {
                restored++;
            }
        }

        Console.WriteLine($"Rollback concluído: {restored} de {changes.Count} documento(s) restaurados.");
        Console.WriteLine("A raiz de destino criada pela migração não é removida automaticamente; remova-a se ficar vazia.");
        return 0;
    }

    // -------------------------------------------------------------- helpers

    /// <summary>
    /// Verifica se <paramref name="child"/> é resolvível como filho direto de
    /// <paramref name="parent"/> pelo critério do NebulaFileSystem.
    /// </summary>
    private static bool IsVisibleUnder(NebulaNode parent, NebulaNode child)
    {
        foreach (var candidate in parent.ParentKeysForChildren())
        {
            if (string.Equals(child.ParentText, candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reescreve um caminho legado de 'Series' para o equivalente na raiz de destino,
    /// preservando o restante da estrutura (ex.: subpastas de temporada).
    /// </summary>
    private static bool TryRewriteLegacyParent(string parent, string title, string rootName, out string rewritten)
    {
        rewritten = string.Empty;
        var normalized = parent.Trim('/');
        foreach (var prefix in new[] { $"raphael/Series/{title}", $"Series/{title}" })
        {
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rest = normalized[prefix.Length..].TrimEnd('/');
            rewritten = $"/{TargetPathFor(rootName)}/{title}{rest}";
            return true;
        }

        return false;
    }

    /// <summary>
    /// Converte o nome da raiz em um identificador simples para nomes de arquivo
    /// (ex.: 'Animações' -> 'animacoes').
    /// </summary>
    private static string Slug(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string DescribeParent(BsonDocument document)
        => DescribeParentValue(document.GetValue("parent", BsonNull.Value));

    private static string DescribeParentValue(BsonValue value)
        => value.IsBsonNull ? "(null)" : value.IsObjectId ? $"ObjectId:{value.AsObjectId}" : value.ToString() ?? string.Empty;

    private static string Truncate(string value, int length)
        => value.Length <= length ? value : value[..(length - 1)] + "…";

    private static Dictionary<string, string> ParseArguments(IEnumerable<string> arguments)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? pending = null;
        foreach (var argument in arguments)
        {
            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                if (pending is not null)
                {
                    parsed[pending] = "true";
                }

                pending = argument[2..];
                continue;
            }

            if (pending is not null)
            {
                parsed[pending] = argument;
                pending = null;
            }
        }

        if (pending is not null)
        {
            parsed[pending] = "true";
        }

        return parsed;
    }

    private static string GetTargetRoot(IReadOnlyDictionary<string, string> arguments)
        => GetArgument(arguments, "root", DefaultTargetRoot);

    private static string GetArgument(IReadOnlyDictionary<string, string> arguments, string name, string fallback)
        => arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static string GetRequiredArgument(IReadOnlyDictionary<string, string> arguments, string name)
        => arguments.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Opção obrigatória ausente: --{name}");
}
