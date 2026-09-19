using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.Nebula;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using Moq;
using MulletaFlix.Server.Implementations.Nebula;
using Xunit;

namespace MulletaFlix.Server.Implementations.Tests.Nebula;

public class NebulaUploadEngineTests
{
    [Fact]
    public void TelegramPool_ParsesBotApiUploadResult()
    {
        const string ResponseJson = """
            {"ok":true,"result":{"message_id":321,"chat":{"id":-1004391811380},"document":{"file_id":"telegram-file-id"}}}
            """;
        var parseMethod = typeof(NebulaTelegramPool).GetMethod(
            "ParseUploadResult",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(parseMethod);
        var result = Assert.IsType<NebulaTelegramUploadResult>(parseMethod.Invoke(null, [ResponseJson]));
        Assert.Equal(321, result.MessageId);
        Assert.Equal(-1004391811380L, result.ChatId);
        Assert.Equal("telegram-file-id", result.FileId);
    }

    [Theory]
    [InlineData("stream-secret", "stream-secret", true)]
    [InlineData("stream-secret", "stream-secret-2", false)]
    [InlineData("stream-secret", "", false)]
    [InlineData("", "stream-secret", false)]
    public void HttpStreamServer_ComparesTokensWithoutAcceptingEmptyValues(string expected, string provided, bool shouldAuthorize)
    {
        var compareMethod = typeof(NebulaHttpStreamServer).GetMethod(
            "AreTokensEqual",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(compareMethod);
        var actual = Assert.IsType<bool>(compareMethod.Invoke(null, [expected, provided]));

        Assert.Equal(shouldAuthorize, actual);
    }

    [Fact]
    public void MongoContext_PathContainment_RejectsSiblingAndTraversalPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "nebula-root");
        var nested = Path.Combine(root, "nested", "movie.mkv");
        var sibling = root + "-other\\movie.mkv";

        Assert.True(NebulaMongoContext.IsPathWithinRoot(nested, root));
        Assert.False(NebulaMongoContext.IsPathWithinRoot(Path.Combine(root, "..", "movie.mkv"), root));
        Assert.False(NebulaMongoContext.IsPathWithinRoot(sibling, root));
    }

    [Fact]
    public void Constructor_UsesSixteenMegabyteLogicalChunks()
    {
        using var engine = new NebulaUploadEngine(
            null!,
            null!,
            uploadConcurrency: 1,
            chunkSizeMb: 64,
            deleteSourceAfterUpload: false,
            NullLogger<NebulaUploadEngine>.Instance);

        var logicalChunkField = typeof(NebulaUploadEngine).GetField("_logicalChunkSizeBytes", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(logicalChunkField);
        Assert.Equal(16 * 1024 * 1024, logicalChunkField.GetValue(engine));
    }

    [Theory]
    [InlineData(-1004391811380L, 4391811380L)]
    [InlineData(-1001234567890L, 1234567890L)]
    [InlineData(-12345L, 12345L)]
    [InlineData(4391811380L, 4391811380L)]
    public void TelegramPool_NormalizesChannelId_ForMTProto(long inputChatId, long expectedBareId)
    {
        var normalizeMethod = typeof(NebulaTelegramPool).GetMethod(
            "NormalizeChannelId",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(normalizeMethod);
        var actual = normalizeMethod.Invoke(null, [inputChatId]);
        Assert.Equal(expectedBareId, actual);
    }

    [Fact]
    public void TelegramPool_SanitizesBotTokens_FromExceptionMessages()
    {
        var sanitizeMethod = typeof(NebulaTelegramPool).GetMethod(
            "SanitizeMessage",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(sanitizeMethod);
        var tokens = new[] { "123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11", "987654:XYZ-UVW9876" };
        var rawMessage = "HttpRequestException: Error calling https://api.telegram.org/bot123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11/sendDocument failed.";

        var sanitized = (string?)sanitizeMethod.Invoke(null, [rawMessage, tokens]);

        Assert.NotNull(sanitized);
        Assert.DoesNotContain("123456:ABC-DEF1234ghIkl-zyx57W2v1u123ew11", sanitized, System.StringComparison.Ordinal);
        Assert.Contains("[REDACTED_TOKEN]", sanitized, System.StringComparison.Ordinal);
    }

    [Fact]
    public void TelegramPool_HelpersLog_SuppressesReceivingUpdates()
    {
        // Ensure static constructor was triggered
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(NebulaTelegramPool).TypeHandle);

        Assert.NotNull(WTelegram.Helpers.Log);
        // Invoking Log with "Receiving Updates" should not throw and should be safely handled
        WTelegram.Helpers.Log(1, "Receiving Updates                                  2026-08-31 23:01:52Z");
        WTelegram.Helpers.Log(1, string.Empty);
    }

    [Fact]
    public void TelegramPool_RewindsOutputAfterFailedDownloadAttempt()
    {
        var captureMethod = typeof(NebulaTelegramPool).GetMethod(
            "CaptureOutputPosition",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        var rewindMethod = typeof(NebulaTelegramPool).GetMethod(
            "RewindOutputToPosition",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.NotNull(captureMethod);
        Assert.NotNull(rewindMethod);

        using var output = new MemoryStream();
        output.WriteByte(1);
        var position = captureMethod.Invoke(null, [output]);
        output.Write([2, 3, 4]);

        rewindMethod.Invoke(null, [output, position]);

        Assert.Equal(1, output.Length);
        Assert.Equal(1, output.Position);
        Assert.Equal([1], output.ToArray());
    }

    [Fact]
    public void UploadEngine_BuildPartCaption_FormatsCorrectly()
    {
        // 1757.2 MB in bytes = (long)(1757.2 * 1024 * 1024) = 1842571264
        long totalSize = (long)(1757.2 * 1024 * 1024);
        var caption = NebulaUploadEngine.BuildPartCaption(
            mediaType: "FILME",
            filename: "Gosto Infernal (2025).mkv",
            partNum: 79, // 0-indexed part 79 is Part 80
            totalParts: 110,
            fileUuid: "793b8f4e-814d-4a09-9076-8537856292f4",
            totalSize: totalSize);

        Assert.Equal("[NEBULA] TIPO: FILME | MIDIA: Gosto Infernal (2025).mkv | PARTE: 80/110 | UUID: 793b8f4e-814d-4a09-9076-8537856292f4 | TAM: 1757.2MB", caption);
    }

    [Theory]
    [InlineData("Filmes", "Gosto Infernal (2025).mkv", "FILME")]
    [InlineData("Series/Breaking Bad/Season 1", "Breaking Bad S01E01.mkv", "SERIE")]
    [InlineData(null, "Stranger.Things.4x01.mp4", "SERIE")]
    [InlineData("Adulto", "video_xxx.mp4", "PORNO")]
    [InlineData(null, "Hentai Episode 1.mkv", "PORNO")]
    [InlineData("Filmes", "Essex.mkv", "FILME")]
    [InlineData(null, "Sex Education S01E01.mkv", "SERIE")]
    [InlineData("Filmes", "The Matrix.mkv", "FILME")]
    // Títulos de filme que contêm palavras de série não podem virar série
    [InlineData(@"Series\Filmes\O Show dos Muppets (2026)", "O Show dos Muppets (2026).mkv", "FILME")]
    [InlineData(@"Series\Filmes\10x10 - O Cativeiro (2018)", "10x10 - O Cativeiro (2018).mkv", "FILME")]
    [InlineData(@"Series\Filmes\4x100 - Correndo por um Sonho (2021)", "4x100 - Correndo por um Sonho (2021).mkv", "FILME")]
    [InlineData("Temporada de Sangue (2025)", "Temporada de Sangue (2025).mkv", "FILME")]
    [InlineData("Show Bar (2000)", "Show Bar (2000).mkv", "FILME")]
    [InlineData("Casteel Series - Os Sonhos de Heaven (2019)", "Casteel Series - Os Sonhos de Heaven (2019).mkv", "FILME")]
    // Palavra de conteúdo adulto no título não desloca mídia que está numa raiz declarada
    [InlineData(@"Filmes\How to Have Sex (2023)", "How to Have Sex (2023).mkv", "FILME")]
    [InlineData(@"Filmes\Adult Swim_'s The Elephant (2025)", "Adult Swim_'s The Elephant (2025).mkv", "FILME")]
    [InlineData(@"Series\Temporada de Sangue (2025)", "Temporada de Sangue (2025).mkv", "FILME")]
    [InlineData(@"Porno\Studio", "cena.mp4", "PORNO")]
    // Séries continuam sendo séries por marcador de episódio ou pasta de temporada
    [InlineData(@"Series\Series\BoJack Horseman\Season 03", "BoJack Horseman - S03E11.mkv", "SERIE")]
    [InlineData("Fuzuê/Temporada 1", "Fuzuê - Ep 12.mkv", "SERIE")]
    public void UploadEngine_ClassifyMediaType_ClassifiesProperly(string? parent, string filename, string expectedType)
    {
        var actual = NebulaUploadEngine.ClassifyMediaType(parent, filename);
        Assert.Equal(expectedType, actual);
    }

    [Fact]
    public void UploadEngine_CompletedUploadValidation_RequiresExactSizeAndParts()
    {
        var doc = new BsonDocument
        {
            { "size", 32L },
            {
                "parts", new BsonArray
                {
                    new BsonDocument
                    {
                        { "part_number", 0 },
                        { "size", 16L },
                        { "tg_file_id", "part-0" }
                    },
                    new BsonDocument
                    {
                        { "part_number", 1 },
                        { "size", 16L },
                        { "tg_file_id", "part-1" }
                    }
                }
            }
        };

        Assert.True(NebulaUploadEngine.IsCompletedUploadForFile(doc, 32L, 2));
        Assert.False(NebulaUploadEngine.IsCompletedUploadForFile(doc, 33L, 2));
        Assert.False(NebulaUploadEngine.IsCompletedUploadForFile(doc, 32L, 3));
    }

    [Theory]
    [InlineData(0, 16L, "completed", true)]
    [InlineData(1, 16L, "completed", true)]
    [InlineData(1, 15L, "completed", false)]
    [InlineData(2, 16L, "completed", false)]
    [InlineData(0, 16L, "uploading", false)]
    public void UploadEngine_PartialResume_RequiresExpectedPartShape(
        int partNumber,
        long partSize,
        string status,
        bool expected)
    {
        var actual = NebulaUploadEngine.IsReusablePart(
            partNumber,
            partSize,
            "telegram-file",
            status,
            totalSize: 32L,
            totalParts: 2,
            logicalChunkSize: 16);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Downloader_ValidateRangeResponse_RejectsServerThatIgnoresRange()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        response.Content.Headers.ContentLength = 10;

        Assert.Throws<IOException>(() => NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10));
    }

    [Fact]
    public void Downloader_ValidateRangeResponse_AcceptsMatchingPartialContent()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent(new byte[10])
        };
        response.Content.Headers.ContentLength = 10;
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 9, 100);

        NebulaDownloaderEngine.ValidateRangeResponse(response, 0, 9, 100, 10);
    }

    [Fact]
    public void TelegramPool_BotApiChunkValidation_RejectsRangeIgnoredForLaterChunk()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK);

        Assert.False(NebulaTelegramPool.IsValidBotApiChunkResponse(response, 1024, 128, 4096));
    }

    [Fact]
    public void TelegramPool_BotApiChunkValidation_RequiresMatchingContentRange()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 127, 4096);

        Assert.False(NebulaTelegramPool.IsValidBotApiChunkResponse(response, 128, 128, 128));
    }

    [Fact]
    public void TelegramPool_BotApiChunkValidation_AcceptsMatchingPartialContent()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(128, 255, 4096);

        Assert.True(NebulaTelegramPool.IsValidBotApiChunkResponse(response, 128, 128, 128));
    }

    [Fact]
    public void Downloader_RangeProbe_AcceptsPartialContentWhenHeadDidNotAdvertiseRanges()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
        {
            Content = new ByteArrayContent([0])
        };
        response.Content.Headers.ContentLength = 1;
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(0, 0, 100);

        Assert.True(NebulaDownloaderEngine.TryGetRangeProbeLength(response, out var totalSize));
        Assert.Equal(100, totalSize);
    }

    [Fact]
    public void Downloader_RangeProbe_RejectsFullResponse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([0])
        };

        Assert.False(NebulaDownloaderEngine.TryGetRangeProbeLength(response, out var totalSize));
        Assert.Equal(0, totalSize);
    }

    [Fact]
    public void TelegramPool_RetryAfter_IsParsedFromBotApiResponse()
    {
        const string ResponseJson = """
            {"ok":false,"error_code":429,"parameters":{"retry_after":42}}
            """;

        Assert.True(NebulaTelegramPool.TryReadRetryAfter(ResponseJson, out var retryAfter));
        Assert.Equal(42, retryAfter);
    }

    [Fact]
    public void TelegramPool_ChannelAccessHashCandidates_TryPreferredThenFallbacks()
    {
        var candidates = NebulaTelegramPool.GetCandidateChannelAccessHashes(1).Take(3).ToArray();

        Assert.True(candidates.Length >= 2);
        Assert.NotEqual(candidates[0], candidates[1]);
    }

    [Fact]
    public void NebulaConfiguration_DoesNotShipBotTokensOrApiHash()
    {
        var config = new NebulaFtpConfiguration();

        Assert.False(config.Enabled);
        Assert.True(string.IsNullOrEmpty(config.ApiHash));
        Assert.True(string.IsNullOrEmpty(config.BotTokens));
    }

    [Fact]
    public void SupabaseRecord_ConvertsBsonDocumentWithParts_Properly()
    {
        var convertMethod = typeof(NebulaSupabaseSyncService).GetMethod(
            "ConvertBsonDocToSupabaseRecord",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(convertMethod);

        var doc = new MongoDB.Bson.BsonDocument
        {
            { "_id", MongoDB.Bson.ObjectId.GenerateNewId() },
            { "name", "Filme Teste.mkv" },
            { "size", 1024L * 1024L * 32L },
            { "status", "completed" },
            {
                "parts", new MongoDB.Bson.BsonArray
                {
                    new MongoDB.Bson.BsonDocument
                    {
                        { "part_number", 0 },
                        { "size", 16 * 1024 * 1024 },
                        { "tg_file_id", "tg-123" }
                    }
                }
            }
        };

        var record = convertMethod.Invoke(null, [doc]);
        Assert.NotNull(record);
    }

    [Fact]
    public void TelegramFileIdDecoder_DecodesRealTelegramFileId_Successfully()
    {
        // FileId real de parte armazenada no MongoDB ftp.files
        const string FileId = "BQACAgEAAyEFAAMBBcW5NAABAaLoapDQ5PR_-d25JlQMDLWKgEvcmj8AAq8KAALr0llEaob3dkC0G2geBA";

        var doc = TelegramFileIdDecoder.DecodeDocument(FileId);

        Assert.NotNull(doc);
        Assert.True(doc.id != 0);
        Assert.True(doc.access_hash != 0);
        Assert.NotNull(doc.file_reference);
        Assert.True(doc.file_reference.Length > 0);
        Assert.Equal(1, doc.dc_id);
    }

    /// <summary>
    /// Regression: arquivos sem NodeId (FileSystemWatcher) devem ser bloqueados
    /// pelo claim em memória (_activeFiles) para evitar processamento duplicado.
    /// </summary>
    [Fact]
    public void StagingWatcher_ActiveFilesField_PreventsDoubleProcessingWithoutNodeId()
    {
        var activeFilesField = typeof(NebulaStagingWatcher).GetField(
            "_activeFiles",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.NotNull(activeFilesField);

        // Verifica que o tipo é ConcurrentDictionary<string, byte>
        var fieldType = activeFilesField.FieldType;
        Assert.Equal(typeof(System.Collections.Concurrent.ConcurrentDictionary<string, byte>), fieldType);

        // Cria instância mínima só para acessar o campo
        using var engine = new NebulaUploadEngine(
            null!,
            null!,
            uploadConcurrency: 1,
            chunkSizeMb: 16,
            deleteSourceAfterUpload: false,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NebulaUploadEngine>.Instance);

        var watcher = new NebulaStagingWatcher(engine, Microsoft.Extensions.Logging.Abstractions.NullLogger<NebulaStagingWatcher>.Instance);

        var dict = (System.Collections.Concurrent.ConcurrentDictionary<string, byte>?)activeFilesField.GetValue(watcher);
        Assert.NotNull(dict);

        // Simula dois workers tentando reivindicar o mesmo arquivo
        const string FakePath = @"C:\staging\filme.mkv";
        var firstClaim = dict.TryAdd(FakePath, 0);
        var secondClaim = dict.TryAdd(FakePath, 0);

        Assert.True(firstClaim, "O primeiro worker deve conseguir reivindicar o arquivo.");
        Assert.False(secondClaim, "O segundo worker NÃO deve conseguir reivindicar o mesmo arquivo.");

        // Simula liberação pelo finally do worker
        dict.TryRemove(FakePath, out _);
        var thirdClaim = dict.TryAdd(FakePath, 0);
        Assert.True(thirdClaim, "Após liberação, um novo worker deve conseguir reivindicar.");
    }

    [Fact]
    public async Task StagingWatcher_DoesNotKeepDirectoriesThatCannotBeMonitored()
    {
        var invalidDirectory = Path.Combine(Path.GetTempPath(), $"mulletaflix-staging-file-{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(invalidDirectory, "not a directory");

        try
        {
            using var engine = new NebulaUploadEngine(
                null!,
                null!,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: false,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NebulaUploadEngine>.Instance);
            await using var watcher = new NebulaStagingWatcher(
                engine,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NebulaStagingWatcher>.Instance);

            watcher.Start([invalidDirectory], workerCount: 1);

            var stagingDirsField = typeof(NebulaStagingWatcher).GetField(
                "_stagingDirs",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

            Assert.NotNull(stagingDirsField);
            var stagingDirs = Assert.IsType<List<string>>(stagingDirsField.GetValue(watcher));
            Assert.Empty(stagingDirs);
        }
        finally
        {
            File.Delete(invalidDirectory);
        }
    }

    /// <summary>
    /// Regression: o script SQL do Supabase deve incluir CREATE POLICY para service_role
    /// em todas as tabelas gerenciadas, tornando o modelo de acesso explícito.
    /// </summary>
    [Fact]
    public void SupabaseSqlScript_ContainsRlsPolicies_ForAllNebulaTablesWithServiceRole()
    {
        var manager = new NebulaFtpManager(
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NebulaFtpManager>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var sql = manager.GetSupabaseSqlScript();

        Assert.NotNull(sql);

        // Deve habilitar RLS em todas as tabelas
        Assert.Contains("ALTER TABLE nebula_files ENABLE ROW LEVEL SECURITY", sql, System.StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE nebula_users ENABLE ROW LEVEL SECURITY", sql, System.StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE nebula_backups ENABLE ROW LEVEL SECURITY", sql, System.StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE nebula_bot_tokens ENABLE ROW LEVEL SECURITY", sql, System.StringComparison.Ordinal);

        // Deve criar políticas para service_role (evita acesso anônimo implícito)
        Assert.Contains("CREATE POLICY nebula_files_service_role_all", sql, System.StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY nebula_users_service_role_all", sql, System.StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY nebula_backups_service_role_all", sql, System.StringComparison.Ordinal);
        Assert.Contains("CREATE POLICY nebula_bot_tokens_service_role_all", sql, System.StringComparison.Ordinal);

        // Deve incluir DROP POLICY IF EXISTS para idempotência
        Assert.Contains("DROP POLICY IF EXISTS nebula_files_service_role_all", sql, System.StringComparison.Ordinal);

        // Não deve mais conter o comentário enganoso que dizia "Desabilita RLS"
        Assert.DoesNotContain("Desabilita RLS", sql, System.StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression: Dispose em NebulaFtpManager não deve lançar exceções mesmo se serviços não foram iniciados.
    /// </summary>
    [Fact]
    public void NebulaFtpManager_Dispose_CleansUpWithoutThrowing()
    {
        var manager = new NebulaFtpManager(
            null!,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<NebulaFtpManager>.Instance,
            Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        var exception = Record.Exception(() => manager.Dispose());
        Assert.Null(exception);
    }

    [Theory]
    [InlineData("12345678901234567890", "••••7890")]
    [InlineData("short", "••••hort")]
    [InlineData("1234", "••••")]
    [InlineData("", "••••")]
    public void NebulaFtpManager_MasksBotTokensWithoutExposingSecrets(string token, string expected)
    {
        Assert.Equal(expected, NebulaFtpManager.MaskBotToken(token));
    }

    [Theory]
    [InlineData("https://example.supabase.co", true, "https://example.supabase.co")]
    [InlineData("http://example.supabase.co", false, "")]
    [InlineData("https://user:password@example.supabase.co", false, "")]
    [InlineData("not-a-url", false, "")]
    public void SupabaseUrl_RequiresHttpsAbsoluteUrlWithoutEmbeddedCredentials(
        string url,
        bool expected,
        string expectedNormalized)
    {
        var result = NebulaFtpManager.IsSafeSupabaseUrl(url, out var normalizedUrl);

        Assert.Equal(expected, result);
        Assert.Equal(expectedNormalized, normalizedUrl);
    }

    [Theory]
    [InlineData(@"N:\Nebula\Filmes\Avatar.strm", @"N:\Nebula", true)]
    [InlineData(@"N:\Nebula", @"N:\Nebula", true)]
    [InlineData(@"N:\NebulaBackup\Avatar.strm", @"N:\Nebula", false)]
    [InlineData(@"N:\Outro\Avatar.strm", @"N:\Nebula", false)]
    public void MetadataExport_PathContainment_DoesNotAcceptPrefixCollisions(string path, string root, bool expected)
    {
        Assert.Equal(expected, NebulaMetadataExportService.IsPathWithinRoot(path, root));
    }

    [Theory]
    [InlineData(ImageType.Primary, 0, "poster.jpg")]
    [InlineData(ImageType.Primary, 1, "poster1.jpg")]
    [InlineData(ImageType.Logo, 0, "logo.png")]
    [InlineData(ImageType.Logo, 1, "logo1.png")]
    [InlineData(ImageType.Backdrop, 0, "fanart.jpg")]
    [InlineData(ImageType.Backdrop, 1, "fanart1.jpg")]
    public void MetadataExport_DoesNotOverwriteAdditionalImages(ImageType type, int index, string expected)
    {
        var item = new MediaBrowser.Controller.Entities.Movies.Movie
        {
            Path = @"N:\Nebula\Filmes\Avatar.strm"
        };

        Assert.Equal(expected, NebulaMetadataExportService.GetImageFileName(item, type, index, expected));
    }

    [Theory]
    [InlineData("poster.nfo", true)]
    [InlineData("poster.jpg", true)]
    [InlineData("poster.avif", true)]
    [InlineData("poster.gif", true)]
    [InlineData("poster.tiff", true)]
    [InlineData("movie.mkv", false)]
    [InlineData("movie.strm", false)]
    public void MetadataExport_RecognizesEveryExportedSidecarExtension(string fileName, bool expected)
    {
        Assert.Equal(expected, NebulaMetadataExportService.IsMetadataSidecarPath(fileName));
    }

    [Theory]
    [InlineData("movie.mkv", true)]
    [InlineData("movie.mp4", true)]
    [InlineData("movie.wmv", true)]
    [InlineData("poster.nfo", false)]
    [InlineData("poster.jpg", false)]
    [InlineData("movie.strm", false)]
    public void MetadataExport_OnlyMediaPayloadsCanReleasePendingMarker(string fileName, bool expected)
    {
        Assert.Equal(expected, NebulaMetadataExportService.IsMediaPayloadPath(fileName));
    }

    [Theory]
    [InlineData("movie.wmv", true)]
    [InlineData("movie.mkv", true)]
    [InlineData("movie.mp4", true)]
    [InlineData("movie.avi", true)]
    [InlineData("movie.ts", true)]
    [InlineData("subtitle.srt", true)]
    [InlineData("poster.nfo", true)]
    [InlineData("poster.jpg", true)]
    [InlineData("movie.strm", false)]
    [InlineData("movie.txt", false)]
    public void MetadataExport_OnlyMediaAndSidecarsAreUploadable(string fileName, bool expected)
    {
        Assert.Equal(expected, NebulaMetadataExportService.IsUploadablePath(fileName));
    }

    [Fact]
    public void NebulaDownloaderEngine_SupportsAllCompatibleMediaFormats()
    {
        Assert.Contains(".strm", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".mkv", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".mp4", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".avi", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".mov", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".wmv", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".m4v", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".ts", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".webm", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".flv", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".iso", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".mp3", NebulaDownloaderEngine.SupportedMediaExtensions);
        Assert.Contains(".flac", NebulaDownloaderEngine.SupportedMediaExtensions);

        Assert.Contains(".srt", NebulaDownloaderEngine.SupportedSidecarExtensions);
        Assert.Contains(".nfo", NebulaDownloaderEngine.SupportedSidecarExtensions);
        Assert.Contains(".jpg", NebulaDownloaderEngine.SupportedSidecarExtensions);
    }

    [Theory]
    [InlineData(@"strm\Filmes\Matrix\Matrix.strm", "Filmes/Matrix")]
    [InlineData(@"strm\Series\Dark\Season 1\Dark.S01E01.strm", "Series/Dark/Season 1")]
    [InlineData(@"strm\Porno\Studio\Cena.strm", "Porno")]
    public void MetadataExport_AutomaticRouteMatchesDownloader(string relativePath, string expectedDirectory)
    {
        var actual = NebulaMetadataExportService.GetAutomaticStageRelativeDirectory(
            relativePath,
            Path.GetFileName(relativePath));

        Assert.Equal(expectedDirectory, actual);
    }

    [Fact]
    public void MetadataExport_AutomaticRouteRejectsPathOutsideNebulaRoot()
    {
        Assert.Null(NebulaMetadataExportService.GetAutomaticStageRelativeDirectory(
            @"..\outside\movie.strm",
            "movie.strm"));
    }

    [Fact]
    public void MetadataExport_AutomaticRouteAllowsFileNamesStartingWithTwoDots()
    {
        Assert.Equal("Filmes", NebulaMetadataExportService.GetAutomaticStageRelativeDirectory(
            "..movie.strm",
            "..movie.strm"));
    }

    [Fact]
    public void MetadataExport_EmptyDirectoryIsAnOrphan()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nebula-orphan-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Assert.True(NebulaMetadataExportService.IsOrphanPendingMarkerDirectory(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Fact]
    public void MetadataExport_DirectoryWithMediaIsNotAnOrphan()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nebula-active-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            File.WriteAllText(Path.Combine(directory, "movie.mkv"), "payload");
            Assert.False(NebulaMetadataExportService.IsOrphanPendingMarkerDirectory(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Theory]
    [InlineData(@"C:\staging\strm\Filmes\Avatar\Avatar.mkv", @"C:\staging", "Filmes/Avatar")]
    [InlineData(@"C:\staging\Filmes\Avatar\Avatar.mkv", @"C:\staging", "Filmes/Avatar")]
    [InlineData(@"C:\staging\strm\Series\Breaking Bad\S01E01.mp4", @"C:\staging", "Series/Breaking Bad")]
    public void NebulaStagingWatcher_GetRelativeDirectory_StripsLeadingStrm(string filePath, string stagingRoot, string expectedRelDir)
    {
        var getRelDirMethod = typeof(NebulaStagingWatcher).GetMethod(
            "GetRelativeDirectory",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(getRelDirMethod);
        var result = (string?)getRelDirMethod.Invoke(null, [filePath, new[] { stagingRoot }]);

        Assert.Equal(expectedRelDir, result);
    }

    [Theory]
    [InlineData(@"C:\staging_backup\Filmes\Avatar", @"C:\staging", false)]
    [InlineData(@"C:\staging\Filmes\Avatar", @"C:\staging", true)]
    public void NebulaStagingWatcher_PathContainment_DoesNotAcceptPrefixCollisions(string filePath, string stagingRoot, bool expected)
    {
        var getRelDirMethod = typeof(NebulaStagingWatcher).GetMethod(
            "GetRelativeDirectory",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(getRelDirMethod);
        var result = (string?)getRelDirMethod.Invoke(null, [Path.Combine(filePath, "video.mkv"), new[] { stagingRoot }]);

        Assert.Equal(expected, !string.IsNullOrEmpty(result));
    }

    [Fact]
    public void DownloaderCleanup_PreservesDirectoriesContainingSidecars()
    {
        var root = Path.Combine(Path.GetTempPath(), "nebula-cleanup-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "movie.nfo"), "<movie />");

        try
        {
            using var engine = new NebulaDownloaderEngine(
                null!,
                null!,
                NullLogger<NebulaDownloaderEngine>.Instance);
            var cleanupMethod = typeof(NebulaDownloaderEngine).GetMethod(
                "CleanDirectoryRecursive",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(cleanupMethod);
            cleanupMethod.Invoke(engine, [root]);

            Assert.True(Directory.Exists(root));
            Assert.True(File.Exists(Path.Combine(root, "movie.nfo")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Downloader_DeleteAssociatedSidecars_PreservesNfoAndSidecarsOfCompletedMedia()
    {
        var root = Path.Combine(Path.GetTempPath(), "nebula-sidecar-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var mediaFile = Path.Combine(root, "Filme (2022).strm");
        var nfoFile = Path.Combine(root, "Filme (2022).nfo");
        var srtFile = Path.Combine(root, "Filme (2022).srt");
        var posterFile = Path.Combine(root, "poster.jpg");

        File.WriteAllText(mediaFile, "http://example.com");
        File.WriteAllText(nfoFile, "<movie />");
        File.WriteAllText(srtFile, "1\n00:00:01 --> 00:00:02\nTeste");
        File.WriteAllText(posterFile, "fake image");

        try
        {
            using var engine = new NebulaDownloaderEngine(
                null!,
                null!,
                NullLogger<NebulaDownloaderEngine>.Instance);
            var deleteSidecarsMethod = typeof(NebulaDownloaderEngine).GetMethod(
                "DeleteAssociatedSidecars",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(deleteSidecarsMethod);
            deleteSidecarsMethod.Invoke(engine, [mediaFile, "Filme (2022)"]);

            // Metadados e sidecars ficam no servidor como cache de exibição: o
            // carregamento do web e do aplicativo depende deles.
            Assert.True(File.Exists(nfoFile));
            Assert.True(File.Exists(srtFile));
            Assert.True(File.Exists(posterFile));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void Downloader_DeleteTargetStageDirectoryIfCompleted_RemovesStageSidecarsAndDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "nebula-stage-clean-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var nfoFile = Path.Combine(root, "Filme.nfo");
        var jpgFile = Path.Combine(root, "cover.jpg");
        var leftoverFile = Path.Combine(root, "trace.log");
        File.WriteAllText(nfoFile, "<movie />");
        File.WriteAllText(jpgFile, "image");
        File.WriteAllText(leftoverFile, "log");

        try
        {
            using var engine = new NebulaDownloaderEngine(
                null!,
                null!,
                NullLogger<NebulaDownloaderEngine>.Instance);
            var cleanStageMethod = typeof(NebulaDownloaderEngine).GetMethod(
                "DeleteTargetStageDirectoryIfCompleted",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(cleanStageMethod);
            cleanStageMethod.Invoke(engine, [root]);

            // O stage é fila transitória: o que já foi enviado ao Telegram sai dele,
            // inclusive capas/NFO, e a pasta vazia é removida.
            Assert.False(File.Exists(leftoverFile));
            Assert.False(File.Exists(nfoFile));
            Assert.False(File.Exists(jpgFile));
            Assert.False(Directory.Exists(root));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void MetadataExport_PendingMarkerIsRemovedOnlyByExplicitRelease()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nebula-metadata-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var marker = Path.Combine(directory, NebulaMetadataExportService.PendingMarkerFileName);
        File.WriteAllText(marker, "pending");

        try
        {
            Assert.True(File.Exists(marker));

            NebulaMetadataExportService.RemovePendingMarker(directory);

            Assert.False(File.Exists(marker));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void MetadataExport_CompletedMediaRetryReleasesPendingMarker()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nebula-completed-media-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var mediaPath = Path.Combine(directory, "movie.mkv");
        var marker = Path.Combine(directory, NebulaMetadataExportService.PendingMarkerFileName);
        File.WriteAllText(mediaPath, "payload");
        File.WriteAllText(marker, "pending");

        try
        {
            NebulaMetadataExportService.ReleasePendingMarkerForMedia(mediaPath);

            Assert.False(File.Exists(marker));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void StagingWatcher_DetectsPendingMetadataMarkerInMediaDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "nebula-staging-marker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var mediaPath = Path.Combine(directory, "movie.mkv");
        var marker = Path.Combine(directory, NebulaMetadataExportService.PendingMarkerFileName);

        try
        {
            var method = typeof(NebulaStagingWatcher).GetMethod(
                "HasPendingMetadataMarker",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);
            Assert.False((bool)method.Invoke(null, [mediaPath])!);

            File.WriteAllText(marker, "pending");

            Assert.True((bool)method.Invoke(null, [mediaPath])!);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Theory]
    [InlineData("/raphael", true)]
    [InlineData("/raphael/Filmes", true)]
    [InlineData("/raphaelBackup", false)]
    public void NebulaMongoContext_RaphaelPath_DoesNotAcceptPrefixCollisions(string path, bool expected)
    {
        Assert.Equal(expected, NebulaMongoContext.IsRaphaelPath(path));
    }

    [Theory]
    [InlineData(@"D:\midias\strm\Filmes\Avatar\Avatar.strm", @"D:\midias", "Filmes" + @"\Avatar")]
    [InlineData(@"D:\midias\Filmes\Avatar\Avatar.strm", @"D:\midias", "Filmes" + @"\Avatar")]
    public void NebulaDownloaderEngine_GetRelativePathFromSource_StripsLeadingStrm(string filePath, string monitorRoot, string expectedRelPath)
    {
        var getRelPathMethod = typeof(NebulaDownloaderEngine).GetMethod(
            "GetRelativePathFromSource",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(getRelPathMethod);
        var result = (string?)getRelPathMethod.Invoke(null, [filePath, new System.Collections.Generic.List<string> { monitorRoot }]);

        Assert.Equal(expectedRelPath, result);
    }

    [Fact]
    public void NebulaDownloader_PathContainment_DoesNotAcceptPrefixCollisions()
    {
        Assert.False(NebulaDownloaderEngine.IsPathWithinRoot(@"D:\midias_backup\Filmes", @"D:\midias"));
        Assert.True(NebulaDownloaderEngine.IsPathWithinRoot(@"D:\midias\Filmes", @"D:\midias"));
    }

    [Theory]
    // Filmes vão para a pasta Filmes
    [InlineData("Avatar (2009)", "Avatar.mkv", "Filmes/Avatar (2009)")]
    [InlineData("Filmes/Avatar (2009)", "Avatar.mkv", "Filmes/Avatar (2009)")]
    [InlineData("strm/Filmes/Matrix (1999)", "Matrix.mp4", "Filmes/Matrix (1999)")]
    [InlineData(null, "Matrix.mp4", "Filmes")]
    [InlineData("", "Gladiator.mkv", "Filmes")]
    // Series vão para Series preservando todas as subpastas
    [InlineData("Breaking Bad/Season 01", "S01E01.mkv", "Series/Breaking Bad/Season 01")]
    [InlineData("Series/Game of Thrones/Temporada 2", "GOT 2x01.mp4", "Series/Game of Thrones/Temporada 2")]
    [InlineData("strm/Series/Dark/Season 1", "Dark.S01E01.mkv", "Series/Dark/Season 1")]
    [InlineData(null, "Lost.S01E01.mkv", "Series")]
    // Porno vai SEMPRE diretamente para Porno sem nenhuma subpasta
    [InlineData("Atriz XYZ/Subpasta1/Subpasta2", "video_xxx.mp4", "Porno")]
    [InlineData("Porno/Studio/Cena", "cena.mp4", "Porno")]
    [InlineData("Adulto", "video.mp4", "Porno")]
    [InlineData("strm/Porno/Hentai Studio", "ep1.mkv", "Porno")]
    [InlineData(null, "Hentai Episode 1.mkv", "Porno")]
    // Filmes guardados dentro de 'Series\Filmes' continuam indo para a raiz Filmes
    [InlineData(@"Series\Filmes\O Show dos Muppets (2026)", "O Show dos Muppets (2026).mkv", "Filmes/O Show dos Muppets (2026)")]
    [InlineData(@"Series\Filmes\10x10 - O Cativeiro (2018)", "10x10 - O Cativeiro (2018).mkv", "Filmes/10x10 - O Cativeiro (2018)")]
    [InlineData(@"Nebula\Filmes\Matrix (1999)", "Matrix.mp4", "Filmes/Matrix (1999)")]
    // Raiz de categoria duplicada nunca é preservada
    [InlineData(@"Series\Series\BoJack Horseman\Season 03", "BoJack Horseman - S03E11.mkv", "Series/BoJack Horseman/Season 03")]
    [InlineData(@"strm\Series\Dark\Season 1", "Dark.S01E01.mkv", "Series/Dark/Season 1")]
    public void NebulaUploadEngine_RouteMediaRelativeDirectory_FollowsCategoryRules(string? relDir, string filename, string expected)
    {
        var result = NebulaUploadEngine.RouteMediaRelativeDirectory(relDir, filename);
        Assert.Equal(expected, result);
    }

    [Theory]
    // Filmes sob Nebula/Filmes/NomeDoFilme
    [InlineData("Avatar (2009)", "Avatar.mkv", "Nebula", "Filmes", "Avatar (2009)")]
    [InlineData("Filmes/Matrix (1999)", "Matrix.mp4", "Nebula", "Filmes", "Matrix (1999)")]
    [InlineData(null, "Gladiator (2000).mkv", "Nebula", "Filmes", "Gladiator (2000)")]
    // Series sob Nebula/Series/Show/Season ##
    [InlineData("Breaking Bad/Season 01", "S01E01.mkv", "Nebula", "Series", "Breaking Bad", "Season 01")]
    [InlineData("Series/Game of Thrones/Temporada 2", "GOT 2x01.mp4", "Nebula", "Series", "Game of Thrones", "Season 02")]
    [InlineData("Series/Dark/Season 1", "Dark.S01E01.mkv", "Nebula", "Series", "Dark", "Season 01")]
    [InlineData("Dark", "Dark.S03E05.mkv", "Nebula", "Series", "Dark", "Season 03")]
    // Porno sob Nebula/Porno
    [InlineData("Porno/Cena", "video_xxx.mp4", "Nebula", "Porno", null, null)]
    [InlineData("Adulto", "video.mp4", "Nebula", "Porno", null, null)]
    // Mesma árvore de destino para a mesma mídia, qualquer que seja a origem
    [InlineData(@"Series\Filmes\O Show dos Muppets (2026)", "O Show dos Muppets (2026).mkv", "Nebula", "Filmes", "O Show dos Muppets (2026)")]
    [InlineData(@"Series\Series\BoJack Horseman\Season 03", "BoJack Horseman - S03E11.mkv", "Nebula", "Series", "BoJack Horseman", "Season 03")]
    [InlineData(@"strm\Series\Dark\Season 1", "Dark.S01E01.mkv", "Nebula", "Series", "Dark", "Season 01")]
    public void NebulaStrmGenerator_RouteStrmRelativeDirectory_FollowsStandardHierarchy(
        string? relDir,
        string filename,
        string p1,
        string p2,
        string? p3 = null,
        string? p4 = null)
    {
        var parts = new System.Collections.Generic.List<string> { p1, p2 };
        if (p3 != null)
        {
            parts.Add(p3);
        }

        if (p4 != null)
        {
            parts.Add(p4);
        }
        var expected = Path.Combine(parts.ToArray());

        var result = NebulaStrmGenerator.RouteStrmRelativeDirectory(relDir, filename);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NebulaStrmGenerator_BuildStrmTargetUrl_PointsToIpAndPath_NeverDriveN()
    {
        var config = new MediaBrowser.Model.Configuration.NebulaFtpConfiguration
        {
            ServerHost = "192.168.1.100",
            ServerPort = 2121,
            UseMappedDrive = true,
            DriveLetter = "N:",
            Username = "user",
            Password = "password",
            EmbedFtpCredentialsInStrmUrls = true
        };

        var url = NebulaStrmGenerator.BuildStrmTargetUrl(config, "Filmes/Matrix (1999)", "Matrix.mp4");

        Assert.DoesNotContain("N:", url, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("ftp://user:password@192.168.1.100:2121/", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Filmes/Matrix%20%281999%29/Matrix.mp4", url, StringComparison.Ordinal);
    }

    [Fact]
    public void NebulaStrmGenerator_BuildStrmTargetUrl_DoesNotEmbedFtpCredentialsByDefault()
    {
        var config = new MediaBrowser.Model.Configuration.NebulaFtpConfiguration
        {
            ServerHost = "192.168.1.100",
            ServerPort = 2121,
            Username = "user",
            Password = "password"
        };

        var url = NebulaStrmGenerator.BuildStrmTargetUrl(config, "Filmes", "Movie.mp4");

        Assert.Equal("ftp://192.168.1.100:2121/Filmes/Movie.mp4", url);
        Assert.DoesNotContain("password", url, StringComparison.Ordinal);
    }

    [Fact]
    public void NebulaStrmGenerator_BuildStrmTargetUrl_WithoutAuth_GeneratesCleanFtpUrl()
    {
        var config = new MediaBrowser.Model.Configuration.NebulaFtpConfiguration
        {
            ServerHost = "10.0.0.5",
            ServerPort = 2121,
            UseMappedDrive = false,
            Username = string.Empty,
            Password = string.Empty
        };

        var url = NebulaStrmGenerator.BuildStrmTargetUrl(config, "Series/Dark/Season 01", "Dark.S01E01.mkv");

        Assert.DoesNotContain("N:", url, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("ftp://10.0.0.5:2121/", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Series/Dark/Season%2001/Dark.S01E01.mkv", url, StringComparison.Ordinal);
    }

    [Fact]
    public void NebulaStrmGenerator_BuildStrmTargetUrl_HttpIncludesStreamToken()
    {
        var config = new MediaBrowser.Model.Configuration.NebulaFtpConfiguration
        {
            ServerHost = "192.168.1.100",
            HttpStreamPort = 2123,
            HttpStreamToken = "token with spaces"
        };

        var url = NebulaStrmGenerator.BuildStrmTargetUrl(config, "Filmes", "Movie.mp4", useHttp: true);

        Assert.Equal("http://192.168.1.100:2123/stream?id=Filmes/Movie.mp4&token=token%20with%20spaces", url);
    }

    [Fact]
    public void NebulaFtpManager_DoesNotMountMonitoredMediaPaths_AndRemovesExistingMonitoredPaths()
    {
        var libraryManagerMock = new Mock<ILibraryManager>(MockBehavior.Strict);

        var moviesFolder = new VirtualFolderInfo
        {
            Name = "Filmes",
            Locations = new[] { @"D:\midias\Filmes", @"D:\External\Movies" }
        };
        var seriesFolder = new VirtualFolderInfo
        {
            Name = "Series",
            Locations = new[] { @"D:\midias2\Series" }
        };

        libraryManagerMock.Setup(m => m.GetVirtualFolders())
            .Returns(new List<VirtualFolderInfo> { moviesFolder, seriesFolder });
        libraryManagerMock.Setup(m => m.RemoveMediaPath("Filmes", @"D:\midias\Filmes"));
        libraryManagerMock.Setup(m => m.RemoveMediaPath("Series", @"D:\midias2\Series"));

        var manager = new NebulaFtpManager(
            null!,
            NullLogger<NebulaFtpManager>.Instance,
            NullLoggerFactory.Instance,
            libraryManager: libraryManagerMock.Object);

        var config = new NebulaFtpConfiguration
        {
            MonitorPaths = new[] { @"D:\midias", @"D:\midias2" }
        };

        manager.RemoveMonitoredMediaLibraryPaths(config);

        libraryManagerMock.Verify(m => m.RemoveMediaPath("Filmes", @"D:\midias\Filmes"), Times.Once);
        libraryManagerMock.Verify(m => m.RemoveMediaPath("Series", @"D:\midias2\Series"), Times.Once);
        libraryManagerMock.Verify(m => m.RemoveMediaPath(It.IsAny<string>(), @"D:\External\Movies"), Times.Never);
        libraryManagerMock.Verify(m => m.AddMediaPath(It.IsAny<string>(), It.IsAny<MediaPathInfo>()), Times.Never);
    }

    [Fact]
    public void CleanEmptyParentDirectories_RemovesEmptyFolders_UpToStageRoot()
    {
        var stageRoot = Path.Combine(Path.GetTempPath(), "nebula_stage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stageRoot);
        try
        {
            var nestedDir = Path.Combine(stageRoot, "Series", "ShowName", "Season 01");
            Directory.CreateDirectory(nestedDir);
            var file = Path.Combine(nestedDir, "episode1.mkv");
            File.WriteAllText(file, "dummy content");

            using var engine = new NebulaUploadEngine(
                null!,
                null!,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: true,
                NullLogger<NebulaUploadEngine>.Instance,
                getStagingRoots: () => new[] { stageRoot });

            // Simula a remoção do arquivo de staging
            File.Delete(file);
            engine.CleanEmptyParentDirectories(file);

            // As subpastas vazias devem ter sido removidas
            Assert.False(Directory.Exists(nestedDir));
            Assert.False(Directory.Exists(Path.Combine(stageRoot, "Series", "ShowName")));
            Assert.False(Directory.Exists(Path.Combine(stageRoot, "Series")));

            // A raiz de staging NÃO deve ser removida
            Assert.True(Directory.Exists(stageRoot));
        }
        finally
        {
            if (Directory.Exists(stageRoot))
            {
                Directory.Delete(stageRoot, true);
            }
        }
    }

    [Fact]
    public void CleanEmptyParentDirectories_PreservesFolder_WhenOtherFilesRemain()
    {
        var stageRoot = Path.Combine(Path.GetTempPath(), "nebula_stage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stageRoot);
        try
        {
            var movieDir = Path.Combine(stageRoot, "Filmes", "Inception (2010)");
            Directory.CreateDirectory(movieDir);
            var mediaFile = Path.Combine(movieDir, "Inception.mkv");
            var sidecarFile = Path.Combine(movieDir, "Inception.nfo");
            File.WriteAllText(mediaFile, "media");
            File.WriteAllText(sidecarFile, "nfo");

            using var engine = new NebulaUploadEngine(
                null!,
                null!,
                uploadConcurrency: 1,
                chunkSizeMb: 16,
                deleteSourceAfterUpload: true,
                NullLogger<NebulaUploadEngine>.Instance,
                getStagingRoots: () => new[] { stageRoot });

            // Remove apenas o arquivo de mídia, mas o sidecar ainda permanece na pasta
            File.Delete(mediaFile);
            engine.CleanEmptyParentDirectories(mediaFile);

            // A pasta NÃO deve ser excluída pois o sidecar ainda está nela
            Assert.True(Directory.Exists(movieDir));
            Assert.True(File.Exists(sidecarFile));

            // Agora remove o sidecar também
            File.Delete(sidecarFile);
            engine.CleanEmptyParentDirectories(sidecarFile);

            // Agora a pasta do filme e a pasta Filmes devem ser removidas
            Assert.False(Directory.Exists(movieDir));
            Assert.False(Directory.Exists(Path.Combine(stageRoot, "Filmes")));

            // Raiz de staging mantida intacta
            Assert.True(Directory.Exists(stageRoot));
        }
        finally
        {
            if (Directory.Exists(stageRoot))
            {
                Directory.Delete(stageRoot, true);
            }
        }
    }
}
