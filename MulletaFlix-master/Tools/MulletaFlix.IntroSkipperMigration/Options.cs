// SPDX-FileCopyrightText: 2026 MulletaFlix
// SPDX-License-Identifier: GPL-3.0-only

namespace MulletaFlix.Tools.IntroSkipperMigration;

/// <summary>
/// Options of the migration. Every default matches what the server writes into
/// <c>config/database.xml</c> for the plugin schema.
/// </summary>
public sealed class Options
{
    /// <summary>Gets the path of the legacy <c>introskipper-v2.db</c>.</summary>
    public string? SegmentDatabasePath { get; init; }

    /// <summary>Gets the path of the legacy <c>introskipper-cache.db</c>.</summary>
    public string? CacheDatabasePath { get; init; }

    /// <summary>Gets the MariaDB host.</summary>
    public string Server { get; init; } = "127.0.0.1";

    /// <summary>Gets the MariaDB port.</summary>
    public string Port { get; init; } = "3306";

    /// <summary>Gets the MariaDB user.</summary>
    public string User { get; init; } = "root";

    /// <summary>Gets the MariaDB password.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Gets the target schema of the plugin.</summary>
    public string DatabaseName { get; init; } = "mulletaflix_introskipper";

    /// <summary>Gets a value indicating whether nothing is written and only counts are reported.</summary>
    public bool DryRun { get; init; }

    /// <summary>Gets a value indicating whether the detection cache is migrated too.</summary>
    public bool IncludeCache { get; init; }

    /// <summary>Gets a value indicating whether usage should be printed.</summary>
    public bool ShowHelp { get; init; }

    /// <summary>Gets the usage text.</summary>
    public static string Usage =>
        """
        Uso:
          mulletaflix-introskipper-migrate --segment-db <caminho> [opções]

        Origem (ao menos uma):
          --segment-db <caminho>    introskipper-v2.db (segmentos, temporadas, análise, flags)
          --cache-db <caminho>      introskipper-cache.db (cache de detecção do FFmpeg)
          --include-cache           migra também o cache informado em --cache-db

        Destino (padrões iguais aos do servidor):
          --server <host>           padrão 127.0.0.1
          --port <porta>            padrão 3306
          --user <usuário>          padrão root
          --password <senha>        padrão vazio
          --database <schema>       padrão mulletaflix_introskipper

        Comportamento:
          --dry-run                 conta o que seria migrado, sem gravar nada
          --help                    mostra esta ajuda

        A migração é idempotente: linhas já existentes no destino nunca são sobrescritas.
        Os arquivos de origem são abertos somente para leitura e não são alterados.
        """;

    /// <summary>Parses the command line.</summary>
    /// <param name="args">Raw arguments.</param>
    /// <returns>The parsed options.</returns>
    /// <exception cref="ArgumentException">An argument is unknown or has no value.</exception>
    public static Options Parse(string[] args)
    {
        string? segment = null;
        string? cache = null;
        var server = "127.0.0.1";
        var port = "3306";
        var user = "root";
        var password = string.Empty;
        var database = "mulletaflix_introskipper";
        var dryRun = false;
        var includeCache = false;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            string Next()
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"'{arg}' exige um valor.");
                }

                return args[++i];
            }

            switch (arg)
            {
                case "--segment-db": segment = Next(); break;
                case "--cache-db": cache = Next(); break;
                case "--server": server = Next(); break;
                case "--port": port = Next(); break;
                case "--user": user = Next(); break;
                case "--password": password = Next(); break;
                case "--database": database = Next(); break;
                case "--dry-run": dryRun = true; break;
                case "--include-cache": includeCache = true; break;
                case "--help" or "-h": help = true; break;
                default:
                    throw new ArgumentException($"argumento desconhecido: '{arg}'");
            }
        }

        if (includeCache && cache is null)
        {
            throw new ArgumentException("'--include-cache' exige '--cache-db'.");
        }

        return new Options
        {
            SegmentDatabasePath = segment,
            CacheDatabasePath = cache,
            Server = server,
            Port = port,
            User = user,
            Password = password,
            DatabaseName = database,
            DryRun = dryRun,
            IncludeCache = includeCache,
            ShowHelp = help,
        };
    }
}
