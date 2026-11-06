import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const statusData = JSON.parse(readFileSync(new URL('../homologacao-status.json', import.meta.url), 'utf8'));
const dashboardSource = readFileSync(new URL('../homologacao.js', import.meta.url), 'utf8');
const dashboardMarkup = readFileSync(new URL('../homologacao.html', import.meta.url), 'utf8');

test('portal registra retry síncrono de faixas de mídia sem promover homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-media-stream-mariadb-sync-retry-2026-11-03-0037');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /3\/3 em SQLite.*build IntroSkipper\.Integration\.Tests passou.*0 erros/i);
    assert.match(event.evidence, /1205\/1213.*três casos simulados.*MariaDB real opt-in.*não foram validados/i);
    assert.match(event.release, /413 eventos.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra cobertura do startup Nebula sem promover homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-upload-enabled-startup-coverage-2026-11-03-0023');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /820 aprovados, 1 ignorado\/821.*82%/);
    assert.match(event.evidence, /lifecycle parametrizado.*82% combinado.*Sem validação Linux, CI hospedada ou produção/i);
    assert.match(event.release, /412 eventos.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra retry integral de UpdatePeople e mantém homologação formal pendente', () => {
    const event = statusData.events.find(item => item.id === 'server-people-mariadb-whole-transaction-retry-2026-11-02-1603');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /1205 e 1213 simulados 2\/2.*seis testes opt-in MariaDB ignorados.*0 avisos\/erros/i);
    assert.match(event.evidence, /DbContext e transação novos.*no máximo três vezes.*1205\/1213/i);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata, não publicada.*Linux.*produção/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra retry síncrono de capítulos sem avançar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-chapters-mariadb-sync-retry-2026-11-02-1615');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /1205 e 1213 simulados 2\/2.*build focal.*0 avisos\/erros.*MariaDB opt-in ignorado/i);
    assert.match(event.evidence, /no máximo três vezes.*1205\/1213.*SQLite.*2\/2/i);
    assert.match(event.release, /2\/10.*2\/7.*12\.1\.46 candidata, não publicada.*produção/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra TRX da CI .NET sem promover homologação hospedada', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-dotnet-trx-artifacts-2026-11-02-1621');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /2\/2.*17 TRX distintos.*contrato CI 16\/16, portal 238\/238.*parse YAML passou/i);
    assert.match(event.evidence, /sete jobs.*if: always\(\).*17 arquivos TRX sem colisão.*não workflow hospedado/is);
    assert.match(event.release, /3\/10.*2\/7.*12\.1\.46 candidata, não publicada.*billing/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra regressão ampla .NET e Nebula sem conceder homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-2026-11-02-1629');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /17 TRX.*5\.671 aprovados.*290 ignorados.*0 falhas\/5\.961.*Nebula Python 3\.10\.11: 806\/806.*93%/i);
    assert.match(event.evidence, /excluiu LiveIntegrationTests.*cenários opt-in.*Windows.*produção não validados/i);
    assert.match(event.release, /4\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.deepEqual(server.testAudit, {
        passed: 5671,
        total: 5961,
        skipped: 290,
        failed: 0,
        percent: 95.1,
        label: 'Regressão .NET Release em Windows/.NET 10: 17 assemblies, 5.671 aprovados, 290 ignorados, 0 falhas/5.961; filtro FullyQualifiedName!~LiveIntegrationTests. Percentual é taxa de aprovação dos casos incluídos, não completude funcional nem homologação formal (2/7, 28,6%). Nebula Python: 820 aprovados, 1 ignorado/821 em Windows/Python 3.14.7; coverage.py combinado linha/branch: 82% em main, FTP e tools. CI hospedada, Linux/macOS, bancos opt-in, runtime instalado e produção permanecem pendentes.'
    });
    assert.match(statusData.latestAudit, /06\/11\/2026.*CI #526.*17 checks falhos antes de iniciar.*bloqueio de cobrança.*252\/252.*416 eventos, lote 6\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra propagação de cancelamento em testes novos do servidor sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-test-cancellation-platform-warnings-2026-11-02-1658');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /build focal.*0 erros.*15 aprovados, 1 ignorado.*11 avisos novos resolvidos/i);
    assert.match(event.evidence, /TestContext\.Current\.CancellationToken.*xUnit1030.*UnsupportedOSPlatform.*nenhuma fonte de produção/i);
    assert.match(event.release, /5\/10.*homologação formal permanece 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra atualização candidata do runtime Python Nebula e deixa produção pendente', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-runtime-314-installer-2026-11-02-1108');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Python 3\.14\.7.*806\/806.*3\.14\.10.*SHA-256/is);
    assert.match(event.evidence, /3\.14\.10.*Python Software Foundation.*3\.14\.7.*Linux.*produção/is);
    assert.match(event.release, /3\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('portal registra auditoria de ciclo de vida do runtime Nebula sem presumir homologação de produção', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-runtime-compat-audit-2026-11-02-1101');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Python 3\.14\.7.*lock.*806\/806/is);
    assert.match(event.evidence, /Python 3\.10.*fora de suporte.*3\.13\.7.*3\.14\.10.*bootstrap C\#/is);
    assert.match(event.release, /2\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('portal registra proteção dos logs Supabase e mantém homologação formal separada', () => {
    const event = statusData.events.find(item => item.id === 'server-supabase-credential-safe-logs-2026-11-02-1038');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /805\/805.*77\/77.*compileall/is);
    assert.match(event.evidence, /host e porta.*sem userinfo.*corpo.*feed_ftp\.py/i);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra proteção contra injeção CR/LF nos logs Nebula sem homologar produção', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-single-line-logs-2026-11-02-1028');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /803\/803.*test_main_logging 7\/7.*compileall.*--help/is);
    assert.match(event.evidence, /CR.*LF.*linha física.*traceback.*primeira suíte completa/is);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra auditoria de persistência das versões alternativas sem sugerir implementação ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-alternate-media-persistence-batch-audit-2026-11-02-0907');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /UpdateItemsAsync.*um único parent/is);
    assert.match(event.evidence, /código não foi alterado nem o custo medido/i);
    assert.match(event.release, /10\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra DTOs assíncronos de playback sem promover homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-playback-async-dto-2026-11-02-0928');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /SessionManagerTests 12\/12.*DtoService focal 11\/11/is);
    assert.match(event.evidence, /GetBaseItemDtoAsync.*ReportNowViewingItem.*não foram verificados/is);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra fallback Happy Eyeballs assíncrono sem promover homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-happy-eyeballs-async-fallback-2026-11-02-0934');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /HappyEyeballsConnectCallback 3\/3.*HTTP 200/is);
    assert.match(event.evidence, /GetAwaiter\(\)\.GetResult\(\).*await ConfigureAwait\(false\).*não foram verificados/is);
    assert.match(event.release, /2\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a suíte local e oportunidades técnicas sem homologar o servidor', () => {
    const event = statusData.events.find(item => item.id === 'server-full-suite-and-engineering-techniques-2026-11-02-0843');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /exit 0.*sem falhas.*filtro excluiu integração/is);
    assert.match(event.evidence, /Startup\.GetRetryPolicy.*OpenTelemetry OTLP.*BlobNotFound/is);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra auditoria de persistência de playlists externas sem promover homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-playlist-file-persistence-audit-2026-11-02-0833');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /cinco formatos.*dois callers/is);
    assert.match(event.evidence, /ACL.*SMB/is);
    assert.match(event.release, /7\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra refresh de backdrops localizados e persistência concorrente do ID do servidor', () => {
    const event = statusData.events.find(item => item.id === 'server-image-language-backdrop-device-id-2026-11-02-0826');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /backdrop.*2\/2.*84\/84.*DeviceIdTests.*3\/3/is);
    assert.match(event.evidence, /se não há candidato.*preserva a imagem.*lock da instância.*temporário irmão/is);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra a serialização local da alocação de InternalId e suas limitações', () => {
    const event = statusData.events.find(item => item.id === 'server-user-internal-id-concurrent-create-2026-11-02-0638');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /14\/14.*37 aprovados, 27 ignorados, 0 falhas\/64/);
    assert.match(event.evidence, /antes da correção.*depois.*EF InMemory.*somente uma instância\/processo/is);
    assert.match(event.release, /6\/10.*2\/7.*proteção entre processos permanece pendente/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('portal registra o fast path de read-ahead do Nebula e limita o alcance do benchmark', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-readahead-ready-queue-fast-path-2026-11-02-0624');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /3 esperas.*801\/801.*Microbenchmark sintético/);
    assert.match(event.evidence, /falhou antes.*passou após.*não mede Telegram/s);
    assert.match(event.release, /5\/10.*2\/7.*não validados/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra persistência protegida do estado do updater sem promover homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-update-state-protected-write-2026-11-02-0806');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /PersistText.*4\/4.*Jellyfin\.Common\.Tests: 40\/40/s);
    assert.match(event.evidence, /falha fechada.*cancelamento.*produção/s);
    assert.match(event.release, /5\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a regressão de idioma validada na suíte completa de providers', () => {
    const event = statusData.events.find(item => item.id === 'server-provider-suite-image-language-regression-2026-11-02-0620');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Jellyfin\.Providers\.Tests: 934 aprovados, 14 ignorados, 0 falhas\/948/);
    assert.match(event.evidence, /--no-build --no-restore.*Linux foram ignorados/s);
    assert.match(event.release, /4\/10.*2\/7.*não validados/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a persistência do idioma da imagem por ItemImageProvider', () => {
    const event = statusData.events.find(item => item.id === 'server-image-language-metadata-regression-test-2026-11-02-0618');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /passou 2\/2.*não provava o metadado/i);
    assert.match(event.evidence, /sem Language.*atribui pt-br/s);
    assert.match(event.release, /3\/10.*2\/7.*não verificados/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra os requisitos pendentes para coordenação MariaDB entre processos', () => {
    const event = statusData.events.find(item => item.id === 'server-user-admin-lock-prerequisites-2026-11-02-0614');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /UpdateUserAsync troca de DbContext.*nenhum teste real/i);
    assert.match(event.evidence, /GET_LOCK.*RELEASE_LOCK.*todos os writers cooperarem/s);
    assert.match(event.release, /2\/10.*2\/7.*não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a publicação do cache após salvar options.xml sem declarar atomicidade completa', () => {
    const event = statusData.events.find(item => item.id === 'server-collection-folder-cache-after-persist-2026-11-02-0540');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /Jellyfin\.Controller\.Tests 80 aprovados, 1 ignorado, 0 falhas\/81/);
    assert.match(event.evidence, /ainda usa SerializeToFile direto.*corromper o arquivo/s);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, não publicada/s);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a proteção concorrente do último administrador sem avançar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-user-admin-delete-concurrency-2026-11-02-0529');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /namespace Users 17\/17.*UserManagerAuthenticationLockTests 12\/12/);
    assert.match(event.evidence, /SemaphoreSlim.*dentro do processo/i);
    assert.match(event.release, /8\/10.*2\/7.*não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a validação de tamanho no fallback sequencial do Nebula sem homologar formalmente', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-sequential-download-length-validation-2026-11-02-0111');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /downloader focal 122\/122.*Nebula completa 791\/791/);
    assert.match(event.evidence, /100 bytes.*7 bytes.*destino válido anterior permanece/s);
    assert.match(event.release, /3\/10.*2\/7.*não foram verificados/s);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra publicação atômica do cache de imagens sem homologar formalmente', () => {
    const event = statusData.events.find(item => item.id === 'server-image-resized-cache-atomic-publish-2026-11-02-0102');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ImageProcessorCacheTests 2\/2.*ImageProcessor 4\/4/);
    assert.match(event.evidence, /arquivo temporário.*File.Move/s);
    assert.match(event.evidence, /lock por chave/i);
    assert.match(event.release, /2\/10.*2\/7.*não foram verificados/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra retry MariaDB de MediaStream sem promover homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-media-stream-transaction-lockwait-retry-2026-11-01-2332');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /teste focal MariaDB 2\/2.*ServerPersistenceMariaDbTests 24\/24/);
    assert.match(event.evidence, /MariaDB 1205 provocou retry/i);
    assert.match(event.evidence, /1213 real.*não foram validados/i);
    assert.match(event.release, /5\/10.*2\/7.*não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra atomicidade dos quatro replaces MariaDB no décimo item do lote', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-media-replace-atomicity-2026-11-01-2219');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ServerPersistenceMariaDbTests 19\/19/);
    assert.match(event.evidence, /anexos, streams, capítulos e keyframes/i);
    assert.match(event.evidence, /rollback.*conjunto anterior/i);
    assert.match(event.release, /10\/10.*2\/7.*sem release/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a proteção do cleanup e URLs STRM sem promover homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-cleanup-url-scheme-hardening-2026-11-02-0025');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /Nebula 746\/746.*208\/208/);
    assert.match(event.evidence, /0o777.*file:\/\//s);
    assert.match(event.evidence, /endereços privados e redirects não são bloqueados/i);
    assert.match(event.release, /9\/10.*2\/7.*não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records Supabase and STRM redirect validation without promoting formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-supabase-http-redirect-boundaries-2026-11-02-0040');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /focais 334\/334.*789\/789.*147\/147/);
    assert.match(event.evidence, /headers unredirected.*recusa redirects/i);
    assert.match(event.evidence, /delimitadores vazios/i);
    assert.match(event.evidence, /hosts privados continuam permitidos/i);
    assert.match(event.release, /10\/10.*2\/7.*não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra propagação de falhas de persistência sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-polly-save-failure-propagation-2026-11-01-2011');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /JellyfinDbContextTransactionRetryTests.*10\/10/);
    assert.match(event.evidence, /ExecuteAndCapture descartava a falha final/i);
    assert.match(event.evidence, /retryAttempt 1-based.*zero-based/i);
    assert.match(event.evidence, /exceção original após o limite/i);
    assert.match(event.release, /1\/10.*sem release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a auditoria de resiliência HTTP sem promover migração ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-http-resilience-policy-audit-2026-11-01-2033');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /StartupHttpRetryPolicyTests 3\/3/);
    assert.match(event.evidence, /breaker contando tentativas HTTP individuais/i);
    assert.match(event.evidence, /Http\.Resilience\/Polly v8.*não é drop-in/i);
    assert.match(event.release, /2\/10.*não publicada/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra lacuna de 429 e Retry-After sem alteração de runtime', () => {
    const event = statusData.events.find(item => item.id === 'server-http-429-retry-after-gap-2026-11-01-2047');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /StartupHttpRetryPolicyTests 4\/4/);
    assert.match(event.evidence, /429.*Retry-After.*uma tentativa/i);
    assert.match(event.evidence, /sem alteração de runtime/i);
    assert.match(event.release, /3\/10.*sem release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra recuperação do breaker após 30 segundos sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-http-circuit-breaker-recovery-2026-11-01-2131');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /StartupHttpRetryPolicyTests 9\/9.*31 s.*HTTP 200/);
    assert.match(event.evidence, /30 segundos configurados/);
    assert.match(event.release, /5\/10.*2\/7.*sem release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra auditoria de transações explícitas sem promover release ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-explicit-transaction-retry-audit-2026-11-01-2135');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /auditoria estática.*nenhum teste de runtime/i);
    assert.match(event.evidence, /MariaDB.*commit pode ser ambíguo/i);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46.*sem release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra retry limitado de compartilhamento de imagem sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-blurhash-transient-sharing-retry-2026-11-01-1738');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /ERROR_SHARING_VIOLATION \(32\)/);
    assert.match(event.evidence, /não identifica processo nem HResult/i);
    assert.match(event.local, /2\/2/);
    assert.match(event.release, /2\/10/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.match(server.homologation.label, /aceite formal de produção permanece pendente/i);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra retry de lock direto na candidata sem consolidar homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-direct-mariadb-lock-retry-2026-11-01-1923');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /1213\/1205.*MySqlException direta/);
    assert.match(event.local, /9\/9.*38\/38/);
    assert.match(event.release, /8\/10/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

function createDashboard(data = statusData) {
    const elements = new Map();
    const createElement = () => ({
        textContent: '',
        innerHTML: '',
        value: '',
        className: '',
        disabled: false,
        style: {},
        listeners: {},
        addEventListener(name, callback) {
            this.listeners[name] = callback;
        }
    });
    const getElement = id => {
        if (!elements.has(id)) {
            elements.set(id, createElement());
        }

        if (id === 'feature-status' && elements.get(id).value === '') {
            elements.get(id).value = 'all';
        }

        return elements.get(id);
    };
    const percentElements = [createElement(), createElement()];
    const document = {
        getElementById: getElement,
        querySelectorAll: selector => selector === '[data-coverage-percent]' ? percentElements : []
    };
    const fetch = async url => ({
        ok: true,
        json: async () => url === 'homologacao-status.json'
            ? data
            : url.includes('/actions/runs')
                ? { workflow_runs: [] }
                : []
    });
    const context = vm.createContext({
        document,
        fetch,
        window: { setInterval() {} },
        Intl,
        Date,
        Math,
        Number,
        String,
        Promise,
        console
    });

    vm.runInContext(dashboardSource, context, { filename: 'homologacao.js' });
    return { elements, getElement, percentElements };
}

async function settleDashboard() {
    await new Promise(resolve => setImmediate(resolve));
    await new Promise(resolve => setImmediate(resolve));
}

test('não exibe 0% quando evento não informa progresso', async () => {
    const data = {
        ...statusData,
        events: [{ id: 'sem-progresso', title: 'Evento sem percentual', status: 'in_progress', progressPercent: null, updatedAt: '2099-01-01T00:00:00Z' }, ...statusData.events]
    };
    const dashboard = createDashboard(data);
    await settleDashboard();

    const html = dashboard.getElement('event-list').innerHTML;
    const eventHtml = html.match(/<strong>Evento sem percentual<\/strong><p>(.*?)<\/p>/)?.[1];
    assert.ok(eventHtml);
    assert.doesNotMatch(eventHtml, /0%/);
});

test('renders every roadmap item and matching coverage counters', async () => {
    const dashboard = createDashboard();
    await settleDashboard();

    assert.equal(dashboard.getElement('coverage-total').textContent, statusData.coverage.total);
    assert.equal(dashboard.getElement('coverage-completed').textContent, statusData.coverage.completed);
    assert.equal(dashboard.getElement('coverage-pending').textContent, statusData.coverage.pending);
    assert.match(dashboard.getElement('feature-count').textContent, new RegExp(`^${statusData.features.length} tarefas`));
    assert.equal((dashboard.getElement('feature-table').innerHTML.match(/<tr>/g) || []).length, 25);
    assert.equal(dashboard.getElement('feature-page-number').textContent, `Página 1 de ${Math.ceil(statusData.features.length / 25)}`);
});
test('portal exibe a auditoria atual sem conceder homologação formal', async () => {
    const dashboard = createDashboard();
    await settleDashboard();

    assert.match(dashboardMarkup, /id="latest-audit"/);
    const auditRegion = dashboardMarkup.match(/<section class="dashboard-section" aria-labelledby="latest-audit-title">([\s\S]*?)<\/section>/)?.[1];
    assert.ok(auditRegion);
    assert.doesNotMatch(auditRegion, /\b(?:role|aria-live)=/);
    assert.equal(dashboard.getElement('latest-audit').textContent, statusData.latestAudit);
    assert.match(dashboard.getElement('latest-audit').textContent, /CI #526.*17 checks falhos antes de iniciar.*bloqueio de cobrança/);
    assert.match(dashboard.getElement('latest-audit').textContent, /Providers 951 aprovados\/14 ignorados\/965.*TmdbImageProviderLanguageTests 4\/4/);
    assert.match(dashboard.getElement('latest-audit').textContent, /FullSystemBackup Windows 157 aprovados\/16 ignorados.*Linux nativo/);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.tracks.find(track => track.id === 'server').homologation.percent, 28.6);
});
test('publishes the latest Nebula startup finding without marking T2.1 complete', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-live-runtime-promotion-2026-10-18-0827');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /95\/95/);
    assert.match(event.evidence, /sem download real/i);
});
test('search, status filter and pagination expose the matching roadmap rows', async () => {
    const dashboard = createDashboard();
    await settleDashboard();

    dashboard.getElement('feature-search').value = 'T6.1';
    dashboard.getElement('feature-search').listeners.input();
    assert.match(dashboard.getElement('feature-table').innerHTML, /T6\.1/);
    const t61Count = statusData.features.filter(feature => `${feature.id} ${feature.name} ${feature.area}`.includes('T6.1')).length;
    assert.equal((dashboard.getElement('feature-table').innerHTML.match(/<tr>/g) || []).length, Math.min(t61Count, 25));
    assert.equal(dashboard.getElement('feature-page-status').textContent, `Itens 1–${Math.min(t61Count, 25)} de ${t61Count}`);

    dashboard.getElement('feature-search').value = '';
    dashboard.getElement('feature-search').listeners.input();
    dashboard.getElement('feature-next').listeners.click();
    assert.equal(dashboard.getElement('feature-page-number').textContent, `Página 2 de ${Math.ceil(statusData.features.length / 25)}`);

    dashboard.getElement('feature-status').value = 'completed';
    dashboard.getElement('feature-status').listeners.change();
    assert.match(dashboard.getElement('feature-table').innerHTML, /Concluído/);
    assert.doesNotMatch(dashboard.getElement('feature-table').innerHTML, /Pendente/);
    assert.equal(dashboard.getElement('feature-prev').disabled, true);
});

test('formal Android matrix separates local evidence from pending product acceptance', async () => {
    const dashboard = createDashboard();
    await settleDashboard();

    const areas = statusData.androidHomologation.areas;
    assert.equal(areas.length, 21);
    assert.equal(areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(areas.filter(area => area.status === 'pending').length, 9);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.match(dashboard.getElement('android-homologation-state').textContent, /10 validadas localmente · 2 em homologação · 9 pendentes · aceite geral pendente/);
    assert.match(dashboard.getElement('android-homologation-table').innerHTML, /Descoberta e troca automática entre LAN e internet/);
    assert.match(dashboard.getElement('android-homologation-table').innerHTML, /receiver Chromecast\/Web real/);
    assert.match(dashboard.getElement('android-homologation-table').innerHTML, /Leitura de EPUB\/CBZ servidos pela instância real/);
    assert.match(dashboard.getElement('android-task-checklist').textContent, /228\/275 marcados e 47 abertos/);
});

test('portal renders every formal Android area and preserves its status evidence', async () => {
    const areas = statusData.androidHomologation.areas;
    const dashboard = createDashboard();
    await settleDashboard();
    const rendered = dashboard.getElement('android-homologation-table').innerHTML;

    assert.equal(new Set(areas.map(area => area.id)).size, 21);
    for (const area of areas) {
        assert.match(rendered, new RegExp(area.id));
        assert.ok(rendered.includes(area.name), `portal must show ${area.id} name`);
        assert.ok(rendered.includes(area.evidence), `portal must show ${area.id} evidence`);
    }
    assert.equal((rendered.match(/<tr>/g) || []).length, 21);
});

test('portal publishes current per-download pause and resume evidence without closing formal gates', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const downloads = statusData.androidHomologation.areas.find(area => area.id === 'APK-H07');
    const event = statusData.events.find(item => item.id === 'android-per-download-pause-resume-2026-10-13');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.equal(android.instrumentationAudit.total, 45);
    assert.equal(android.instrumentationAudit.failed, 1);
    assert.match(downloads.evidence, /HTTP Range até COMPLETED/);
    assert.ok(event, 'portal should include the current Android development event');
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'pending').length, 9);
    assert.match(event.evidence, /APK-H17 continua pendente/);
    assert.match(event.evidence, /Nenhuma versão, APK de produção ou release criada/);
});

test('portal mirrors verified HTML/KF8 CSS progress without promoting formal acceptance', () => {
    const event = statusData.events.find(item => item.id === 'android-kf8-css-important-order-2026-10-15');
    const android = statusData.tracks.find(track => track.id === 'android');

    assert.ok(event, 'portal should include the latest verified Android event');
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /PHONE\/API 35, TABLET\/API 35 e Android TV\/API 34/);
    assert.match(event.evidence, /1\.784\/1\.784 em 256 XML/);
    assert.match(event.evidence, /PlainTextBookDocumentTest passou 45\/45/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.failed, 0);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.find(area => area.id === 'APK-H21').status, 'pending');
});

test('portal records update-effect test isolation without changing formal homologation', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const event = statusData.events.find(item => item.id === 'android-update-effect-test-isolation-2026-10-13');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.match(event.evidence, /Activity não intercepta sockets GitHub ponta a ponta/);
    assert.match(event.evidence, /AppUpdateCheckEffectTest-PHONE-API35\.xml/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.match(android.instrumentationAudit.label, /AppUpdateCheckEffectTest 2\/2/);
    assert.ok(event, 'portal should record the completed Android test isolation update');
    assert.match(event.evidence, /TDD RED/);
    assert.match(event.evidence, /AppUpdateCheckEffectTest` passou 2\/2/);
    assert.match(event.evidence, /Quality gate .* passou/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'pending').length, 9);
});

test('portal records SyncPlay event-flow integration without formal acceptance', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const event = statusData.events.find(item => item.id === 'android-syncplay-realtime-event-flow-2026-10-13');

    assert.ok(event, 'portal should reflect the newly verified SyncPlay integration test');
    assert.match(event.evidence, /MockWebServer/);
    assert.match(event.evidence, /Registro histórico de 13\/10\/2026/);
    assert.match(event.evidence, /cobertura subsequente.*registrada no evento/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'pending').length, 9);
});

test('portal mirrors SyncPlay remote-close progress without granting formal acceptance', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H10');
    const event = statusData.events.find(item => item.id === 'android-syncplay-remote-close-2026-10-13');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.match(android.testAudit.label, /256 XML/);
    assert.ok(event, 'portal must mirror the verified SyncPlay implementation and tests');
    assert.match(event.evidence, /TDD RED/);
    assert.match(event.evidence, /Connected → Disconnected → Connected/);
    assert.match(event.evidence, /1005 em status válido 1000/);
    assert.match(event.evidence, /TestCoroutineScheduler/);
    assert.match(event.evidence, /captura o job de retry durante o backoff suspenso/);
    assert.match(event.evidence, /quadro de resposta mascarado com 1000 usando um único fluxo de entrada/);
    assert.match(area.evidence, /fechamento iniciado pelo servidor/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'pending').length, 9);
});

test('portal records the complete session-navigation retest without changing formal homologation', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const event = statusData.events.find(item => item.id === 'android-session-navigation-stability-2026-10-13');

    assert.ok(event, 'portal should record the latest complete session-navigation run');
    assert.match(event.evidence, /passou 9\/9/);
    assert.match(event.evidence, /MainActivitySessionNavigationTest-PHONE-API35\.xml/);
    assert.match(event.evidence, /falha histórica 8\/9 permanece sem causa confirmada/);
    assert.match(android.instrumentationAudit.label, /MainActivitySessionNavigationTest 9\/9/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(area => area.status === 'pending').length, 9);
});

test('portal reports the latest server-switch login regression as in progress, not homologated', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H02');
    const event = statusData.events.find(item => item.id === 'android-profile-server-switch-login-investigation-2026-10-14');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(area.status, 'in_progress');
    assert.match(area.evidence, /MainActivitySessionNavigationTest passou 12\/12 em PHONE.*12\/12 em TABLET/);
    assert.match(area.evidence, /repeat 2 TABLET.*sem causa raiz determinada/i);
    assert.equal(android.homologation.completed, 10);
    assert.equal(android.homologation.total, 21);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.equal(android.instrumentationAudit.total, 45);
    assert.equal(android.instrumentationAudit.failed, 1);
    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /profile-server-switch-login-red\.xml/);
    assert.match(event.evidence, /Sem release, versão nova ou mudança no servidor/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
});

test('portal reflects reader cold-start evidence without closing APK-H21', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H21');
    const event = statusData.events.find(item => item.id === 'android-book-reader-cold-start-harness-2026-10-13');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.match(android.instrumentationAudit.label, /cold start da Activity principal 4\/4 fases/);
    assert.equal(area.status, 'pending');
    assert.match(area.evidence, /morte real do processo/);
    assert.match(area.evidence, /não depende do polling best-effort/);
    assert.ok(event, 'portal should include the verified host-harness correction');
    assert.match(event.evidence, /Pester falhou 1\/1/);
    assert.match(event.evidence, /PID 3555→3644/);
    assert.match(event.evidence, /Servidor e conteúdo sintéticos/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'pending').length, 9);
});

test('portal records API 37 emulator boot blocker without counting tests or closing APK-H14', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H14');
    const event = statusData.events.find(item => item.id === 'android-local-network-api37-emulator-boot-blocked-2026-10-13');

    assert.ok(event);
    assert.equal(event.status, 'blocked');
    assert.match(event.evidence, /surfaceflinger.*hasReadColorBufferDma/);
    assert.match(event.evidence, /nenhum caso de permissão foi contado/);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.equal(android.instrumentationAudit.failed, 1);
    assert.equal(area.status, 'pending');
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
});

test('portal records this round API 37 boot blocker without inflating tests or formal acceptance', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H14');
    const event = statusData.events.find(item => item.id === 'android-api37-avd-boot-inconclusive-2026-10-16');

    assert.ok(event);
    assert.equal(event.status, 'blocked');
    assert.match(event.evidence, /sys\.boot_completed permaneceu vazio/);
    assert.match(event.evidence, /zero casos executados/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.equal(area.status, 'pending');
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
});

test('portal mirrors persisted SurfaceFlinger boot failure without counting instrumentation', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H14');
    const event = statusData.events.find(item => item.id === 'android-api37-surfaceflinger-boot-diagnostics-2026-10-16');

    assert.ok(event);
    assert.equal(event.status, 'blocked');
    assert.match(event.evidence, /SIGABRT na thread RegionSampling/);
    assert.match(event.evidence, /zero testes, zero XML/);
    assert.match(event.evidence, /Pester 51\/51.*parser PowerShell 24\/24.*portal Node 27\/27/);
    assert.match(event.evidence, /1\.788 testes\/256 XML, 0 falhas\/erros\/ignorados/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.equal(area.status, 'pending');
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
});

test('portal records Android API 24 and 35 connectivity callback validation without closing APK-H14', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const checklist = statusData.androidHomologation.taskChecklist;
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H14');
    const event = statusData.events.find(item => item.id === 'android-connectivity-callback-api24-api35-2026-10-13');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /passou 3\/3 em PHONE\/API 35 e 3\/3 em PHONE\/API 24/);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.equal(android.instrumentationAudit.total, 45);
    assert.equal(android.instrumentationAudit.failed, 1);
    assert.equal(checklist.checked, 228);
    assert.equal(checklist.unchecked, 47);
    assert.equal(area.status, 'pending');
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
});

test('portal renders blocked validation events with a Portuguese warning state in its isolated event fixture', async () => {
    const blockedEvent = statusData.events.find(item => item.id === 'android-local-network-api37-emulator-boot-blocked-2026-10-13');
    assert.ok(blockedEvent);
    const dashboard = createDashboard({ ...statusData, events: [blockedEvent] });
    await settleDashboard();

    const events = dashboard.getElement('event-list').innerHTML;
    assert.match(events, /Tentativa de validar permissão de rede local na API 37/);
    assert.match(events, /Bloqueado/);
    assert.match(events, /event-dot is-warn/);
});

test('portal mirrors the Home cache URL-scope fix without closing formal homologation', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H04');
    const event = statusData.events.find(item => item.id === 'android-home-cache-default-port-normalization-2026-10-13');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.match(android.testAudit.label, /1\.788 testes.*256 XML/);
    assert.ok(event, 'portal must record the verified Android cache-scope fix');
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /RED.*1 falha esperada/);
    assert.match(event.evidence, /HTTPS :443 e HTTP :80/);
    assert.match(area.evidence, /escopo de cache offline da Home/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'pending').length, 9);
});

test('portal records Home cache DataStore persistence tests without closing formal homologation', () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const area = statusData.androidHomologation.areas.find(item => item.id === 'APK-H04');
    const event = statusData.events.find(item => item.id === 'android-home-cache-datastore-persistence-2026-10-13');

    assert.ok(event, 'portal should record the latest DataStore persistence validation');
    assert.equal(event.status, 'completed');
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.match(event.evidence, /HomeFeedCacheRepositoryPersistenceTest passou 4\/4/);
    assert.match(event.evidence, /não possuem RED\/GREEN comportamental/);
    assert.match(android.instrumentationAudit.label, /HomeFeedCacheRepositoryPersistenceTest 4\/4/);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.equal(android.instrumentationAudit.total, 45);
    assert.equal(android.instrumentationAudit.failed, 1);
    assert.match(area.evidence, /recriar DataStore\/repositório/);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(statusData.androidHomologation.areas.filter(item => item.status === 'pending').length, 9);
});

test('portal mirrors Android progress from 2026-10-15 without promoting formal acceptance', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const matrix = statusData.androidHomologation;
    const area = id => matrix.areas.find(item => item.id === id);
    const event = id => statusData.events.find(item => item.id === id);

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.match(android.testAudit.label, /1\.788 testes.*256 XML/);
    assert.equal(android.instrumentationAudit.passed, 44);
    assert.equal(android.instrumentationAudit.total, 45);
    assert.equal(android.instrumentationAudit.failed, 1);
    assert.match(area('APK-H02').evidence, /passou 12\/12 em PHONE.*12\/12 em TABLET/);
    assert.equal(area('APK-H02').status, 'in_progress');
    assert.match(area('APK-H09').evidence, /PlaybackInfo.*LiveStreams\/Open/);
    assert.match(area('APK-H10').evidence, /limpar sala.*trocar para outra conta ou servidor/);
    assert.match(area('APK-H21').evidence, /Busca OCR PDF.*PHONE.*TABLET/);
    assert.ok(event('android-syncplay-server-session-isolation-2026-10-15'));
    assert.ok(event('android-live-tv-player-journey-2026-10-15'));
    assert.ok(event('android-pdf-ocr-cancel-retry-2026-10-15'));
    assert.ok(event('android-full-jvm-gate-2026-10-15'));
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(item => item.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const visibleEvents = statusData.events.filter(item => [
        'android-syncplay-server-session-isolation-2026-10-15',
        'android-live-tv-player-journey-2026-10-15',
        'android-pdf-ocr-cancel-retry-2026-10-15',
        'android-full-jvm-gate-2026-10-15'
    ].includes(item.id));
    const dashboard = createDashboard({ ...statusData, events: visibleEvents });
    await settleDashboard();
    const activity = dashboard.getElement('event-list').innerHTML;
    assert.match(activity, /Isolar a sessão SyncPlay ao trocar conta ou servidor/);
    assert.match(activity, /Validar a navegação da TV ao vivo até PlaybackInfo/);
    assert.match(activity, /Validar cancelamento e nova tentativa de busca OCR de PDF/);
    assert.match(activity, /Revalidar o gate JVM e a compilação Android/);
});

test('portal mirrors the latest Android reader validation without promoting formal acceptance', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-kf8-embedded-css-emphasis-2026-10-15');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.match(android.testAudit.label, /1\.788 testes.*256 XML/);
    assert.ok(event, 'portal should expose the latest bounded embedded CSS reader evidence');
    assert.match(event.evidence, /39\/39.*256 Ki caracteres.*2\.048 regras/);
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(item => item.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Preservar ênfase CSS embutido no leitor HTML\/KF8/);
});

test('portal mirrors accessible paginated PDF OCR progress without promoting APK-H21', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-book-text-search-accessible-progress-2026-10-15');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.ok(event, 'portal should record the latest accessible paginated OCR progress validation');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, ['APK-H21']);
    assert.match(event.evidence, /paginatedSearchShowsAccessibleDeterminateProgress passou 1\/1 em PHONE\/API 35/);
    assert.match(event.evidence, /ProgressBarRangeInfo.*LiveRegionMode\.Polite/);
    assert.match(event.evidence, /não valida fala audível.*TalkBack/i);
    assert.equal(android.homologation.completed, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.find(item => item.id === 'APK-H21').status, 'pending');
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Progresso acessível da busca paginada/);
});

test('portal mirrors TV background pause and tablet PiP-close evidence without promoting formal acceptance', async () => {
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-player-background-pause-tv-pip-close-tablet-2026-10-15');

    assert.ok(event, 'portal should record the latest TV and tablet player lifecycle validation');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, ['APK-H06']);
    assert.match(event.evidence, /PlayerBackgroundPlaybackTest.*3\/3.*TV\/API 34/);
    assert.match(event.evidence, /PlayerPictureInPictureLifecycleTest.*1\/1.*TABLET\/API 35/);
    assert.match(event.evidence, /sintética.*não promove/i);
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Pausa em segundo plano na TV e fechamento de PiP no tablet/);
});

test('portal mirrors full-document KF8 image rendering on Android TV without promoting APK-H21', async () => {
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-kf8-full-document-image-tv-2026-10-15');

    assert.ok(event, 'portal should record the verified full-document KF8 image test on Android TV');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, ['APK-H21']);
    assert.match(event.evidence, /hybridMobiImageRendersAtOriginalPositionInFullDocumentAfterScroll passou 1\/1 em Android TV\/API 34/);
    assert.match(event.evidence, /pixel magenta.*descrição acessível/i);
    assert.match(event.evidence, /fixture sintética não valida D-pad/i);
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(item => item.status === 'pending').length, 9);
    assert.equal(matrix.areas.find(item => item.id === 'APK-H21').status, 'pending');
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Imagem inline KF8 em Android TV/);
});

test('portal mirrors nested KF8 style resets across device profiles without promoting APK-H21', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-kf8-nested-style-reset-decoration-2026-10-15');

    assert.ok(event, 'portal should record the nested style reset and decoration propagation tests');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, ['APK-H21']);
    assert.match(event.evidence, /PHONE\/API 35, TABLET\/API 35 e Android TV\/API 34/);
    assert.match(event.evidence, /PlainTextBookReaderIntegrationTest passou 15\/15/);
    assert.match(event.evidence, /1\.780\/1\.780 em 256 XML/);
    assert.match(event.evidence, /APK-H21 permanece pendente/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(item => item.status === 'pending').length, 9);
    assert.equal(matrix.areas.find(item => item.id === 'APK-H21').status, 'pending');
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, new RegExp(event.title));
});

test('portal mirrors SyncPlay Media3 command handling during media preparation without formal promotion', async () => {
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-syncplay-player-realtime-2026-10-16');

    assert.ok(event, 'portal should record the realtime SyncPlay command reaching Media3 while media loads');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, ['APK-H10']);
    assert.match(event.evidence, /Pause recebida enquanto a fila carregava/i);
    assert.match(event.evidence, /Media3.*READY pausado em 0 ms/i);
    assert.match(event.evidence, /PHONE\/API 35, TABLET\/API 35 e TV\/API 34/);
    assert.match(event.evidence, /MockWebServer.*sintétic/i);
    assert.equal(matrix.areas.find(area => area.id === 'APK-H10').status, 'completed');
    assert.equal(matrix.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(area => area.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Comandos SyncPlay aplicados ao Media3 durante preparação da mídia/);
});

test('portal mirrors offline download fixture race correction without formal promotion', async () => {
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-offline-download-completion-race-2026-10-16');

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.ok(event, 'portal should record why the offline download fixture was disabled early');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, ['APK-H07']);
    assert.match(event.evidence, /89,4%.*antes de STATE_COMPLETED/);
    assert.match(event.evidence, /Duas execuções integradas consecutivas passaram em TABLET\/API 35/);
    assert.match(event.evidence, /Sem alteração no player de produção/);
    assert.match(event.evidence, /formalSignoff=pending/);
    assert.equal(matrix.areas.find(area => area.id === 'APK-H07').status, 'completed');
    assert.equal(matrix.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(area => area.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const serverEvent = statusData.events.find(item => item.id === 'server-nebula-live-runtime-promotion-2026-10-18-0827');
    assert.ok(serverEvent, 'portal should retain the Nebula startup evidence');

    const dashboard = createDashboard({ ...statusData, events: [serverEvent] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, new RegExp(serverEvent.title));
});

test('portal records each Android work-round review without fabricating homologation progress', async () => {
    const event = statusData.events.find(item => item.id === 'android-homologation-round-sync-2026-10-16-1819');
    const matrix = statusData.androidHomologation;

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.ok(event, 'portal should include the current round review');
    assert.equal(event.status, 'completed');
    assert.deepEqual(event.featureIds, []);
    assert.match(event.evidence, /Nenhum teste funcional Android ou instrumentado foi executado nesta rodada/);
    assert.match(event.evidence, /suíte do portal passou 28\/28/);
    assert.match(event.evidence, /Portal atualizado apenas no checkout local/);
    assert.equal(matrix.formalSignoff, 'pending');
    assert.equal(matrix.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(area => area.status === 'pending').length, 9);

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Revisão da homologação Android nesta rodada/);
});

test('portal records the API 37.1 boot blocker without counting tests or closing APK-H14', async () => {
    const event = statusData.events.find(item => item.id === 'android-api37-1gapi-systemserver-restart-2026-10-16-1841');
    const matrix = statusData.androidHomologation;

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.ok(event, 'portal should record the latest API 37.1 attempt');
    assert.equal(event.status, 'blocked');
    assert.deepEqual(event.featureIds, ['APK-H14']);
    assert.match(event.evidence, /system_server\.start_count chegou a 41/);
    assert.match(event.evidence, /0 testes e nenhum XML/);
    assert.match(event.evidence, /não falha nem aprovação do APK/);
    assert.equal(matrix.areas.find(area => area.id === 'APK-H14').status, 'pending');
    assert.equal(matrix.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(area => area.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Nova imagem Android 17 sem boot completo/);
});

test('portal records API 37.0 system-server death with zero Android tests and no formal promotion', async () => {
    const event = statusData.events.find(item => item.id === 'android-api37-0gapi-systemserver-deadobject-2026-10-16-1906');
    const matrix = statusData.androidHomologation;

    assert.match(statusData.generatedAt, /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.ok(event, 'portal should record the API 37.0 instrumentation attempt');
    assert.equal(event.status, 'blocked');
    assert.deepEqual(event.featureIds, ['APK-H14']);
    assert.match(event.evidence, /DeadObjectException/);
    assert.match(event.evidence, /XML JUnit do AVD contém tests="0"/);
    assert.match(event.evidence, /não é falha funcional do APK/);
    assert.equal(matrix.areas.find(area => area.id === 'APK-H14').status, 'pending');
    assert.equal(matrix.areas.filter(area => area.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(area => area.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(area => area.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Android 17\/API 37\.0 derrubou serviços durante o AndroidTest/);
});

test('portal reports the server Nebula release gate without claiming production validation', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-configured-monitor-paths-2026-10-16');
    const audit = statusData.tracks.find(track => track.id === 'server').testAudit;

    assert.ok(event, 'portal should include the current server validation/release event');
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /1\.437 aprovados, 60 ignorados, 0 falhas/);
    assert.match(event.evidence, /repetição final inconclusiva/i);
    assert.match(event.evidence, /produção/i);
    assert.match(event.evidence, /dpl_22RZqxXtpqbAwL9EUW2Ys5sw6HQ1 READY/);
    assert.match(event.evidence, /HTTP 200/);
    assert.equal(audit.passed, 5671);
    assert.equal(audit.total, 5961);
    assert.equal(audit.skipped, 290);
    assert.equal(audit.failed, 0);
    assert.equal(audit.percent, 95.1);
    assert.match(audit.label, /bancos opt-in/);

    const releaseEvent = statusData.events.find(item => item.title === 'Servidor 12.1.36 publicada com artefatos Windows e Linux');
    assert.ok(releaseEvent, 'portal source should retain the server release event');
});

test('portal records the current deterministic server suite without claiming integration or production validation', async () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-server-suite-gate-2026-10-18-0247');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.equal(server.testAudit.skipped, 290);
    assert.equal(server.testAudit.failed, 0);
    assert.match(server.testAudit.label, /bancos opt-in/);
    assert.match(event.evidence, /NebulaPython.*592\/592/);
    assert.match(event.evidence, /não tiveram testes correspondentes ao filtro/);
    assert.match(event.evidence, /Nenhuma release de servidor criada/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /gate determinístico \.NET aprovado/);
});

test('portal records the orphaned Nebula worker and avoids claiming a production repair', async () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-nebula-orphaned-worker-2026-10-18-0254');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /12\.1\.21/);
    assert.match(event.evidence, /PID pai já não existe/);
    assert.match(event.evidence, /não expõe PID/);
    assert.match(event.evidence, /não foi encerrado/);
    assert.match(event.evidence, /passou 1\/1/);
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, new RegExp(event.title));
});

test('server Nebula STRM download scan records tests without claiming production homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-strm-scan-status-2026-10-16-2127');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /90\/90 passaram/);
    assert.match(event.evidence, /Smoke test de download real.*pendentes/);
});

test('server Nebula startup audit records the disabled global gate without claiming manual validation', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-startup-gate-2026-10-16-2136');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /Enabled=false/);
    assert.match(event.evidence, /Não foi observado se houve tentativa manual/);
    assert.match(event.evidence, /Smoke test manual.*pendentes/);
    assert.match(event.evidence, /não expôs suas linhas de comando/);

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Downloader Nebula sem início automático/);
});

test('server Nebula download scan progress records production reproduction and source validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-scan-progress-2026-10-16-2149');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /11m55 sem iniciar mídia/);
    assert.match(event.evidence, /571\/571 passou/);
    assert.match(event.evidence, /processa uma categoria por vez/);
    assert.match(event.evidence, /1\.077 avisos de analisadores/);
    assert.match(event.evidence, /tag v12\.1\.22.*bloqueio de cobrança na conta GitHub/);
    assert.match(event.evidence, /não existe GitHub Release baixável/);
    assert.match(event.evidence, /binário de produção continua 12\.1\.21/);
    assert.match(event.evidence, /Smoke test ponta a ponta.*pendentes/);
});

test('portal records title-level Nebula download startup without claiming production homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-first-title-download-2026-10-16-2214');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /subdivide categorias diretas em pastas de título ordenadas A–Z/);
    assert.match(event.evidence, /574\/574 aprovad[oa]/);
    assert.match(event.evidence, /Model\.Tests 684\/684/);
    assert.match(event.evidence, /Server\.Implementations\.Tests 1\.437 aprovados, 60 ignorados/);
    assert.match(event.evidence, /zero falhas nos projetos reportados/);
    assert.match(event.evidence, /Integrações dependentes de Mongo.*foram ignoradas/);
    assert.match(event.evidence, /testes do portal no espelho 35\/35/);
    assert.match(event.evidence, /GitHub CLI sem autenticação/);
    assert.match(event.evidence, /nenhum worker de produção foi iniciado\/parado/);
    assert.match(event.evidence, /instalação segue 12\.1\.21/);
    assert.match(event.evidence, /homologação real e release permanecem pendentes/);
    assert.equal(server.status, 'in_progress');
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Nebula inicia o download após indexar o primeiro título/);
});

test('portal records real-library Nebula scan revalidation as partial', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-first-title-revalidation-2026-10-16-2248');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.deepEqual(event.featureIds, ['T0.3', 'T2.1']);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /Produção segue 12\.1\.21/);
    assert.match(event.evidence, /duas varreduras sem contagem ou Iniciando download/);
    assert.match(event.evidence, /120 títulos em Animações e um item elegível no primeiro título A–Z/);
    assert.match(event.evidence, /suíte NebulaPython 574\/574/);
    assert.match(event.evidence, /Nenhum download, Mongo\/Telegram, worker ou configuração de produção foi alterado/);
    assert.match(event.evidence, /Correção local não instalada/);

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Revalidar o início do primeiro download Nebula/);
});

test('portal records library refresh admission and downloader false-response feedback as partial', async () => {
    const event = statusData.events.find(item => item.id === 'server-library-refresh-nebula-start-feedback-2026-10-16-2233');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1', 'T6.2']);
    assert.match(event.evidence, /POST \/Library\/Refresh.*single-flight/);
    assert.match(event.evidence, /429\/Retry-After: 1/);
    assert.match(event.evidence, /false gera aviso/);
    assert.match(event.evidence, /126\/126/);
    assert.match(event.evidence, /574\/574/);
    assert.match(event.evidence, /Sem navegador real, teste de carga, download\/worker de produção/);

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Admissão de refresh global e feedback do downloader Nebula/);
});

test('portal records Nebula worker scan readiness without claiming first download or production homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-worker-startup-readiness-2026-10-16-2308');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /primeiro item efetivamente enumerado pelo os\.walk/);
    assert.match(event.evidence, /647 aprovados, 21 ignorados, 0 falhas/);
    assert.match(event.evidence, /NebulaPython 576\/576/);
    assert.match(event.evidence, /não o primeiro download/);
    assert.match(event.evidence, /Produção e download Telegram reais não foram exercitados/);

});

test('portal records a rebuilt Nebula stage without claiming an installer or production fix', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-stage-rebuilt-2026-10-17-0042');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /validate-stage\.ps1` passou/);
    assert.match(event.evidence, /C588DD2006A182440A57AC02D33730531E1FD0575FB3B971B6441D459FDAA88F/);
    assert.match(event.evidence, /581\/581/);
    assert.match(event.evidence, /10\/10/);
    assert.match(event.evidence, /Web, tray e installer foram pulados/);
    assert.match(event.evidence, /Produção segue 12\.1\.21/);
    assert.match(event.evidence, /Primeiro download real continua pendente/);
    assert.match(event.evidence, /rclone\.exe não estava presente/);
    assert.match(event.evidence, /Revalidação em 2026-10-17: HTTP local continua retornando 12\.1\.21/);
    assert.match(event.evidence, /startup \.NET 10\/10 e `validate-stage\.ps1` passou/);
});

test('portal keeps the active Nebula runtime mismatch and release gate explicit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-runtime-still-stale-2026-10-17-0220');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /HTTP 200 e versão 12\.1\.21/);
    assert.match(event.evidence, /fonte e stage têm SHA-256/i);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 10\/10/);
    assert.match(event.evidence, /GitHub CLI não autenticado/);
    assert.match(event.evidence, /HTTP 403 Resource not accessible by integration/);
    assert.match(event.evidence, /Causa operacional confirmada: runtime anterior/);
});

test('portal distinguishes the stale STRM scan runtime from the Python upload module issue', () => {
    const uploadEvent = statusData.events.find(item => item.id === 'server-nebula-missing-mongo-admission-runtime-2026-10-17-0922');
    const downloadEvent = statusData.events.find(item => item.id === 'server-nebula-download-runtime-stale-scan-2026-10-17-0934');

    assert.ok(uploadEvent);
    assert.match(uploadEvent.title, /upload/i);
    assert.match(uploadEvent.evidence, /não explica o Downloader STRM/i);
    assert.ok(downloadEvent);
    assert.equal(downloadEvent.status, 'in_progress');
    assert.match(downloadEvent.evidence, /get_categorized_scan_batches/);
    assert.match(downloadEvent.evidence, /586\/586/);
    assert.match(downloadEvent.evidence, /11\/11/);
    assert.match(downloadEvent.evidence, /gate global de release do roadmap permanece ativo/);
});

test('portal records CPython 3.12 dependency fix and keeps release blocked by GitHub billing', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-cp312-wheel-validation-2026-10-17-1045');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /NebulaPython 587\/587/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 11\/11/);
    assert.match(event.evidence, /1\.464 aprovados, 62 ignorados, 0 falhas/);
    assert.match(event.evidence, /407\/407 testes/);
    assert.match(event.evidence, /portal independente 57\/57, espelho 43\/43/);
    assert.match(event.evidence, /conta bloqueada por cobrança/);
    assert.match(event.evidence, /Sem tag\/release/);
    assert.match(event.evidence, /produção 12\.1\.21/);
});

test('portal keeps the Nebula download correction pending until the runtime is updated', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-runtime-version-revalidation-2026-10-17-1056');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /GET \/System\/Info\/Public respondeu 12\.1\.21/);
    assert.match(event.evidence, /fonte 12\.1\.24/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests 11\/11/);
    assert.match(event.evidence, /test_strm_downloader\.py \+ test_strm_downloader_helpers\.py 89\/89/);
    assert.match(event.evidence, /Nenhum worker, download, instalação.*ou restart executado/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('avatar admin routes publish denial and authorized-pipeline evidence without claiming production deployment', () => {
    const event = statusData.events.find(item => item.id === 'server-avatar-admin-authorization-2026-10-17-1108');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.front, 'Servidor / autorização de API');
    assert.match(event.evidence, /12\/12/);
    assert.match(event.evidence, /Matriz focada Nebula\+GetAvatar: 26\/26/);
    assert.match(event.evidence, /Jellyfin\.Api\.Tests Release: 706\/706/);
    assert.match(event.evidence, /não foi instalado nem liberado em produção|Sem instalação ou validação em produção/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula download remains pending while production uses the older worker runtime', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-runtime-stale-2026-10-17-1114');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /GET \/System\/Info\/Public continua em 12\.1\.21/);
    assert.match(event.evidence, /fonte 12\.1\.24/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 11\/11/);
    assert.match(event.evidence, /scanner\/helpers 89\/89/);
    assert.match(event.evidence, /Nenhum worker\/download real.*restart executado/);
    assert.match(event.evidence, /publicação, instalação e smoke test continuam pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula download revalidation distinguishes passing source tests from stale production runtime', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-runtime-revalidation-2026-10-17-1137');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /API local confirmou servidor 12\.1\.21/);
    assert.match(event.evidence, /fonte 12\.1\.24/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 11\/11/);
    assert.match(event.evidence, /NebulaPython 587\/587/);
    assert.match(event.evidence, /tag v12\.1\.24 ausente.*gh sem sessão/);
    assert.match(event.evidence, /Não houve download real, instalação ou restart/i);
    assert.match(event.evidence, /release e smoke test real continuam pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Nebula: downloader de produção ainda usa runtime antigo/);
});

test('Nebula download recheck publishes passing source tests but keeps production and release gates open', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-runtime-recheck-2026-10-17-1222');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /produção 12\.1\.21/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests` Release 11\/11/);
    assert.match(event.evidence, /scanner\/helpers 89\/89/);
    assert.match(event.evidence, /candidato Windows SHA-256/);
    assert.match(event.evidence, /Linux SHA-256/);
    assert.match(event.evidence, /tag v12\.1\.24 ausente/);
    assert.match(event.evidence, /não está aplicada\/homologada em produção/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula live recheck records stale production log and no active downloader child', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-live-process-log-recheck-2026-10-17-1227');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /produção 12\.1\.21/);
    assert.match(event.evidence, /nebula\.log.*11\/10\/2026 23:22:32/i);
    assert.match(event.evidence, /nenhum processo Python\/STRM\/rclone descendente/i);
    assert.match(event.evidence, /não comprova.*motivo.*falha/i);
    assert.match(event.evidence, /não houve.*iniciar worker.*download.*instalação.*restart/i);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula download investigation distinguishes worker startup from long production scan', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-start-investigation-2026-10-17-1240');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /três workers iniciados e sem progresso de mídia aparente/);
    assert.match(event.evidence, /produção 12\.1\.21/);
    assert.match(event.evidence, /fonte 12\.1\.24/);
    assert.match(event.evidence, /Python 89\/89 e C# startup\/hosted service 26\/26/);
    assert.match(event.evidence, /Não foi disparada a fila completa/);
    assert.match(event.evidence, /release\/instalação e smoke test limitado ainda pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('STRM generator package gate rejects a stage missing the required module', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-strm-generator-stage-integrity-2026-10-17-1234');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T0.3']);
    assert.match(event.evidence, /Assert-StageIntegrity\.Tests\.ps1 passou/);
    assert.match(event.evidence, /StageBuildOptions\.Tests\.ps1 passou/);
    assert.match(event.evidence, /test_generate_strm\.py \+ test_control_plane_operations\.py: 27\/27/);
    assert.match(event.evidence, /T0\.3 permanece parcial/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula download runtime recheck keeps production and release gates explicit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-runtime-recheck-2026-10-17-1943');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /produção 12\.1\.21/);
    assert.match(event.evidence, /NebulaPython 587\/587/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 11\/11/);
    assert.match(event.evidence, /run 39037932557 terminou com falha/);
    assert.match(event.evidence, /sem runner atribuído ou etapas executadas/);
    assert.match(event.evidence, /Produção não corrigida\/homologada/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula port collision log keeps production homologation open', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-port-collision-log-2026-10-17-2003');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /rclone saindo com código 0/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 15\/15/);
    assert.match(event.evidence, /12\/12/);
    assert.match(event.evidence, /não registra mídia iniciando transferência/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula upload startup conflict keeps deployment and real download pending', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-upload-mode-port-conflict-2026-10-17-2016');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests Release 15\/15/);
    assert.match(event.evidence, /22\/22/);
    assert.match(event.evidence, /restore\/concorrência Mongo isolados 20\/20/);
    assert.match(event.evidence, /Release não publicada\/instalada/);
    assert.match(event.evidence, /download real pendente/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula mount duplicate helper records deterministic result and keeps production gate open', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-mount-duplicate-result-fix-2026-10-17-2024');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /test_mount_drive_n\.py 3\/3/);
    assert.match(event.evidence, /NebulaPython 591\/591/);
    assert.match(event.evidence, /código 3 indica somente outro helper/);
    assert.match(event.evidence, /26\/26/);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /download real permanecem pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula downloader starts independently while automatic N drive retry stays gated by Envio', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-downloader-independent-start-2026-10-17-2037');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /StartDownloaderAsync/);
    assert.match(event.evidence, /retry automático de N: agora também é suprimido quando Envio\/FTP não está ativo/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests \+ NebulaHostedServiceConfigurationTests 35\/35/);
    assert.match(event.evidence, /1\.473 aprovados, 63 ignorados, 0 falhas/);
    assert.match(event.evidence, /colisão 2121 persiste/);
    assert.match(event.evidence, /smoke test de transferência e homologação de produção pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula production recheck publishes the stale runtime, port collision, and local test evidence', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-runtime-check-2026-10-18-0225');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /HTTP 200.*produção 12\.1\.21/);
    assert.match(event.evidence, /readiness antigo aceitando outro processo/);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /Python Nebula 592\/592/);
    assert.match(event.evidence, /Release 39\/39/);
    assert.match(event.evidence, /primeiro download real permanecem pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /produção segue 12\.1\.21; porta FTP impede validar o download/);
});

test('portal records v12.1.36 package evidence without closing formal homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-release-12-1-36-packaged-2026-10-27-1808');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /instalador Windows x64/);
    assert.match(event.evidence, /cross-publish em Windows/);
    assert.match(event.evidence, /Release GitHub publicada/);
    assert.match(event.evidence, /workflow hospedado falhou/);
    assert.match(event.evidence, /homologação formal permanece aberta/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Servidor 12\.1\.36 publicada com artefatos Windows e Linux/);
});

test('Nebula stream-only logs are not treated as proof that the STRM downloader started', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-streaming-vs-downloader-2026-10-18-0233');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /Modo Somente Streaming/);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /Não há \[STRM-WORKER-READY\]/);
    assert.match(event.evidence, /causa da ausência de marcadores permanece indeterminada/);
    assert.match(event.evidence, /smoke test de download real pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula downloader disabled startup remains pending and does not imply manual start or production success', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-downloader-disabled-config-check-2026-10-18-0340');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /NebulaFTP está desabilitado na configuração/);
    assert.match(event.evidence, /POST StartDownloader.*antecedem o início do processo atual/);
    assert.match(event.evidence, /uma ação manual atual não foi executada nem confirmada/);
    assert.match(event.evidence, /poderá varrer e baixar itens de toda a pasta monitorada/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula port collision log keeps production download homologation open', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-runtime-port-conflict-log-2026-10-18-0352');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /falham ao vincular `127\.0\.0\.1:2121` com WinError 10048/);
    assert.match(event.evidence, /Não aparecem `\[STRM-WORKER-READY\]`, `\[STRM-SCAN-STARTED\]`, título em fila ou transferência/);
    assert.match(event.evidence, /39\/39/);
    assert.match(event.evidence, /produção 12\.1\.21/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula autosave correction remains partial until a real STRM download is verified', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-autosave-download-check-2026-10-18-0403');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /autosaves preservam o Enabled persistido/);
    assert.match(event.evidence, /JavaScript 4\/4/);
    assert.match(event.evidence, /Python 3\.10 3\/3/);
    assert.match(event.evidence, /Release 39\/39/);
    assert.match(event.evidence, /não há readiness\/scan STRM, item de fila ou transferência/);
    assert.match(event.evidence, /nenhuma release foi criada/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.match(event.title, /autosave preserva Enabled/);
});

test('Nebula download startup log remains partial until runtime transfer is verified', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-download-start-runtime-log-2026-10-18-0427');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /Somente Streaming, que desativa feeder, fila e workers de upload/);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /startup\/configuração\/mount C# 45\/45/);
    assert.match(event.evidence, /ciclo de vida\/mount Python 9\/9/);
    assert.match(event.evidence, /suíte NebulaPython completa 592\/592/);
    assert.match(event.evidence, /execução global inconclusiva, não aprovada/);
    assert.match(event.evidence, /Nenhum serviço\/processo\/mount\/configuração.*nenhuma transferência real/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula Python mode promotion fix remains in progress until production transfer is verified', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-mode-promotion-fix-2026-10-18-0444');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /a promoção só encerrava o runtime antigo para a engine C#/);
    assert.match(event.evidence, /C# Nebula startup\/hosted\/mount Release passou 49\/49/);
    assert.match(event.evidence, /Python ciclo de vida\/health\/mount passou 16\/16/);
    assert.match(event.evidence, /GET read-only confirma produção 12\.1\.21/);
    assert.match(event.evidence, /homologação produtiva e release pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula Envio startup exception does not block downloader attempt and keeps production gate open', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-downloader-survives-envio-start-failure-2026-10-18-0542');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /continuar a tentativa independente do Downloader/);
    assert.match(event.evidence, /44\/44/);
    assert.match(event.evidence, /versão 12\.1\.21/);
    assert.match(event.evidence, /Release e download real seguem pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula stale runtime and preexisting drive stop mount retry loops without closing production gate', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-stale-runtime-mount-retries-2026-10-18-0600');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /WinError 10048/);
    assert.match(event.evidence, /código 4/);
    assert.match(event.evidence, /C# Release recompilado passou 29\/29/);
    assert.match(event.evidence, /Python 3\.10 test_mount_drive_n\.py passou 3\/3/);
    assert.match(event.evidence, /servidor local 12\.1\.21/);
    assert.match(event.evidence, /processo Python pré-existente escutando 2121\/2123/);
    assert.match(event.evidence, /release e smoke test real permanecem pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('latest Nebula operational finding is visible and distinguishes autostart from manual download', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-autostart-disabled-manual-unverified-2026-10-18-0650');
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /Enabled=false/);
    assert.match(event.evidence, /rota manual, independente via StartDownloaderAsync/);
    assert.match(event.evidence, /C# Debug 51\/51/);
    assert.match(event.evidence, /Python 3\.10 lifecycle\/downloader 23\/23/);
    assert.match(event.evidence, /sem readiness\/scan STRM, item ou transferência/);
    assert.match(event.evidence, /sem download real ou release/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.match(dashboard.getElement('event-list').innerHTML, /Nebula: autostart desativado; Downloader manual ainda não homologado/);
});

test('GetAvatar visibility fix is complete in source while production and HTTP gates stay open', () => {
    const fixed = statusData.features.find(item => item.id === 'T6.1.24');
    const pipelineGate = statusData.features.find(item => item.id === 'T6.1.23');
    const parent = statusData.features.find(item => item.id === 'T6.1');

    assert.ok(fixed);
    assert.equal(fixed.status, 'completed');
    assert.match(fixed.name, /controller 11\/11/);
    assert.match(fixed.name, /Release focal combinada 37\/37/);
    assert.match(fixed.name, /Pipeline HTTP completo da rota e produção seguem pendentes/);
    assert.equal(pipelineGate.status, 'pending');
    assert.equal(parent.status, 'pending');
    const rollback = statusData.features.find(item => item.id === 'T6.1.25');
    assert.ok(rollback);
    assert.equal(rollback.status, 'completed');
    assert.match(rollback.name, /12\/12/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.equal(statusData.coverage.total, 397);
});

test('Nebula pauses mount retries when another helper owns the lock and leaves production gate open', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-pause-conflicting-mount-retries-2026-10-18-0802');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /código 3 era considerado retryável/);
    assert.match(event.evidence, /NebulaMountRuntimeHealthTests 7\/7/);
    assert.match(event.evidence, /NebulaMount 18\/18/);
    assert.match(event.evidence, /não houve ação produtiva/i);
    assert.match(event.evidence, /produção, download real e release permanecem pendentes/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('Nebula Supabase admission audit reports evidence limits without claiming a stale lease', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-supabase-admission-audit-2026-10-18-0812');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T4.2']);
    assert.match(event.evidence, /592\/592/);
    assert.match(event.evidence, /Três testes C# Mongo compilaram e foram ignorados/);
    assert.match(event.evidence, /não identifica proprietário ou expiração da lease/);
    assert.match(server.testAudit.label, /5\.671 aprovados, 290 ignorados, 0 falhas\/5\.961/);
    assert.match(server.testAudit.label, /formal \(2\/7,/);
    assert.equal(statusData.coverage.pending, 65);
    assert.equal(statusData.coverage.inProgress, 8);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('public homologation data contains no absolute Windows user-profile paths', () => {
    const matchingFields = [];
    const userProfilePath = /[A-Za-z]:\\Users\\/i;
    const visit = (value, fieldPath = '$') => {
        if (typeof value === 'string') {
            if (userProfilePath.test(value)) matchingFields.push(fieldPath);
            return;
        }

        if (Array.isArray(value)) {
            value.forEach((entry, index) => visit(entry, `${fieldPath}[${index}]`));
            return;
        }

        if (value && typeof value === 'object') {
            for (const [key, entry] of Object.entries(value)) visit(entry, `${fieldPath}.${key}`);
        }
    };

    visit(statusData);
    assert.deepEqual(matchingFields, []);
});

test('Nebula MongoClient cleanup evidence stays visible without closing formal homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-runtime-logging-mongo-cleanup-2026-11-01-0426');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /719\/719/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Nebula: logs íntegros e fechamento MongoClient/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('Nebula TLS fail-closed evidence stays visible without granting formal homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-tls-fail-closed-2026-11-01-0432');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /sem iniciar FTP sem TLS/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Nebula: startup TLS falha fechado/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('placeholder-only item persistence evidence remains local and grants no formal coverage', async () => {
    const event = statusData.events.find(item => item.id === 'server-item-persistence-placeholder-noop-2026-11-01-0441');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ItemPersistenceServiceTests passou 34\/34/);
    assert.match(dashboard.getElement('event-list').innerHTML, /lote só com placeholders/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('related item delete audit is completed locally without promoting homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-item-related-delete-audit-2026-11-01-0444');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /8 DELETEs de providers; após o agrupamento, 1 comando/);
    assert.match(dashboard.getElement('event-list').innerHTML, /exclusões em lote das coleções relacionadas/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('related metadata batching evidence covers the 1000-item boundary without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-related-metadata-delete-batching-2026-11-01-0515');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /1.001/);
    assert.match(event.evidence, /59 aprovados, 1 ignorado/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('local server regression evidence records skips without granting formal coverage', async () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-2026-11-01-0527');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /1\.682 aprovados, 91 ignorados, 0 falhas/);
    assert.match(event.evidence, /720\/720 aprovados/);
    assert.match(event.evidence, /Sem execução de LiveIntegrationTests/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Regressão ampla local/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('restore root path replacement test records Windows evidence and Linux limitation', async () => {
    const event = statusData.events.find(item => item.id === 'server-restore-root-path-replacement-2026-11-01-0532');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /FullSystemBackup passou 155, ignorou 16 e falhou 0/);
    assert.match(event.evidence, /ramo não foi executado nesta máquina/);
    assert.match(event.evidence, /auditoria TOCTOU global permanece aberta/);
    assert.match(dashboard.getElement('event-list').innerHTML, /troca do caminho da raiz aberta/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('provider media writers filesystem audit completes without formal coverage', async () => {
    const event = statusData.events.find(item => item.id === 'server-provider-filesystem-write-audit-2026-11-01-0539');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.location, /LyricManager\.cs, Subtitles\/SubtitleManager\.cs e Manager\/ImageSaver\.cs/);
    assert.match(event.evidence, /LyricManager e SubtitleManager validam antes de criar\/abrir por pathname/);
    assert.match(event.evidence, /ImageSaver usa FileMode\.Create/);
    assert.match(event.evidence, /raízes distintas: biblioteca, metadata interno e configuração de perfil/);
    assert.match(event.evidence, /Rejeitar links indiscriminadamente pode quebrar bibliotecas configuradas/);
    assert.match(event.evidence, /Nenhuma corrida foi reproduzida/);
    assert.match(event.evidence, /correção runtime permanece pendente/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Auditoria dos gravadores de mídia/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('localized poster replacement evidence stays visible without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-image-language-replacement-refresh-2026-11-01-1611');
    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /116 aprovados/);
    assert.match(event.evidence, /ReplaceAllImages=true/);
    assert.match(event.evidence, /sem crédito de gate/);
    assert.equal(statusData.serverHomologation?.formalSignoff ?? 'pending', 'pending');
    assert.equal(statusData.tracks.find(track => track.id === 'server').homologation.percent, 28.6);
});

test('PR #1 CI billing block stays visible without granting formal coverage', async () => {
    const event = statusData.events.find(item => item.id === 'server-pr1-ci-investigation-blocked-auth-2026-11-01');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'blocked');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /16 jobs falharam/);
    assert.match(event.evidence, /billing/);
    assert.match(event.evidence, /sem logs de teste/);
    assert.match(event.evidence, /41005367220.*41283703952|41283703952.*41005367220/);
    assert.match(event.evidence, /BlobNotFound/);
    assert.match(dashboard.getElement('event-list').innerHTML, /PR #1: checks bloqueados por cobrança/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('PR #1 check failure is recorded without attributing a cause when GitHub exposes no steps or logs', () => {
    const event = statusData.events.find(item => item.id === 'server-pr1-checks-no-steps-no-logs-2026-11-02-1520');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'blocked');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /16 jobs.*steps vazias.*runner não atribuído.*BlobNotFound.*sem causa confirmada/is);
    assert.match(event.release, /6\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('Mídia Storage Online path writes remain an audit finding without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-midia-storage-file-write-audit-2026-11-01-0542');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.location, /MidiaStorageOnlineSyncTask\.cs e Api\/MidiaStorageOnlineController\.cs/);
    assert.match(event.evidence, /MidiaStorageOnlineSyncTaskTests passou 8\/8/);
    assert.match(event.evidence, /não cobrem symlinks, troca concorrente do diretório/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Auditoria Mídia Storage Online/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('profile image deletion policy remains open without promoting formal homologation', async () => {
    const event = statusData.events.find(item => item.id === 'server-profile-image-delete-boundary-audit-2026-11-01-0631');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /exceção específica e a ACL do ambiente de produção não foram capturadas/);
    assert.match(event.evidence, /passaram 20\/20 em Windows\/\.NET 10 Release/);
    assert.match(event.evidence, /Sem mudança runtime, produção, crédito formal, release ou publicação remota/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Imagem de perfil: validar limite seguro/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('focused server regression rerun records platform and database limits', async () => {
    const event = statusData.events.find(item => item.id === 'server-focused-regression-revalidation-2026-11-01-0637');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ItemPersistenceServiceTests passou 34\/34/);
    assert.match(event.evidence, /Nebula Python passou 720\/720/);
    assert.match(event.evidence, /BackupRestoreDirectoryTests passou 47 e ignorou 11 cenários Linux/);
    assert.match(event.evidence, /MariaDB integrado não executado/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Revalidação focal do servidor/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('OMDb cache path rejects invalid IMDb identifiers without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-omdb-cache-path-id-validation-2026-11-01-0545');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /OmdbProviderPathSafetyTests e regressões Omdb passaram 20\/20/);
    assert.match(event.evidence, /slash, backslash, traversal/);
    assert.match(event.evidence, /não fecha a auditoria separada de escrita por pathname/);
    assert.match(dashboard.getElement('event-list').innerHTML, /OMDb: validação do IMDb ID antes do caminho de cache/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('AudioDB cache paths require canonical MusicBrainz UUIDs without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-audiodb-cache-mbid-validation-2026-11-01-0547');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /AudioDbCachePathSafetyTests passou 7\/7/);
    assert.match(event.evidence, /Guid\.TryParseExact no formato D/);
    assert.match(event.evidence, /não criam chamada HTTP com ID inválido/);
    assert.match(dashboard.getElement('event-list').innerHTML, /AudioDB: MBID validado antes dos caminhos de cache/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('Mídia Storage Online logo URL collisions are fixed without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-midia-online-logo-url-collision-2026-11-01-0724');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /MidiaStorageOnlineLogoLibraryTests passou 2\/2/);
    assert.match(event.evidence, /867 aprovados, 13 ignorados por integrações externas, 0 falhas/);
    assert.match(event.evidence, /Arquivos de logo antigos sem hash não foram removidos/);
    assert.match(dashboard.getElement('event-list').innerHTML, /distingue logos por URL e evita gravação parcial/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('Mídia Storage Online manifest cleanup confines paths without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-midia-online-manifest-path-confinement-2026-11-01-0729');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /MidiaStorageOnlineManifestPathTests passaram 13\/13/);
    assert.match(event.evidence, /não resolve symlinks\/junctions ou TOCTOU/);
    assert.match(dashboard.getElement('event-list').innerHTML, /confina a limpeza pelos caminhos do manifesto/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('remote image selection prefers the server locale without replacing saved artwork automatically', async () => {
    const event = statusData.events.find(item => item.id === 'server-image-language-server-precedence-2026-11-01-0732');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /Jellyfin\.Model\.Tests passou 699\/699; Jellyfin\.Providers\.Tests passou 887, ignorou 13 integrações externas e teve 0 falhas \(900 total\)/);
    assert.match(event.evidence, /Refresh comum não substitui imagens já salvas/);
    assert.match(dashboard.getElement('event-list').innerHTML, /Imagens remotas priorizam idioma configurado no servidor/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('Nebula large-file queue threshold evidence stays local without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-large-lane-threshold-2026-11-01-0827');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /Teste focal 9\/9; suíte Nebula Python 724\/724, cobertura 93%/i);
    assert.match(event.evidence, /Candidata 12\.1\.46 sem release/);
    assert.match(dashboard.getElement('event-list').innerHTML, /atualiza a profundidade da faixa de arquivos grandes após inserir/);
    assert.equal(statusData.tracks.find(item => item.id === 'server').homologation.completed, 2);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('ImageSaver rooted deletion records Windows evidence while platform coverage stays open', async () => {
    const event = statusData.events.find(item => item.id === 'server-imagesaver-metadata-prefix-boundary-audit-2026-11-01-0906');
    assert.ok(event);
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ImageSaverTests 4 aprovados, 1 ignorado\/5; RootedFileSystemTests 4 aprovados, 1 ignorado\/5/);
    assert.match(event.evidence, /preserva metadata-backup, que é pasta irmã/);
    assert.match(event.evidence, /Linux\/macOS continuam fail-closed por PlatformNotSupportedException/);
    assert.match(event.release, /sem crédito formal \(2\/7\)/i);
    assert.match(dashboard.getElement('event-list').innerHTML, /ImageSaver: exclusão sob raiz e proteção contra renomeação/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
});

test('TMDb image region fix is recorded without formal or release credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-tmdb-image-region-retained-2026-11-01-1008');
    const server = statusData.tracks.find(track => track.id === 'server');
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /iso_639_1 e iso_3166_1/);
    assert.match(event.evidence, /TmdbUtilsTests 32\/32.*Jellyfin\.Providers\.Tests 907 aprovados, 14 ignorados, 0 falhas\/921/);
    assert.match(event.evidence, /API TMDb ao vivo.*produção não verificados/);
    assert.match(event.release, /candidata; release não publicada.*Homologação formal 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.match(dashboard.getElement('event-list').innerHTML, /TMDb: preservar região real das imagens em todos os idiomas/);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});

test('portal records atomic Mídia Storage Online manifest persistence without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-mso-manifest-atomic-write-2026-11-01-1101');
    const server = statusData.tracks.find(track => track.id === 'server');
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /912 aprovados, 14 ignorados, 0 falhas\/926/);
    assert.match(event.evidence, /focal 3\/3.*Mídia Storage Online 40\/40/);
    assert.match(event.evidence, /concorrentes ainda podem sobrescrever snapshots/);
    assert.match(event.release, /2\/7.*sem crédito.*não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.match(dashboard.getElement('event-list').innerHTML, /persistência atômica do manifesto/);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});
test('portal mirror records atomic image replacement without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-imagesaver-atomic-replacement-2026-11-01-1036');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.equal(event?.status, 'completed');
    assert.equal(event?.progressPercent, 100);
    assert.match(event?.evidence ?? '', /File.Replace conserva ACL Windows/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('portal mirror records handle-relative restore verification without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-backup-restore-handles-windows-revalidation-2026-11-01-1045');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.equal(event?.status, 'completed');
    assert.equal(event?.progressPercent, 100);
    assert.match(event?.evidence ?? '', /FullSystemBackup recompilado e passou 155, ignorou 16 e falhou 0\/171/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('Mídia Storage Online writer audit stays open without formal credit', async () => {
    const event = statusData.events.find(item => item.id === 'server-midia-storage-filesystem-audit-2026-11-01-1048');
    const server = statusData.tracks.find(track => track.id === 'server');
    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /RootedFileSystem não é chamado pelo Mídia Storage Online/);
    assert.match(event.evidence, /share mask 0x3, sem FILE_SHARE_DELETE/);
    assert.match(event.evidence, /Nenhum exploit ou teste de código executado nesta sessão/);
    assert.match(event.release, /2\/7.*sem crédito.*não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.match(dashboard.getElement('event-list').innerHTML, /inventário de gravações por pathname/);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});
test('Mídia Storage Online keeps promotion failure primary when temp cleanup also fails', () => {
    const event = statusData.events.find(item => item.id === 'server-mso-atomic-cleanup-exception-precedence-2026-11-01-1137');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /UnauthorizedAccessException em TemporaryFileCleanupException/);
    assert.match(event.release, /Homologação formal permanece 2\/7, sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});

test('server homologation mirror records current local regression without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-root-containment-regression-2026-11-01-1151');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /5.465 aprovados, 95 ignorados, 0 falhas\/5.560/);
    assert.match(event.evidence, /Nebula Python 724\/724/);
    assert.match(event.evidence, /Linux.*MariaDB\/Mongo opt-in.*produção permanecem pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.equal(server.testAudit.skipped, 290);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});
test('server image-language audit mirrors Jellyfin comparison without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-language-jellyfin-comparison-2026-11-01-1209');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /ProviderManager 3\/3.*refresh locale\/fallback 26\/26.*TMDb utils 32\/32.*RemoteImageController 8\/8/);
    assert.match(event.evidence, /MetadataService legado sem proveniência/);
    assert.match(event.release, /sem crédito formal.*2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});

test('legacy metadata image language audit does not infer language from text metadata', () => {
    const event = statusData.events.find(item => item.id === 'server-legacy-image-language-provenance-2026-11-01-1218');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /MetadataResult\.RemoteImages contém URL e ImageType/);
    assert.match(event.evidence, /ResultLanguage descreve texto e não deve ser usado como idioma da capa/);
    assert.match(event.evidence, /Model EnumerableExtensionsTests: 21\/21; Providers .*23\/23/);
    assert.match(event.release, /não inferir idioma.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});

test('Mídia Storage Online sync lease serializes only writers sharing a manifest', () => {
    const event = statusData.events.find(item => item.id === 'server-mso-sync-serialization-2026-11-01-1232');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /caminho canonicalizado.*roots diferentes continuam independentes/);
    assert.match(event.evidence, /cancelamento e liberação após exceção/);
    assert.match(event.release, /Homologação formal permanece 2\/7.*sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});

test('server audit mirror records current full regression without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-nebula-2026-11-01-1510');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.543 aprovados, 273 ignorados, 0 falhas\/5\.816/);
    assert.match(event.evidence, /Nebula Python 3\.10:.*726\/726/);
    assert.match(event.release, /4\/10.*aguarda completar dez/);
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.equal(server.testAudit.skipped, 290);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
});


test('server audit mirror records rooted ImageSaver gap without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-imagesaver-rooted-write-gap-2026-11-01-1520');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /13 entradas.*dois callers: AvatarService e ImageController/);
    assert.match(event.evidence, /ImageSaver ainda cria e promove arquivos por pathname/);
    assert.match(event.release, /2\/7.*sem crédito.*Snapshot local 5\/10/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('server audit mirror records Next Up exact-count scan cost without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nextup-exact-count-query-cost-audit-2026-11-01-1524');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /enableTotalRecordCount=true.*materializa chaves e episódios de todas as séries/);
    assert.match(event.evidence, /8 aprovados, 1 ignorado por MariaDB opt-in/);
    assert.match(event.release, /snapshot local 6\/10.*Preservado o contrato público de TotalRecordCount/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('server mirror records exact-count Next Up branch coverage without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nextup-exact-count-branch-coverage-2026-11-01-1530');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /Limit nulo.*TotalRecordCount=2.*Limit=1/);
    assert.match(event.evidence, /9 aprovados, 1 ignorado por MariaDB opt-in/);
    assert.match(event.release, /Homologação formal 2\/7.*Snapshot local 7\/10/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records complete .NET solution regression without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-full-solution-regression-2026-11-01-1537');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.544 aprovados, 286 ignorados e 0 falhas\/5\.830 em 17 assemblies/);
    assert.match(event.evidence, /MariaDB\/Mongo com opt-in/);
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.equal(server.testAudit.skipped, 290);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.testAudit.percent, 95.1);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal mirror records the rooted image-writer boundary audit without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-imagesaver-rooted-primitive-boundary-audit-2026-11-01-1549');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.local, /47 aprovados, 11 ignorados por plataforma, 0 falhas\/58/);
    assert.match(event.evidence, /BackupRestoreDirectory/);
    assert.match(event.evidence, /pathname-based|por pathname/);
    assert.match(event.release, /snapshot local 9\/10.*deployment público 128b39a READY/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the ImageSaver writer test boundary without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-imagesaver-write-regression-revalidation-2026-11-01-1553');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /4 aprovados, 1 ignorado, 0 falhas\/5/);
    assert.match(event.evidence, /modo Unix retorna cedo/);
    assert.match(event.evidence, /pathname-based|por pathname/);
    assert.match(event.release, /Lote 10\/10.*homologação permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records Nebula link isolation and atomic studio cache without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-symlink-and-studio-cache-2026-11-01-1629');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /51\/51/);
    assert.match(event.evidence, /3\/3/);
    assert.match(event.evidence, /corrida|pathname-based/i);
    assert.match(event.release, /3\/10.*release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records People mapping serialization without MariaDB or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-people-mapping-serialization-2026-11-01-1637');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /três comandos SQL distintos em BaseItemImageInfos.*retentativa após deadlock/);
    assert.match(event.evidence, /um comando final em PeopleBaseItemMap.*chave duplicada/);
    assert.match(event.evidence, /mensagens repetidas.*não representam operações adicionais/);
    assert.match(event.release, /4\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the preexisting People mapping test as pending MariaDB evidence', () => {
    const event = statusData.events.find(item => item.id === 'server-people-mapping-existing-link-concurrency-2026-11-01-1708');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 70);
    assert.match(event.local, /2 testes MariaDB focais ignorados/);
    assert.match(event.evidence, /semeia pessoa e vínculo.*oito atualizações simultâneas/);
    assert.match(event.evidence, /não valida MariaDB nem o lock/);
    assert.match(event.release, /8\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records nested MariaDB deadlock retries as an unverified risk', () => {
    const event = statusData.events.find(item => item.id === 'server-image-savechanges-transaction-deadlock-retry-audit-2026-11-01-1712');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 45);
    assert.match(event.evidence, /transação explícita.*cinco retries.*retry externo/);
    assert.match(event.evidence, /hipótese não validada/);
    assert.match(event.release, /9\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records atomic AudioDB cache downloads without formal or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-audiodb-atomic-cache-2026-11-01-1643');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /10 aprovados/);
    assert.match(event.evidence, /930 aprovados, 14 ignorados, 0 falhas\/944/);
    assert.match(event.release, /5\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records optional notification sidecars without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-notification-sidecar-optional-2026-11-01-1651');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /NebulaTelegramNotifierTests 14\/14/);
    assert.match(event.evidence, /exigia NFO e capa existentes/);
    assert.match(event.evidence, /132 expirações.*sem discriminar/);
    assert.match(event.release, /6\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records notification timeout diagnostics without private paths or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-notification-timeout-diagnostics-2026-11-01-1658');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /NebulaTelegramNotifierTests 14\/14/);
    assert.match(event.evidence, /NomePreenchido.*SnapshotEstavel.*CapaDisponivel.*NfoDisponivel/);
    assert.match(event.evidence, /sem caminho nem título/);
    assert.match(event.release, /7\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records People mapping collation fix without formal or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-people-map-role-collation-2026-11-01-1730');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /PeopleId.*Role.*collation MariaDB/);
    assert.match(event.evidence, /lower_case_table_names=1/);
    assert.match(event.local, /UpdatePeople_ 3\/3.*MariaDB 11\.4\.4 descartável/);
    assert.match(event.release, /3\/10.*2\/7.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal records the local NuGet vulnerability audit without formal homologation or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nuget-vulnerability-audit-2026-11-01-1759');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /--vulnerable --include-transitive --no-restore/);
    assert.match(event.evidence, /não listou pacotes vulneráveis/);
    assert.match(event.release, /4\/10.*2\/7.*sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});


test('portal records the rooted image writer design audit without runtime or formal release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-rooted-image-writer-design-audit-2026-11-01-1807');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, undefined);
    assert.match(event.evidence, /dirfds.*rename não impede que diretório aberto seja movido/);
    assert.match(event.evidence, /Sem teste de corrida.*produção/);
    assert.match(event.release, /5\/10.*2\/7.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the continuous server filesystem review without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-filesystem-technique-review-2026-11-01-1824');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.local, /espelho 123\/123.*dedicado 463\/463/);
    assert.match(event.evidence, /122 arquivos-fonte.*411 ocorrências/);
    assert.match(event.evidence, /temporário no diretório de destino.*movimento entre volumes/);
    assert.match(event.release, /6\/10.*2\/7.*sem release/);
    assert.match(event.evidence, /122 arquivos-fonte.*411 ocorrências/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});
test('portal records the Release build analyzer fix without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-release-build-analyzer-fix-2026-11-01-1838');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, null);
    assert.match(event.local, /dotnet build MulletaFlix.sln Release.*0 erros/);
    assert.match(event.local, /5\.560 aprovados.*275 ignorados.*0 falhas\/5\.835/);
    assert.match(event.evidence, /SA1117.*build completo Release passou/);
    assert.match(event.release, /7\/10.*2\/7.*não publicada/);
    if (statusData.serverHomologation) {
        assert.match(statusData.serverHomologation.latestAudit, /18:38 BRT.*5\.560 aprovados.*7\/10/);
    }
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the cross-platform image-writer design audit without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-writer-cross-platform-design-2026-11-01-1934');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Linux openat2.*5\.6.*EAGAIN.*ENOSYS/);
    assert.match(event.evidence, /FILE_RENAME_INFO\.RootDirectory/);
    assert.match(event.local, /Linux não foi validado/);
    assert.match(event.release, /9\/10.*2\/7.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records selective Nebula checkpoint retries without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-checkpoint-transient-retry-2026-11-01-1953');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /focais de upload 27\/27.*suíte Nebula Python completa 728\/728/);
    assert.match(event.evidence, /OperationFailure permanente.*ServerSelectionTimeoutError/);
    assert.match(event.evidence, /Sem Mongo\/Telegram reais.*produção/);
    assert.match(event.release, /10\/10.*2\/7.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records idempotent HTTP 429 retries without increasing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-http-429-idempotent-retry-2026-11-01-2111');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /StartupHttpRetryPolicyTests 8\/8.*dotnet build.*0 erros/);
    assert.match(event.evidence, /Retry-After.*GET.*POST não/i);
    assert.match(event.evidence, /8 respostas 429 não o abrem/i);
    assert.match(event.release, /4\/10.*2\/7.*12\.1\.46 candidata.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the EF Core array Contains compatibility fix without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-ef9-csharp14-array-contains-2026-11-01-2142');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /reproduziu a falha antes.*passou 1\/1/);
    assert.match(event.evidence, /EF Core 9\.0\.0 e Pomelo 9\.0\.0/);
    assert.match(event.evidence, /EF Core 9\.0\.x.*EF 10/);
    assert.match(event.evidence, /nenhuma conexão MariaDB foi aberta/);
    assert.match(event.release, /7\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the C# 14 EF array Contains audit and its incomplete broad suite', () => {
    const event = statusData.events.find(item => item.id === 'server-ef-array-contains-sweep-2026-11-01-2152');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /teste Pomelo.*1\/1.*Jellyfin\.Server\.Tests.*interrompida/);
    assert.match(event.evidence, /nove callsites.*Enumerable\.Contains/);
    assert.match(event.evidence, /Nenhum crédito foi dado à homologação formal/);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records MariaDB attachment rollback without retry or homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-attachment-replace-atomicity-2026-11-01-2211');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ServerPersistenceMariaDbTests 16\/16.*MariaDB 11\.4\.4 descartável/);
    assert.match(event.evidence, /BEFORE INSERT após DELETE.*conjunto anterior persiste/i);
    assert.match(event.evidence, /não retry, deadlock ou confirmação de commit ambíguo/i);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, sem release publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records PeopleRepository rollback in the next local batch without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-people-role-replacement-rollback-2026-11-01-2228');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ServerPersistenceMariaDbTests 20\/20.*MariaDB 11\.4\.4 descartável/);
    assert.match(event.evidence, /BEFORE INSERT.*papel original.*rollback/i);
    assert.match(event.evidence, /não retry, deadlock nem commit ambíguo/i);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records image replace rollback in MariaDB without formal or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-images-save-rollback-2026-11-01-2240');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ServerPersistenceMariaDbTests 21\/21.*MariaDB 11\.4\.4 descartável/);
    assert.match(event.evidence, /BEFORE INSERT.*ExecuteDelete.*ID, caminho e idioma.*imagem anterior/i);
    assert.match(event.release, /2\/10.*2\/7.*12\.1\.46 candidata.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records UserData key preservation after MariaDB rollback without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-userdata-key-mutation-rollback-2026-11-01-2252');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /focal 1\/1.*ServerPersistenceMariaDbTests 22\/22.*UserDataManagerTests 1\/1/);
    assert.match(event.evidence, /referência do batch cache.*duas chaves.*terceira/i);
    assert.match(event.evidence, /rollback.*ausência de evento.*preservação da chave de entrada/i);
    assert.match(event.release, /3\/10.*formal.*2\/7.*12\.1\.46 candidata.*sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records the real MariaDB image transaction retry without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-transaction-lockwait-retry-2026-11-01-2314');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ServerPersistenceMariaDbTests 23\/23.*focal.*1\/1/);
    assert.match(event.evidence, /recebeu 1205.*sinal de retry.*tentativa nova.*persistiu/i);
    assert.match(event.evidence, /datadir temporário.*produção :3306.*intocada/i);
    assert.match(event.release, /4\/10.*formal.*2\/7.*12\.1\.46 candidata, sem release publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records bounded exact-count NextUp paging without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nextup-bounded-exact-count-2026-11-01-2344');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /TVSeriesManagerNextUpPaginationTests 5\/5.*NextUpQueryOptimizationTests 4\/4/);
    assert.match(event.evidence, /lotes \[50, 1\].*total exato 51.*página StartIndex=1\/Limit=2 estável/i);
    assert.match(event.evidence, /PriorityQueue da BCL.*O\(log K\)/i);
    assert.match(event.evidence, /GetNextUpSeriesKeys ainda materializa todas as chaves.*não houve benchmark/i);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46 candidata, sem release publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal records why exact-count NextUp does not keyset-page mutable keys', () => {
    const event = statusData.events.find(item => item.id === 'server-nextup-keyset-snapshot-safety-2026-11-01-2357');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /LastPlayedDate.*mutável.*omissão, repetição ou desvio no total/i);
    assert.match(event.evidence, /mesma transação REPEATABLE READ/i);
    assert.match(event.release, /7\/10.*2\/7.*12\.1\.46 candidata, sem release publicada/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records Nebula unused vulnerable dependencies removed without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-remove-unused-image-dependencies-2026-11-02-0011');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /test_check_deps\.py 31\/31.*Nebula completa 728\/728.*pip-audit.*nenhum aviso conhecido/);
    assert.match(event.evidence, /39 advisories.*Pillow 13\.0\.0.*Python 3\.10/);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records Telegram time probe redirect protection without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-telegram-time-no-redirect-2026-11-02-0050');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /focal 1\/1.*Nebula 790\/790/);
    assert.match(event.evidence, /probe Telegram recusa redirects/i);
    assert.match(event.evidence, /usa o relógio local/i);
    assert.match(event.release, /1\/10.*homologação formal permanece 2\/7.*12\.1\.46 candidata.*não publicada/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra a recuperação da fila de refresh durante um lote sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-provider-refresh-queue-ownership-2026-11-02-0123');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /teste focal 1\/1.*931 aprovados, 14 ignorados/);
    assert.match(event.evidence, /antes da correção.*timeout.*segundo refresh iniciou/s);
    assert.match(event.release, /4\/10.*2\/7.*não foram verificados/s);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra refresh de imagem por item serializado sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-provider-refresh-item-serialization-2026-11-02-0132');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /932 aprovados, 14 ignorados, 0 falhas\/946/);
    assert.match(event.evidence, /reproduziu o defeito antes da correção/i);
    assert.match(event.evidence, /itens diferentes continuam em paralelo/);
    assert.match(event.release, /5\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra regressão integral do servidor e Nebula sem homologar produção', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-image-writer-audit-2026-11-02-0140');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /17 TRX, 5\.584 aprovados, 285 ignorados, 0 falhas\/5\.869.*Nebula.*791\/791/);
    assert.match(event.evidence, /filtro excluiu LiveIntegrationTests/);
    assert.match(event.evidence, /File\.OpenHandle também recebe pathname/);
    assert.match(event.evidence, /proteção completa precisa cobrir criação de diretórios/);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.equal(server.testAudit.skipped, 290);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the ImageSaver platform-contract audit without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-writer-platform-contracts-2026-11-02-0144');
    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /SMB.*RootDirectory zero/i);
    assert.match(event.evidence, /não foi implementada/i);
    assert.match(event.release, /7\/10.*não publicado/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra auditoria de idioma TMDb sem crédito formal de homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-tmdb-image-language-selection-audit-2026-11-02-0157');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /busca todos os idiomas.*ordena por locale preferido/s);
    assert.match(event.evidence, /variantes regionais não são uniformes/i);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra os limites de segurança de unlinkat sob rename concorrente', () => {
    const event = statusData.events.find(item => item.id === 'server-rooted-delete-linux-rename-race-audit-2026-11-02-0202');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /descritor continua referindo.*após rename/i);
    assert.match(event.evidence, /permanece fail-closed fora do Windows/i);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra retomada Nebula vinculada a ETag sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-http-range-resume-etag-2026-11-02-0213');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /focais 126\/126.*Nebula 799\/799/);
    assert.match(event.evidence, /If-Range.*HTTP 206/i);
    assert.match(event.release, /10\/10.*2\/7.*12\.1\.46 continua candidata/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra correção do snapshot de imagens após espera pelo lock sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-saveimages-snapshot-after-item-lock-2026-11-02-0223');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /captura de ImageInfos.*região protegida/i);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 continua candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra persistência atômica de configurações preservando ACLs', () => {
    const event = statusData.events.find(item => item.id === 'server-config-atomic-persist-acl-2026-11-02-0229');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /ServerConfigurationManagerFilePermissionsTests 5 aprovados, 1 ignorado.*ACL temporária protegida antes da serialização.*NamedConfigurationConcurrencyTests 33\/33/);
    assert.match(event.evidence, /temporário.*Administradores.*SYSTEM.*File\.Replace.*DACL\/ACL.*backup.*recuperar.*File\.Move/s);
    assert.match(event.evidence, /bytes originais.*falha na serialização/i);
    assert.match(event.evidence, /rotina de recuperação confirma restauração/i);
    assert.match(event.evidence, /destino está ausente/i);
    assert.match(event.evidence, /preserva destino novo/i);
    assert.match(event.release, /2\/10.*2\/7.*12\.1\.46 continua candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a auditoria de migração da resiliência HTTP sem homologar produção', () => {
    const event = statusData.events.find(item => item.id === 'server-http-resilience-modernization-audit-2026-11-02-0244');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /Microsoft\.Extensions\.Http\.Polly/);
    assert.match(event.evidence, /Microsoft\.Extensions\.Http\.Resilience/);
    assert.match(event.evidence, /janela\/razão.*cinco falhas consecutivas/s);
    assert.match(event.release, /3\/10.*2\/7.*12\.1\.46 continua candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra retry de capítulos em andamento sem crédito de homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-chapter-mariadb-lock-retry-2026-11-02-0252');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 80);
    assert.match(event.local, /projeto .* compilado.*teste MariaDB foi ignorado/i);
    assert.match(event.evidence, /DbContext\/transação novos.*três tentativas.*1205\/1213/s);
    assert.match(event.release, /4\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra retry de keyframes em andamento sem crédito de homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-keyframe-mariadb-lock-retry-2026-11-02-0302');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 80);
    assert.match(event.local, /projeto .* compilado.*teste MariaDB foi ignorado/i);
    assert.match(event.evidence, /contexto e transação assíncronos.*1205\/1213.*três tentativas/s);
    assert.match(event.release, /5\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a suíte de backup estável sem crédito formal e marca CI bloqueada por cobrança', () => {
    const event = statusData.events.find(item => item.id === 'server-backup-storage-provider-tests-2026-11-02-0316');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /5\.589 aprovados, 300 ignorados e 0 falhas/);
    assert.match(event.evidence, /IStorageSpaceProvider.*jobs .*falharam antes de iniciar por bloqueio de cobrança/s);
    assert.match(event.release, /6\/10.*2\/7/);
    assert.match(event.release, /candidata a 12\.1\.46/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a avaliação do Dev Proxy como proposta sem crédito de homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-devproxy-http-pipeline-test-audit-2026-11-02-0337');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /ConnectCallback.*não foi provada/);
    assert.match(event.evidence, /RetryAfterPlugin.*LatencyPlugin/);
    assert.match(event.release, /7\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra o teste de cancelamento do Retry-After sem homologar produção', () => {
    const event = statusData.events.find(item => item.id === 'server-http-retry-cancellation-2026-11-02-0342');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /StartupHttpRetryPolicyTests.*passou 1\/1/);
    assert.match(event.evidence, /não comprova pipeline.*produção/);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra correção de charset do teste do servidor sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-test-source-utf8-2026-11-02-0345');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /dotnet format whitespace Jellyfin.Server.Tests --verify-no-changes passou/);
    assert.match(event.evidence, /sem mudança textual no conteúdo/);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra o retry dos três clientes HTTP via transporte loopback sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-http-clients-local-pipeline-retry-2026-11-02-0347');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /StartupHttpClientRegistrationTests passou 3\/3/);
    assert.match(event.evidence, /Default, MusicBrainz e DirectIp|IHttpClientFactory/);
    assert.match(event.evidence, /não proxy, TLS interceptado.*produção/);
    assert.match(event.release, /10\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra proxy HTTP via callback Happy Eyeballs sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-http-proxy-happy-eyeballs-2026-11-02-0416');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /proxy HTTP de loopback.*4\/4/);
    assert.match(event.evidence, /GET absoluto.*unresolvable\.invalid.*HTTP 200/);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra túnel HTTPS interceptado via callback sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-http-tls-proxy-happy-eyeballs-2026-11-02-0420');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /StartupHttpClientRegistrationTests passou 5\/5/);
    assert.match(event.evidence, /CONNECT unresolvable\.invalid:443.*GET \/media.*não houve conexão externa/s);
    assert.match(event.release, /2\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra a auditoria de writers XML sem marcar persistência como corrigida', () => {
    const event = statusData.events.find(item => item.id === 'server-xml-writers-atomicity-audit-2026-11-02-0427');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 50);
    assert.match(event.local, /SaveConfiguration.*SaveLibraryOptions.*ConfigurationHelper.*nenhuma suíte C# dos writers foi executada/i);
    assert.match(event.evidence, /[Cc]onfigs de plugin podem conter segredos.*ACLs/);
    assert.match(event.release, /3\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.coverage.inProgress, 8);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra CI do PR sem steps como diagnóstico inconclusivo', () => {
    const event = statusData.events.find(item => item.id === 'server-pr-ci-no-steps-diagnostic-2026-11-02-0438');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 50);
    assert.match(event.local, /StartupHttpClientRegistrationTests.*22\/22/);
    assert.match(event.evidence, /sem steps.*BlobNotFound.*causa permanece desconhecida/);
    assert.match(event.evidence, /cbc380c9.*e50f7a8f/);
    assert.match(event.release, /4\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.coverage.inProgress, 8);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a pesquisa inicial de métricas dos health checks', () => {
    const event = statusData.events.find(item => item.id === 'server-health-check-otel-metrics-research-2026-11-02-0445');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /AddTelemetryHealthCheckPublisher.*não implementado/);
    assert.match(event.evidence, /\/health.*\/ready.*dotnet\.health_check\.reports.*modo push/);
    assert.match(event.evidence, /linha 9\.0\.0.*alvo é \.NET 10/);
    assert.match(event.release, /5\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.coverage.inProgress, 8);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra métricas de health checks implementadas sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-health-check-otel-metrics-implementation-2026-11-02-0456');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /HealthChecks\.Common 9\.0\.0.*passou 3\/3/);
    assert.match(event.evidence, /somente quando `MetricsEnabled`.*[Ss]em endpoint.*30 s.*produção/s);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.coverage.inProgress, 8);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra refresh de imagens por idioma e recuperação de falha no read-ahead', () => {
    const event = statusData.events.find(item => item.id === 'server-image-language-nebula-readahead-2026-11-02-0515');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.scope, /idioma preferido.*fallback.*falha do produtor/s);
    assert.match(event.local, /Imagens existentes sem idioma explícito.*preservadas/i);
    assert.ok(event.release.includes('7/10') && event.release.includes('2/7') && event.release.includes('12.1.46 candidata, não publicada'));
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra auditoria multi-processo sem alegar correção ou crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-user-admin-cross-process-audit-2026-11-02-0549');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /SemaphoreSlim por processo.*nenhum teste de concorrência multi-instância/i);
    assert.match(event.evidence, /AuthenticateUser grava.*DbContext\.Update\(user\).*produção multi-processo não foi consultada/s);
    assert.match(event.release, /lote 10\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra recarga das permissões após autenticação externa', () => {
    const event = statusData.events.find(item => item.id === 'server-auth-refresh-permissions-after-provider-2026-11-02-0608');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /falhou antes da mudança.*passou depois da recarga/s);
    assert.match(event.evidence, /MariaDB multi-instância.*produção não foram validados/s);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata, não publicada/s);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});
test('portal records the latest .NET solution TRX regression without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-solution-regression-trx-2026-11-02-0701');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.649, executed\/passed 5\.554, falhas 0, 95 não executados\/ignorados/);
    assert.match(event.evidence, /MariaDB\/MongoDB.*Linux.*produção/);
    assert.match(event.release, /8\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.testAudit.passed, 5671);
    assert.equal(server.testAudit.total, 5961);
    assert.equal(server.testAudit.skipped, 290);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});
test('portal registra a serialização concorrente de options.xml sem alegar atomicidade', () => {
    const event = statusData.events.find(item => item.id === 'server-library-options-concurrent-serialization-2026-11-02-0708');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /CollectionFolderTests 2\/2.*81 aprovados, 1 ignorado.*0 falhas\/82/);
    assert.match(event.evidence, /Lock é local ao processo.*não torna escrita atômica/s);
    assert.match(event.evidence, /Linux\/SMB, CI hospedada e produção não foram validados/);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});
test('portal registra substituição atômica de options.xml sem crédito formal', () => {
    const event = statusData.events.find(item => item.id === 'server-library-options-atomic-save-2026-11-02-0713');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.scope, /temporário irmão.*ACL Windows.*modo Unix/s);
    assert.match(event.evidence, /Falha de serialização mantém o arquivo anterior.*File.Replace.*File.Move/s);
    assert.match(event.evidence, /modo POSIX está incluído mas não foi executado.*Linux em runtime.*produção/s);
    assert.match(event.release, /10\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra auditoria do writer XML de bootstrap sem alegar correção', () => {
    const event = statusData.events.find(item => item.id === 'server-bootstrap-config-direct-write-audit-2026-11-02-0717');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /único caller interno.*FileMode\.Create.*truncar/s);
    assert.match(event.evidence, /temporário protegido.*File\.Replace.*Nenhuma correção foi aplicada/s);
    assert.match(event.release, /1\/10.*sem mudança funcional.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra writer atômico compartilhado no bootstrap e na API pública', () => {
    const event = statusData.events.find(item => item.id === 'server-bootstrap-config-safe-writer-2026-11-02-0721');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.scope, /fluxos interno e público.*writer atômico protegido.*assinatura pública/);
    assert.match(event.evidence, /caller interno.*sobrecarga pública.*PersistConfigurationAtomically.*assinatura pública não mudou/s);
    assert.match(event.release, /2\/10.*12\.1\.46.*sem publicação.*2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra a persistência protegida de configurações de plugins', () => {
    const event = statusData.events.find(item => item.id === 'server-plugin-config-protected-writer-2026-11-02-0745');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.scope, /writer protegido.*restringindo o temporário antes de serializar segredos/);
    assert.match(event.evidence, /Falha parcial do serializer mantém o XML anterior.*modo POSIX/s);
    assert.match(event.evidence, /ACL Windows/);
    assert.match(event.evidence, /Linux real, SMB.*produção não foram verificados/);
    assert.match(event.release, /3\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});


test('portal registra as migrações XML protegidas sem crédito de homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-prestartup-xml-atomic-migrations-2026-11-02-0754');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /BasePluginConfigurationPersistenceTests 4\/4.*PreStartupXmlMigrationPersistenceTests 3\/3/);
    assert.match(event.evidence, /arquivo anterior.*CertificatePassword.*vEthernet\*.*XML inválido/);
    assert.match(event.release, /4\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra retry HTTP limitado a métodos idempotentes', () => {
    const event = statusData.events.find(item => item.id === 'server-http-retry-idempotent-methods-2026-11-02-0854');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /19\/19.*POST.*503.*uma requisição/s);
    assert.match(event.evidence, /método não idempotente não é repetido.*circuit breaker por cliente nomeado/s);
    assert.match(event.evidence, /compartilham o mesmo circuit breaker.*abertura após cinco falhas foi comprovada/s);
    assert.match(event.evidence, /chamadas com IHttpClientFactory sem nome não foram alteradas/);
    assert.match(event.release, /9\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra piloto MTP sem alterar runner padrão nem homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-mtp-isolated-pilot-2026-11-02-0953');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Jellyfin\.Networking\.Tests passou 124\/124 em VSTest e MTP.*1\.769 ms.*1\.301 ms/s);
    assert.match(event.evidence, /coverlet\.MTP.*filtros VSTest.*Linux, macOS.*não foram verificados/s);
    assert.match(event.release, /4\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra a remoção de consultas bootstrap sem plugins na inicialização', () => {
    const event = statusData.events.find(item => item.id === 'server-plugin-bootstrap-empty-catalog-startup-2026-11-02-1002');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /BootstrapPluginCatalogTests.*PluginManagerTests.*21\/21/);
    assert.match(event.evidence, /PluginIds vazias.*HttpClient.*100 segundos.*Não foi medido startup real em produção/s);
    assert.match(event.release, /5\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra sanitização dos logs do feeder e helper obrigatório no stage Windows', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-feeder-safe-console-stage-2026-11-02-1050');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Nebula Python 806\/806.*test_feed_ftp_strm\.py 96\/96.*Assert-StageIntegrity/);
    assert.match(event.evidence, /exceções.*CR\/LF.*logging_safety\.py/s);
    assert.match(event.release, /10\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra saída segura do SupabaseSync sem alterar SQL ou dados do backup', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-supabase-cli-safe-console-2026-11-02-1055');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Nebula Python 806\/806.*test_supabase_sync\.py 77\/77.*SQL schema/);
    assert.match(event.evidence, /nome de banco.*CR\/LF.*SQL schema.*multilinha intencional/s);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra auditoria SSRF STRM e preserva o bloqueio de homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-strm-ssrf-compatibility-audit-2026-11-02-1013');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /41\.650 arquivos STRM.*porta local 2124.*nenhum URL com userinfo/i);
    assert.match(event.evidence, /loopback.*interromperia o fluxo normal/i);
    assert.match(event.evidence, /vincular a conexão ao IP efetivamente aprovado/i);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra auditoria de logging Nebula sem alegar vazamento confirmado ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-log-data-exposure-audit-2026-11-02-1021');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /41\.650 STRMs.*nenhum URL com userinfo.*apenas a chave id/i);
    assert.match(event.evidence, /não foi reproduzido vazamento de segredo/i);
    assert.match(event.evidence, /sanitizar CR\/LF/i);
    assert.match(event.release, /7\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra persistência resiliente das playlists externas sem homologar SMB ou produção', () => {
    const event = statusData.events.find(item => item.id === 'server-playlist-atomic-persistence-2026-11-02-1116');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /PlaylistManagerTests.*6 aprovados.*1 ignorado/);
    assert.match(event.evidence, /symlink.*Linux.*SMB.*produção/s);
    assert.ok(event.release.includes('4/10') && event.release.includes('2/7') && event.release.includes('12.1.46 candidata, não publicada'));
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra regressão ampla C# sem tratar os ignorados como homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-mtp-audit-2026-11-02-1125');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.ok(event.local.includes('17 assemblies') && event.local.includes('5.636 aprovados') && event.local.includes('288 ignorados') && event.local.includes('0 falhas/5.924'));
    assert.ok(event.evidence.includes('LiveIntegrationTests') && event.evidence.includes('Linux/macOS') && event.evidence.includes('produção'));
    assert.ok(event.release.includes('5/10') && event.release.includes('2/7') && event.release.includes('12.1.46 candidata, não publicada'));
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra o inventário STRM e mantém a política SSRF pendente sem interromper fontes locais', () => {
    const event = statusData.events.find(item => item.id === 'server-strm-redirect-origin-inventory-2026-11-02-1135');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /41\.650 STRMs válidos.*127\.0\.0\.1:2124.*sem userinfo/);
    assert.match(event.local, /Python 3\.13\.7 sem pytest/);
    assert.match(event.evidence, /STREAM_PORT configurável com padrão 2122.*nenhum listener.*produção.*DNS rebinding/s);
    assert.match(event.evidence, /Não foi lido \.env nem consultada produção/);
    assert.match(event.release, /6\/10.*2\/7.*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra o inventário de Contains para C# 14 e EF sem creditar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-ef-csharp14-contains-inventory-2026-11-02-1150');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /arrays capturados usam Enumerable\.Contains explícito/);
    assert.match(event.local, /BaseItemRepositoryTagQueryTranslationTests 1\/1/);
    assert.match(event.evidence, /dotnet\/docs #43952.*EF Core #37176/s);
    assert.match(event.evidence, /Linux, MariaDB, CI hospedada, runtime instalado e produção não foram validados/);
    assert.match(event.release, /7\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra correção async do cache de keyframes HLS e mantém pendências de homologação explícitas', () => {
    const event = statusData.events.find(item => item.id === 'server-hls-keyframe-sync-db-playback-audit-2026-11-02-1157');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /CreateMainPlaylistAsync.*GetKeyframeDataAsync.*SaveKeyframeDataAsync/s);
    assert.match(event.evidence, /FFprobe.*ReadLineAsync\(CancellationToken\)/s);
    assert.match(event.evidence, /Matroska\/NEbml síncrono/);
    assert.match(event.evidence, /Keyframes 7\/7.*HLS 21\/21/s);
    assert.match(event.evidence, /MariaDB isolado.*produção não validados/);
    assert.match(event.release, /8\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra a auditoria do parser EBML síncrono sem inventar progresso ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-hls-ebml-async-investigation-2026-11-02-1226');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(Object.hasOwn(event, 'progressPercent'), false);
    assert.match(event.evidence, /DynamicHlsPlaylistGenerator.*metadata-based.*NEbml.Core 1.1.0.5/s);
    assert.match(event.evidence, /IKeyframeExtractor async padr[aã]o chama TryExtractKeyframes s[ií]ncrono/);
    assert.match(event.evidence, /Task.Run mant[eé]m bloqueio de worker/);
    assert.ok(event.release.includes('Evento local 9/10 do lote; snapshot ainda não publicado no Git. Homologação formal 2/7 (28,6%); release 12.1.46 candidata, não publicada.'));
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});


test('portal registra o leitor EBML assíncrono e explicita a matriz de validação pendente', () => {
    const event = statusData.events.find(item => item.id === 'server-hls-ebml-async-reader-2026-11-02-1234');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /Keyframes 10\/10.*HLS 22\/22/);
    assert.match(event.evidence, /ReadExactlyAsync.*CancellationToken/);
    assert.match(event.evidence, /fixture Matroska sintética.*mesmos ticks/);
    assert.match(event.evidence, /[Bb]enchmark.*mídia real.*produção não foram verificados/);
    assert.match(event.release, /10\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra fallback de imagens após timeout sem confundir cancelamento ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-image-provider-timeout-fallback-2026-11-02-1244');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /ItemImageProviderTests 88\/88.*940 aprovados.*14 ignorados.*0 falhas/is);
    assert.match(event.evidence, /TimeoutException interna.*cancelamento explícito.*Linux.*produção/is);
    assert.match(event.release, /1\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra reconciliação segura de renomeação no backup Supabase', () => {
    const event = statusData.events.find(item => item.id === 'server-supabase-user-rename-reconcile-2026-11-02-1257');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /NebulaSupabaseSyncTests 60\/60.*1\.731 aprovados.*92 ignorados.*0 falhas/);
    assert.match(event.evidence, /on_conflict.*chave primária.*UNIQUE.*23505.*falha antes de qualquer escrita/s);
    assert.match(event.evidence, /troca cíclica de nomes permanece não suportada.*produção não foram validados/s);
    assert.match(event.release, /2\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra diagnóstico limitado do código PostgREST sem expor o corpo', () => {
    const event = statusData.events.find(item => item.id === 'server-supabase-cursor-error-code-2026-11-02-1311');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /NebulaSupabaseSyncTests 62\/62.*1\.733 aprovados.*92 ignorados.*0 falhas/);
    assert.match(event.scope, /Manter os retries.*somente o código.*cursor de arquivos e do histórico/);
    assert.match(event.evidence, /4 KiB.*message, details ou hint.*PGRST000.*CR\/LF/);
    assert.match(event.release, /3\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});


test('portal registra correção de CueTrackPositions múltiplas nos leitores Matroska', () => {
    const event = statusData.events.find(item => item.id === 'server-matroska-multitrack-cues-2026-11-02-1322');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Keyframes.Tests 14\/14.*Hls.Tests 22\/22/);
    assert.match(event.evidence, /CueTrackPositions como repetível.*áudio antes e depois do vídeo.*síncrono e assíncrono/);
    assert.match(event.release, /4\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal registra regressão ampla da solução .NET sem elevar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-solution-regression-2026-11-02-1329');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /dotnet test MulletaFlix\.sln.*Release.*exit code 0.*17 assemblies: 5\.660 aprovados, 288 ignorados, 0 falhas\/5\.948/s);
    assert.match(event.local, /Server\.Implementations 1\.733\/1\.825.*Server 127\/127.*API 823\/826/s);
    assert.match(event.evidence, /TRX nomeados por assembly.*corrigido teste de heartbeat.*teste de restore.*Linux, CI hospedada, MariaDB\/MongoDB reais, runtime instalado e produção não foram validados/is);
    assert.match(event.release, /5\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.deepEqual(server.testAudit, {
        passed: 5671,
        total: 5961,
        skipped: 290,
        failed: 0,
        percent: 95.1,
        label: 'Regressão .NET Release em Windows/.NET 10: 17 assemblies, 5.671 aprovados, 290 ignorados, 0 falhas/5.961; filtro FullyQualifiedName!~LiveIntegrationTests. Percentual é taxa de aprovação dos casos incluídos, não completude funcional nem homologação formal (2/7, 28,6%). Nebula Python: 820 aprovados, 1 ignorado/821 em Windows/Python 3.14.7; coverage.py combinado linha/branch: 82% em main, FTP e tools. CI hospedada, Linux/macOS, bancos opt-in, runtime instalado e produção permanecem pendentes.'
    });
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
});

test('portal registra revisão de tecnologias HTTP sem promover migração ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-http-resilience-and-polly-maintenance-review-2026-11-02-1346');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /Microsoft.Extensions.Http.Polly.*obsoleto.*handler padrão altera retries.*Polly oficial 8\.9\.0/is);
    assert.match(event.evidence, /OSMF apenas para referência direta.*Receita\/termos aplicáveis.*não foram verificados/is);
    assert.match(event.release, /6\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/);
    assert.equal(statusData.events.length, 416);
    assert.equal(event.updatedAt, '2026-11-02T13:46:00-03:00');
    assert.equal(statusData.events.at(-1).updatedAt, statusData.generatedAt);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra a auditoria de batching sem marcar homologação nem release como concluídas', () => {
    const event = statusData.events.find(item => item.id === 'server-library-alternate-version-batching-gate-2026-11-02-1402');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /LibraryManagerDeleteAsyncTests 4\/4.*1\.734 aprovados, 92 ignorados/is);
    assert.match(event.evidence, /protótipo foi removido.*frequência\/duração.*MariaDB descartável/is);
    assert.match(event.release, /7\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra o gate de equivalência HTTP sem elevar homologação nem aprovar migração', () => {
    const event = statusData.events.find(item => item.id === 'server-http-resilience-contract-audit-2026-11-02-1412');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /StartupHttpRetryPolicyTests \+ StartupHttpClientRegistrationTests 20\/20/);
    assert.match(event.evidence, /handler padr[aã]o repete todos os m[eé]todos.*PUT\/DELETE.*breaker cl[aá]ssico/s);
    assert.match(event.evidence, /pipeline customizado.*Linux.*produ[cç][aã]o permanecem pendentes/i);
    assert.match(event.release, /8\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, n[aã]o publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
    assert.equal(statusData.events.at(-1).updatedAt, statusData.generatedAt);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra auditoria dos consumidores das probes sem alterar homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-health-probe-consumer-audit-2026-11-02-1418');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Jellyfin\.Server\.Tests Health 14\/14.*portal 224\/224/i);
    assert.match(event.evidence, /\/health executa todos os checks.*\/ready filtra.*Docker usa \/health.*updater Windows usam \/ready/is);
    assert.match(event.evidence, /não encontrou \/live.*rotas existentes foram preservadas.*produção não foi verificada/is);
    assert.match(event.release, /9\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
    assert.equal(statusData.events.at(-1).updatedAt, statusData.generatedAt);
    assert.match(statusData.latestAudit, /06\/11\/2026.*CI #526.*17 checks falhos antes de iniciar.*bloqueio de cobrança.*252\/252.*416 eventos, lote 6\/10.*2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra regressão ampla .NET e Nebula sem concluir homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-dotnet-nebula-2026-11-02-1435');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.local, /17 assemblies, 5\.660 aprovados, 288 ignorados, 0 falhas\/5\.948.*Nebula Python 3\.10\.11: 806\/806/s);
    assert.match(event.evidence, /LiveIntegrationTests.*17 arquivos únicos.*Python.*MariaDB.*Mongo.*Linux.*produção permanecem pendentes/is);
    assert.match(event.release, /10\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
    assert.equal(statusData.events.at(-1).updatedAt, statusData.generatedAt);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra a validação de ACL das playlists sem concluir homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-playlist-windows-acl-regression-2026-11-02-1447');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /PlaylistManagerTests 7 aprovados, 1 ignorado.*ACL Windows 1\/1/is);
    assert.match(event.evidence, /ACE explícita.*File\.Replace.*teste POSIX de modo foi ignorado/is);
    assert.match(event.evidence, /logs dos jobs retornam 404.*head atual do PR não tem workflow run/is);
    assert.match(event.release, /1\/10 do lote.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
    assert.equal(statusData.events.at(-1).updatedAt, statusData.generatedAt);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra auditoria HLS Matroska sem concluir homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-hls-matroska-validation-audit-2026-11-02-1454');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Keyframes\.Tests 14\/14.*Hls\.Tests 22\/22.*nenhum arquivo de vídeo encontrado/i);
    assert.match(event.evidence, /paridade sync\/async.*cancelamento.*CueTrackPositions.*não mídia de vídeo.*nem justificativa.*dependências/i);
    assert.match(event.release, /2\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(statusData.generatedAt, statusData.events.at(-1).updatedAt);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra o limite de rename rooted em SMB sem homologar o servidor', () => {
    const event = statusData.events.find(item => item.id === 'server-image-writer-smb-compatibility-2026-11-02-1503');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ImageSaver.*pathname.*RootDirectory=0.*openat2.*sem mudança estrutural/is);
    assert.match(event.release, /3\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra o contrato HTTP de Retry-After sem declarar migração ou homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-http-resilience-contract-validation-2026-11-02-1507');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /StartupHttpRetryPolicyTests 10\/10.*1m09s/i);
    assert.match(event.evidence, /Retry-After por delta e data HTTP.*não repetição de POST.*recuperação após 30 s.*semântica diferente/is);
    assert.match(event.release, /4\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra FakeTimeProvider como oportunidade condicionada à migração da política HTTP', () => {
    const event = statusData.events.find(item => item.id === 'server-http-retry-virtual-time-review-2026-11-02-1512');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /FakeTimeProvider.*TimeProvider.*Polly clássico.*não virtualiza/is);
    assert.match(event.evidence, /sem dependência, código ou mudança de runtime/i);
    assert.match(event.release, /5\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra documentação XML completa do restore Nebula sem mudar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-restore-xml-doc-warning-2026-11-02-1529');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /0 avisos, 0 erros/i);
    assert.match(event.release, /7\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra oportunidade TRX de CI sem alegar diagnóstico nem homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-trx-artifact-audit-2026-11-02-1531');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /LogFilePrefix.*sem etapas ou runner/is);
    assert.match(event.release, /8\/10.*implementação de CI pendente.*homologação formal 2\/7/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra os testes opt-in Nebula em Mongo descartável sem validar produção', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-mongodb-optin-2026-11-02-1535');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /MongoDB 8\.3 isolado.*27099.*30\/30/i);
    assert.match(event.evidence, /remoção.*bloqueada.*TEMP.*27017 e 3306 não foram usadas/i);
    assert.match(event.release, /9\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra preferência regional de imagens sem elevar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-image-regional-language-refresh-2026-11-02-1542');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /ItemImageProviderTests.*94\/94.*pt-PT.*pt-BR/is);
    assert.match(event.release, /10\/10.*homologação formal 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra timeout por chamada no Nebula sem homologar produção', () => {
    const event = statusData.events.find(item => item.id === 'nebula-socket-timeout-scope-2026-11-02-1704');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Nebula Python: 806\/806.*proíbem setdefaulttimeout global/i);
    assert.match(event.evidence, /altera o timeout dos novos sockets.*urlopen já passa timeout explicitamente.*não se mediu impacto em produção/is);
    assert.match(event.release, /6\/10.*homologação formal permanece 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra limite de seleção Mongo para geração STRM sem homologar produção', () => {
    const event = statusData.events.find(item => item.id === 'nebula-strm-mongo-selection-timeout-2026-11-02-1714');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /14\/14.*serverSelectionTimeoutMS.*3000/is);
    assert.match(event.evidence, /30000 ms.*não foi adicionado timeout de operação\/socket.*não validados/is);
    assert.match(event.release, /7\/10.*homologação formal permanece 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.ok(Date.parse(statusData.generatedAt) >= Date.parse(event.updatedAt));
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra o piloto PyMongo Async sem alegar ganho nem homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-pymongo-async-playback-pilot-2026-11-02-1724');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /test_stream_service 4\/4.*timeout esperado.*close\(\)/i);
    assert.match(event.evidence, /pode melhorar ou degradar.*nenhum ganho.*sem Mongo real, benchmark, Linux, CI hospedada ou produção/i);
    assert.match(event.release, /8\/10.*homologação formal permanece 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra migração PyMongo Async do Nebula principal e Supabase', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-pymongo-async-main-supabase-2026-11-02-1738');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /suíte Nebula 808\/808.*focais.*131\/131/i);
    assert.match(event.evidence, /remove io_loop.*Motor deprecated.*sem ganho presumido.*não houve acesso ao Mongo real, benchmark, Linux, CI hospedada ou produção/i);
    assert.match(event.release, /9\/10.*homologação formal permanece 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra o teste de lease Nebula contra Mongo real sem creditar homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-mongo-admission-real-ci-2026-11-02-1854');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /809 aprovados, 1 ignorado.*URI 7 aprovados.*contrato CI 2\/2/i);
    assert.match(event.evidence, /MongoDB 7.*não houve benchmark.*produção nem homologação formal/i);
    assert.match(event.release, /lote local 10\/10.*não publicado.*homologação formal permanece 2\/7.*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra cleanup de falhas no startup Nebula sem creditar homologação formal', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-startup-queue-failure-cleanup-2026-11-03-0014');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.local, /Python 3\.14\.7.*819 aprovados, 1 ignorado\/820.*lifecycle 22\/22/i);
    assert.match(event.evidence, /Falha normal.*encerra recursos.*retorna código 1.*Cancelamento.*propaga CancelledError.*16 jobs.*bloqueio de cobrança/is);
    assert.match(event.release, /correção em 93d8a020.*snapshot.*branch do PR em 32b04fc8.*aguarda merge em main.*2\/7 \(28,6%\).*12\.1\.46 candidata/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra a auditoria de idempotência Telegram sem creditar homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-telegram-idempotent-recovery-audit-2026-11-03-0047');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 30);
    assert.match(event.evidence, /Bot API sendDocument.*não recebe random_id.*MTProto messages\.sendMedia.*preservação da fonte/i);
    assert.match(event.release, /4\/10.*414 eventos.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal registra retomada de partes Telegram checkpointadas sem creditar homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-checkpointed-part-recovery-2026-11-03-0055');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /não apaga mais o arquivo local.*reenfileira.*sem chamada de envio.*continua pendente/i);
    assert.match(event.release, /415 eventos.*lote 5\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});


test('portal registra reservas idempotentes por parte no Nebula sem creditar homologação', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-telegram-random-id-reservations-2026-11-03-0105');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 70);
    assert.match(event.evidence, /random_id.*identidade do bot.*SHA-256.*\$addToSet.*retries preservam bot e random_id/i);
    assert.match(event.evidence, /823 aprovados, 1 ignorado.*Sem envio Telegram real.*produção/i);
    assert.match(event.release, /416 eventos.*lote 6\/10.*2\/7 \(28,6%\).*12\.1\.46 candidata, não publicada/i);
    assert.equal(statusData.events.length, 416);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
