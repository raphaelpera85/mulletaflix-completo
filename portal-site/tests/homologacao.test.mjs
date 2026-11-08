import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const statusData = JSON.parse(readFileSync(new URL('../homologacao-status.json', import.meta.url), 'utf8'));
const dashboardSource = readFileSync(new URL('../homologacao.js', import.meta.url), 'utf8');
const homologationHtml = readFileSync(new URL('../homologacao.html', import.meta.url), 'utf8');
const serverCiWorkflow = readFileSync(new URL('../../.github/workflows/ci.yml', import.meta.url), 'utf8');

test('registra a restauração oficial 12.1.51 sem promover aceite autenticado', () => {
    const event = statusData.events.find(item => item.id === 'runtime-installed-1251-official-backup-restore-complete-2026-11-07-0445');

    assert.ok(event);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /Restored primary and media-domain databases/);
    assert.match(event.evidence, /Startup complete.*04:35:44/);
    assert.match(event.evidence, /users=16.*baseitems=194203.*peoplebaseitemmap=850927.*activitylogs=2671/is);
    assert.match(event.evidence, /Username=Raphael count=1/);
    assert.match(event.evidence, /Login real e navegação autenticada não foram executados/);
});

test('registra o recheck atual dos bloqueios de ambiente sem promover homologação', () => {
    const eventId = 'environment-blockers-reprobe-2026-11-08-0153';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /8096.*MulletaFlix\.exe.*12\.1\.51/is);
    assert.match(event.evidence, /3306.*MariaDB.*produção/is);
    assert.match(event.evidence, /27017.*MongoDB.*produção/is);
    assert.match(event.evidence, /ADB.*não listou dispositivos/is);
    assert.match(event.evidence, /WSL.*sem distribuições/is);
    assert.match(event.release, /não promove homologação global/is);
});

test('registra a reexecução da suíte web sem esconder a flakiness do harness', () => {
    const eventId = 'web-vitest-flake-reprobe-2026-11-08-0212';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /ActionLog passou 4\/4/);
    assert.match(event.evidence, /três reexecuções amplas consecutivas passaram 78 arquivos e 425\/425 testes/);
    assert.match(event.evidence, /getComputedStyle.*window.*indisponível/is);
    assert.match(event.release, /flakiness do harness.*melhoria aberta/is);
});

test('registra o baseline ESLint web sem converter legado em GREEN', () => {
    const eventId = 'web-eslint-baseline-2026-11-08-0226';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /1\.415 problemas.*1\.305 erros e 110 warnings/is);
    assert.match(event.evidence, /lint focal.*240 problemas.*226 erros e 14 warnings/is);
    assert.match(event.evidence, /build:check.*Vitest continuam verdes/is);
    assert.match(event.release, /Não promover o lint como GREEN/is);
});

test('registra os gates locais de artefato e estilo sem promover runtime', () => {
    const eventId = 'web-build-artifact-gates-2026-11-08-0229';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /1\.928 arquivos válidos.*1\.536 KiB/is);
    assert.match(event.evidence, /295,8 KB gzip.*420 KB/is);
    assert.match(event.evidence, /stylelint.*passou sem findings/is);
    assert.match(event.release, /não valida CI hospedada.*runtime Kestrel/is);
});

test('registra o TDD do parser linear dos artefatos web', () => {
    const eventId = 'web-build-artifact-parser-tdd-2026-11-08-0236';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /RED:.*export ausente.*GREEN:.*passou 3\/3/is);
    assert.match(event.evidence, /ESLint focal.*passou/);
    assert.match(event.evidence, /verify:build.*build:check.*425\/425/is);
    assert.match(event.release, /não promove CI.*runtime Kestrel.*produção/is);
});

test('registra o TDD do nome de stage isolado sem promover o harness', () => {
    const eventId = 'web-playwright-stage-name-tdd-2026-11-08-0247';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /RED:.*helper ainda não existia.*GREEN:.*passou 1\/1/is);
    assert.match(event.evidence, /verify:build.*build:check.*425\/425/is);
    assert.match(event.evidence, /2 findings existentes.*TEMP\/TMP/is);
    assert.match(event.release, /não valida ACL.*runtime Kestrel.*produção/is);
});

test('registra o TDD do ambiente temporário sem certificar ACL ou execução Playwright', () => {
    const eventId = 'web-playwright-stage-env-tdd-2026-11-08-0254';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /RED:.*não era exportado.*GREEN:.*passou 2\/2/is);
    assert.match(event.evidence, /ESLint focal passou.*verify:build.*build:check.*425\/425/is);
    assert.match(event.release, /não certifica ACL.*execução Playwright.*produção/is);
});

test('registra o reprobe read-only da ACL do stage e preserva resíduos para decisão posterior', () => {
    const eventId = 'web-playwright-temp-acl-reprobe-2026-11-08-0257';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /icacls.*somente SYSTEM.*Administradores.*Raphael/is);
    assert.match(event.evidence, /tasklist.*somente MulletaFlix\.exe instalado PID 19932/is);
    assert.match(event.evidence, /171 diretórios stage antigos.*nenhum foi removido/is);
    assert.match(event.release, /não prova execução completa.*retenção\/limpeza explícita/is);
});

test('registra o TDD de cleanup do stage sem apagar o stage padrão', () => {
    const eventId = 'web-playwright-stage-cleanup-tdd-2026-11-08-0305';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /RED:.*não existia.*GREEN:.*suíte focal passou 9\/9/is);
    assert.match(event.evidence, /verify:build.*build:check.*425\/425/is);
    assert.match(event.release, /sem tocar no stage-data padrão.*171 resíduos/is);
});

test('registra o probe real do cleanup sem promover a suíte Playwright', () => {
    const eventId = 'web-playwright-stage-cleanup-probe-2026-11-08-0308';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(event.front, 'web');
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /status=no-specs.*specCount=0.*stageReachable=true/is);
    assert.match(event.evidence, /removeu o diretório fornecido.*nenhum Node\/Playwright\/Vite\/dotnet ficou ativo/is);
    assert.match(event.release, /não a suíte Playwright completa.*171 diretórios antigos/is);
});

test('roadmap counters match the current checklist totals', () => {
    const { completed, inProgress, pending, total } = statusData.coverage;

    assert.equal(completed + inProgress + pending, total);
    assert.match(statusData.coverage.definition, /não equivale à homologação formal/);
    assert.ok(homologationHtml.includes('id="coverage-source"'));
    assert.ok(dashboardSource.includes("byId('coverage-source').textContent = `${coverage.definition"));
});

test('records metadata saver cancellation propagation without crediting formal homologation', () => {
    const eventId = 'server-metadata-saver-cancellation-2026-11-03-1633';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /SaveMetadataAsync passaram 2\/2/);
    assert.match(event.evidence, /Jellyfin\.Providers\.Tests sem Integration passou 930\/930/);
    assert.match(event.evidence, /5\.557 aprovados, 98 ignorados, 0 falhas\/5\.655/);
    assert.match(event.release, /Runtime instalado 12\.1\.45 não contém a mudança/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records local image validation failure without advancing refresh date', () => {
    const eventId = 'server-local-image-failure-refresh-stamp-2026-11-03-1644';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /DateLastRefreshed avançando/);
    assert.match(event.evidence, /MetadataServiceTests passou 51\/51/);
    assert.match(event.release, /Runtime instalado 12\.1\.45 não contém a mudança/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records refresh stamp persistence when a full refresh has no providers', () => {
    const eventId = 'server-refresh-stamp-persistence-2026-11-03-1658';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /UpdateItemAsync não era chamado/);
    assert.match(event.evidence, /MetadataServiceTests passou 52\/52/);
    assert.match(event.evidence, /Jellyfin\.Providers\.Tests sem Integration passou 932\/932/);
    assert.match(event.evidence, /5\.559 aprovados, 98 ignorados, 0 falhas\/5\.657/);
    assert.match(event.evidence, /build Release passou 0 erros/);
    assert.match(event.release, /Runtime instalado 12\.1\.45 não contém a mudança/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records repeated image deadlock evidence without crediting formal homologation', () => {
    const eventId = 'server-image-deadlock-log-runtime-audit-2026-10-27-0307';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /52 Passed, 0 Skipped, 0 Failed/);
    assert.match(event.evidence, /DLL instalada 12\.1\.35 não contém os marcadores do lock/);
    assert.match(event.evidence, /integração MariaDB não executada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records duplicate image deadlock log rerun without crediting formal homologation', () => {
    const eventId = 'server-image-deadlock-duplicate-log-rerun-2026-10-27-1332';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /passou 54\/54 no checkout atual/);
    assert.match(event.evidence, /não identifica o binário emissor nem prova produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records NextUp MariaDB tie-break coverage without crediting formal homologation', () => {
    const eventId = 'server-nextup-mariadb-tie-ci-2026-10-27-1030';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /foi ignorado porque não havia endpoint MariaDB descartável/);
    assert.match(event.release, /sem comprovação ainda da execução contra Pomelo\/MariaDB/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records NextUp SQL selection audit without claiming a database optimization', () => {
    const eventId = 'server-nextup-sql-selection-audit-2026-10-27-1058';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /NextUpQueryOptimizationTests passou 8\/8/);
    assert.match(event.release, /não implementa nem mede a redução SQL/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records isolated MariaDB runtime lifecycle without crediting NextUp or formal homologation', () => {
    const eventId = 'server-mariadb-disposable-runtime-lifecycle-2026-10-27-1103';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /StartStopStart_UsesTheSameIsolatedDataDirectory passou 1\/1/);
    assert.match(event.release, /Não executa NextUp\/Pomelo, EXPLAIN, benchmark/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records NextUp SQL candidate selection and nullable-position regression without formal credit', () => {
    const eventId = 'server-nextup-mariadb-candidate-selection-2026-10-27-1124';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /passou 1\/1 contra a instância MariaDB embutida/);
    assert.match(event.evidence, /ROW_NUMBER\(\) com filtro <= 1/);
    assert.match(event.release, /EXPLAIN, latência, memória ou carga de catálogo/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records SQL-backed NextUp rewatch selection without formal or performance credit', () => {
    const eventId = 'server-nextup-rewatch-sql-selection-2026-10-27-1222';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /passou 1\/1 após corrigir o seed/);
    assert.match(event.evidence, /6 séries\/19 episódios/);
    assert.match(event.evidence, /inclui posição nula antes do próximo episódio numerado/);
    assert.match(event.evidence, /NextUpQueryOptimizationTests passou 8\/8/);
    assert.match(event.evidence, /Rerun ANALYZE FORMAT=JSON mediu 0,501 ms no seletor não assistido e 0,568 ms no rewatch/);
    assert.match(event.evidence, /exige LastPlayedDate e ROW_NUMBER\(\) OVER na consulta capturada/);
    assert.match(event.release, /não representam benchmark nem ganho de produção/);
    assert.match(event.release, /sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records MariaDB ANALYZE on a small NextUp fixture without claiming production performance', () => {
    const eventId = 'server-nextup-analyze-small-fixture-2026-10-27-1145';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /contagens observadas incluindo 4 e 16 linhas/);
    assert.match(event.evidence, /0,429 ms nesta execução/);
    assert.match(event.release, /aliases internos do plano não são critérios do teste/);
    assert.match(event.release, /não é benchmark de biblioteca real/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records deterministic NextUp rewatch ties without formal homologation credit', () => {
    const eventId = 'server-nextup-rewatch-tie-break-2026-10-27-1201';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /maior episódio venceu/);
    assert.match(event.evidence, /maior Guid/);
    assert.match(event.evidence, /LastPlayedDate nulo perdeu para um timestamp não nulo/);
    assert.match(event.evidence, /posição nula perdeu para posição válida em empate de data/);
    assert.match(event.release, /Não é release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records full local .NET Release regression separately from formal homologation', () => {
    const eventId = 'server-dotnet-full-regression-2026-10-27-0321';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /15 assemblies: 5\.434 Passed, 98 Skipped, 0 Failed, total 5\.532/);
    assert.match(event.evidence, /exit code 0/);
    assert.match(event.evidence, /Integration foram excluídos pelo filtro/);
    assert.match(event.release, /Sem release, commit, push ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records focused Windows restore regression without crediting skipped Linux gates', () => {
    const eventId = 'server-restore-focused-windows-regression-2026-10-27-0327';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /136 Passed, 16 Skipped, 0 Failed\/152/);
    assert.match(event.evidence, /ACL\/symlink\/mount Linux/);
    assert.match(event.evidence, /WSL, Docker e Podman não estão disponíveis/);
    assert.match(event.evidence, /13 jobs concluídos com failure; a API não expõe steps nem logs/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records backup and Nebula authorization audit without closing the full endpoint gate', () => {
    const eventId = 'server-admin-backup-nebula-auth-audit-2026-10-27-0330';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /passou 33\/33, 0 ignorados\/falhas/);
    assert.match(event.evidence, /7 rotas enumeradas: anônimo retorna 401, usuário comum 403/);
    assert.match(event.evidence, /ClaimTypes.Role=Administrator/);
    assert.match(event.evidence, /T6\.1 continua parcial/);
    assert.match(event.release, /Sem release, commit, push ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records expanded admin HTTP route coverage without closing T6.1', () => {
    const eventId = 'server-activity-playback-admin-route-coverage-2026-10-27-0332';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /33\/33, 0 ignorados\/falhas/);
    assert.match(event.evidence, /GET \/System\/ActivityLog\/Entries/);
    assert.match(event.evidence, /GET \/PlaybackReports\/Entries/);
    assert.match(event.evidence, /\/Stats/);
    assert.match(event.evidence, /\/Export/);
    assert.match(event.evidence, /401 anônimo e 403 para usuário comum/);
    assert.match(event.release, /T6\.1 segue parcial/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records user and billing MariaDB coverage without crediting the Windows-only skip', () => {
    const eventId = 'server-user-billing-mariadb-windows-2026-10-26-2211';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /31 Passed, 1 Skipped, 0 Failed\/32/);
    assert.match(event.evidence, /porta dinâmica 60327, bind 127\.0\.0\.1, datadir exclusivo em TEMP/);
    assert.match(event.evidence, /MariaDB operacional PID 31236\/3306 não foi conectado/);
    assert.match(event.release, /[Nn]ão altera o agregado auditável 5\.465\/5\.493 nem a homologação formal 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records O(1) item persistence lookups without changing formal homologation or audit totals', () => {
    const eventId = 'server-item-persistence-o1-lookups-2026-10-26-2219';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /1\.707 Passed, 94 Skipped, 0 Failed\/1\.801/);
    assert.match(event.evidence, /HashSet/);
    assert.match(event.evidence, /Nenhum benchmark foi executado/);
    assert.match(event.release, /sem release ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the selected Release regression separately from formal homologation', () => {
    const eventId = 'server-release-regression-selected-tests-2026-10-26-2206';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /idêntico ao incidente já registrado/);
    assert.match(event.evidence, /5\.501 casos selecionados: 5\.403 Passed, 98 Skipped, 0 Failed/);
    assert.match(event.evidence, /Dois assemblies de integração não tiveram casos compatíveis/);
    assert.match(event.release, /não equivale à homologação formal/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records reconciled server test results separately from formal homologation', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-full-solution-trx-regression-2026-10-26-1852');
    const mongoEvent = statusData.events.find(item => item.id === 'server-mongo-integration-and-ci-isolated-python-2026-10-26-1857');
    const mariaEvent = statusData.events.find(item => item.id === 'server-mariadb-integration-reconciliation-2026-10-26-1903');

    assert.ok(event);
    assert.ok(mongoEvent);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.match(event.evidence, /17 TRX analisados: 5\.397 Passed, 96 NotExecuted, 0 Failed/);
    assert.match(mongoEvent.evidence, /Category=RequiresMongo.*37\/37/);
    assert.match(mariaEvent.evidence, /IntroSkipper\.Integration\.Tests.*17\/17/s);
});

test('Android track stays in progress while formal acceptance is pending', () => {
    const android = statusData.tracks.find(track => track.id === 'android');

    assert.equal(android.status, 'in_progress');
    assert.equal(android.homologation.completed, 10);
    assert.equal(android.homologation.total, 21);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
});

test('records MariaDB parent-row lock evidence without claiming production homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-parent-row-lock-mariadb-2026-10-26-0649');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /7\/7/);
    assert.match(event.release, /Nenhuma release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records localized image refresh coverage without changing the formal gate', () => {
    const event = statusData.events.find(item => item.id === 'server-image-refresh-missing-language-2026-10-26-2135');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /47\/47/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('records the missing CI serialization contract without granting hosted CI credit', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-runner-serialization-contract-2026-10-26-2138');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /14\/14/);
    assert.match(event.release, /CI hospedada.*pendente/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('records the privileged Linux restore CI contract without claiming Linux execution', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-ci-contract-2026-10-26-2140');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /15\/15/);
    assert.match(event.evidence, /WSL.*Docker.*indisponíveis/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('records a real isolated MariaDB start-stop-second-start smoke without production credit', () => {
    const eventId = 'server-mariadb-start-stop-restart-smoke-2026-10-26-2145';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /start → stop → start → stop passou 1\/1/);
    assert.match(event.evidence, /porta efêmera e datadir exclusivo em Temp/);
    assert.match(event.evidence, /MariaDbStartupMutexTests passou 4\/4/);
    assert.match(event.evidence, /porta 3306 e produção não foram acessados/);
    assert.match(event.release, /Homologação formal permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records confirmed people orphan cleanup without crediting formal homologation', () => {
    const eventId = 'server-people-delete-update-concurrency-audit-2026-10-27-0949';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /falhou antes do patch e passou depois \(1\/1\) contra MariaDB 11\.4\.4 descartável/);
    assert.match(event.evidence, /DeleteItem_ConcurrentWithUpdatePeople_DoesNotLeaveOrphanPeople passou 1\/1/);
    assert.match(event.evidence, /UpdatePeople_WaitsForSameItemPersistenceLockBeforeOpeningDatabaseContext passou 1\/1/);
    assert.match(event.evidence, /1\.738 aprovados, 95 ignorados, 0 falhas/);
    assert.match(event.release, /interleaving com PeopleRepository\.UpdatePeople foi reproduzida e serializada pelos locks compartilhados/);
    assert.match(event.release, /sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records permanent MariaDB lifecycle coverage and its hosted CI gate without formal credit', () => {
    const eventId = 'server-mariadb-lifecycle-regression-ci-2026-10-26-2150';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /Teste permanente passou 1\/1/);
    assert.match(event.evidence, /Jellyfin\.Server\.Tests passou 87\/87/);
    assert.match(event.evidence, /Sete contratos CI passaram 16\/16/);
    assert.match(event.evidence, /MariaDB operacional em 3306 permaneceu ativo e não foi consultado/);
    assert.match(event.release, /CI hospedada ainda não executou/);
    assert.match(event.release, /Homologação formal permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the repeated image deadlock without attributing it to production', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-identical-log-rerun-2026-10-26-0826');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /49\/49/);
    assert.match(event.release, /runtime instalado ou produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records real MariaDB revalidation without production or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-mariadb-revalidation-2026-10-26-1243');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ServerPersistenceMariaDbTests passou 19\/19/);
    assert.match(event.evidence, /ItemPersistenceServiceTests\|ItemPersistenceDatabaseLockTests passou 49\/49/);
    assert.match(event.release, /sem identificar o processo\/assembly do log/);
    assert.match(event.release, /Nenhuma alteração de runtime ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records concurrent image persistence revalidation without production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-concurrent-image-persistence-rerun-2026-10-26-1557');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ServerPersistenceMariaDbTests passou 24 aprovados, 1 ignorado e 0 falhas\/25/);
    assert.match(event.evidence, /casos de concorrência/);
    assert.match(event.release, /não identifica o runtime do log/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records SHA-256 validation for server update packages without signature or production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-update-package-sha256-2026-10-26-1304');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ServerUpdateTaskTests passou 20\/20/);
    assert.match(event.evidence, /UpdateInfoControllerTests passou 8\/8/);
    assert.match(event.evidence, /mesmo handle/);
    assert.match(event.release, /Digest obtido por HTTPS não autentica o publicador/);
    assert.match(event.release, /sem release ou push/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records same-handle verification for plugin installation without release or production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-plugin-package-same-handle-2026-10-26-1308');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /31\/31/);
    assert.match(event.evidence, /same handle|mesmo handle/i);
    assert.match(event.evidence, /MD5 mantido é compatibilidade/);
    assert.match(event.release, /sem commit\/release\/push/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('keeps Nebula physical path aliases partial after lexical C# and Python identity converge', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-queue-path-identity-audit-2026-10-26-0829');
    const feature = statusData.features.find(item => item.id === 'T2.2');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(feature.status, 'in_progress');
    assert.equal(statusData.coverage.completed, 357);
    assert.equal(statusData.coverage.pending, 74);
    assert.equal(statusData.coverage.inProgress, 7);
    assert.equal(statusData.coverage.total, 438);
});

test('records the Nebula unique queue index fail-closed fix without formal or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-queue-unique-index-fail-closed-2026-10-26-0834');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /721, 2 skips POSIX/);
    assert.match(event.release, /Nenhuma release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records the native C# queue index fail-closed fix without production or release claims', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-csharp-queue-index-fail-closed-2026-10-26-0910');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /NebulaQueueConcurrencyMongoTests: 26\/26/);
    assert.match(event.release, /Duplicatas preexistentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records cross-runtime Windows queue identity and lowercase legacy compatibility without release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-queue-identity-case-compat-2026-10-26-0920');
    const feature = statusData.features.find(item => item.id === 'T2.2');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /27\/27.*722, 2 skips POSIX/);
    assert.match(event.release, /symlink\/junction/);
    assert.equal(feature.status, 'in_progress');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records resumable legacy queue identity backfill without release or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-legacy-queue-identity-backfill-2026-10-26-0938');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /29\/29.*723 aprovados, 2 skips POSIX/);
    assert.match(event.release, /concorrência cruzada C#\/Python permanecem pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records concurrent C# and Python legacy queue initialization without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-cross-runtime-queue-backfill-2026-10-26-0951');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /Teste cruzado.*1\/1.*MongoDB 8\.3\.7 descartável/);
    assert.match(event.release, /Sem acesso ao Mongo operacional, CI remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records full local server regression without crediting filtered or skipped environments', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-full-regression-local-2026-10-26-1003');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.370 aprovados, 94 ignorados, 0 falhas\/5\.464 resultados/);
    assert.match(event.evidence, /723 aprovados, 2 skips POSIX/);
    assert.match(event.evidence, /portal: 272\/272/);
    assert.match(event.release, /Sem Mongo\/MariaDB operacionais, Linux nativo/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records installed image-persistence binary mismatch without attributing the deadlock log', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-installed-artifact-lock-gap-2026-10-26-0842');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /não é possível atribuir a falha/);
    assert.match(event.release, /Não instalar nem publicar/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records v12.1.35-based lock regression without claiming MariaDB or release validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-v35-worktree-regression-2026-10-26-0847');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /43\/43/);
    assert.match(event.release, /Não publicar\/instalar/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records repeated isolated MariaDB image-lock validation without promoting homologation or release', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-v35-mariadb-revalidation-2026-10-26-0857');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /ServerPersistenceMariaDbTests.*15\/15/);
    assert.match(event.evidence, /10\/10/);
    assert.match(event.evidence, /ItemPersistenceAdvisoryLock_BlocksAnotherDatabaseSession/);
    assert.match(event.release, /PID\/assembly/);
    assert.match(event.release, /sem commit/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records full implementations regression and isolated database opt-ins without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-implementations-mariadb-mongo-regression-2026-10-26-0702');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /1\.516 aprovados, 65 ignorados, 0 falhas\/1\.581/);
    assert.match(event.evidence, /25\/25/);
    assert.match(event.evidence, /257\/257/);
    assert.match(event.release, /Nenhuma release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('dashboard announces refresh state and filtered feature count accessibly', () => {
    assert.match(homologationHtml, /id="dashboard-state"[^>]*role="status"/);
    assert.match(homologationHtml, /id="live-updated"[^>]*role="status"/);
    assert.match(homologationHtml, /id="feature-count"[^>]*role="status"/);
});

test('records portal status and accessibility correction without claiming remote publication', () => {
    const event = statusData.events.find(item => item.id === 'portal-homologation-status-integrity-accessibility-2026-10-26-0404');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /248\/248/);
    assert.match(event.release, /Nenhuma publicação remota ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records GetAvatar download URL allowlist without claiming real network validation', () => {
    const event = statusData.events.find(item => item.id === 'server-getavatar-pack-url-allowlist-2026-10-26-0450');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /OnlinePackServiceTests: 9\/9/);
    assert.match(event.evidence, /GitHub real não foi chamado/);
    assert.match(event.release, /redirects do transporte/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records nested-mount restore guard without claiming Linux execution', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-programdata-bind-source-alias-2026-10-26-0410');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /tmpfs aninhado, bind-mounta ProgramData/);
    assert.match(event.evidence, /Reexecução Windows[/].NET 10 Release/);
    assert.match(event.evidence, /FullSystemBackup: 133 aprovados, 14 ignorados, 0 falhas\/147/);
    assert.match(event.evidence, /Execução privilegiada Linux e estabilidade concorrente do namespace ainda não verificadas/);
    assert.match(event.release, /executar o teste em Linux com permissão de mount/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records mandatory bootstrap checksum validation without claiming package authenticity', () => {
    const event = statusData.events.find(item => item.id === 'server-bootstrap-plugin-checksum-required-2026-10-26-0418');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /BootstrapPluginCatalogTests` Release passou 9\/9/);
    assert.match(event.release, /MD5 não fornece assinatura\/autenticidade forte/);
    assert.match(event.release, /catálogo está sem IDs de instalação/);
    assert.match(event.release, /sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records batched Nebula FTP listing without claiming database or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-pathio-batched-listing-2026-10-25-2203');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /710 aprovados, 2 ignorados\/712/);
    assert.match(event.evidence, /uma consulta por nível/);
    assert.match(event.release, /Mongo real, Linux, runtime instalado, CI hospedada e produção seguem sem validação/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records duplicate image deadlock log and filtered full-solution regression without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-duplicate-full-regression-2026-10-25-2248');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.209 aprovados, 83 ignorados, 0 falhas\/5\.292/);
    assert.match(event.evidence, /FullyQualifiedName!~Integration/);
    assert.match(event.release, /não confirma que o processo do log executa esta fonte/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the repeated deadlock attachment and current focused regression without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-duplicate-focal-rerun-2026-10-26-0005');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /49\/49/);
    assert.match(event.release, /não valida transações concorrentes em MariaDB/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records byte-identical MariaDB deadlock attachment recheck and focused rerun without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-duplicate-attachment-rerun-2026-10-26-0039');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /byte-idêntico ao incidente anterior/);
    assert.match(event.evidence, /ItemPersistenceServiceTests.*49\/49/);
    assert.match(event.evidence, /reenviado em 26\/10/);
    assert.match(event.evidence, /portal passou 233\/233/);
    assert.match(event.evidence, /helper não existe no HEAD 64a33dcf/);
    assert.match(event.release, /precisam integrar uma release após os gates pendentes/);
    assert.match(event.release, /não comprova a versão executada em produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records localized TMDb image flow against a local fixture without claiming database persistence', () => {
    const event = statusData.events.find(item => item.id === 'server-tmdb-portuguese-image-local-fixture-2026-10-26-0044');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /fixture HTTP local/);
    assert.match(event.evidence, /pt,null,en/);
    assert.match(event.evidence, /97\/97/);
    assert.match(event.release, /SaveImage foi mockado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the startup recovery subprocess rerun without claiming full server startup validation', () => {
    const event = statusData.events.find(item => item.id === 'server-startup-recovery-probe-release-rerun-2026-10-26-0008');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /exit 1 sem marcador/);
    assert.match(event.evidence, /retorno false terminou exit 0 sem marcador/);
    assert.match(event.evidence, /ProgramStartupRecoveryTests Release: 8\/8/);
    assert.match(event.release, /Não inicia StartApp/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records bounded detached Trickplay work with scan backpressure and no production claim', () => {
    const event = statusData.events.find(item => item.id === 'server-trickplay-pending-gauge-2026-10-26-0010');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /mulletaflix\.trickplay\.refresh\.pending/);
    assert.match(event.evidence, /no máximo oito refreshes detached/);
    assert.match(event.evidence, /TrickplayProviderTests Release: 3\/3/);
    assert.match(event.release, /não há medição de carga em biblioteca representativa/);
    assert.match(event.release, /formal continua 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the full filtered Release regression without counting skipped integrations as approved', () => {
    const event = statusData.events.find(item => item.id === 'server-full-suite-release-regression-2026-10-26-0011');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /15 projetos, 5\.313 aprovados, 80 ignorados, 0 falhas\/5\.393/);
    assert.match(event.evidence, /FullyQualifiedName!~Integration/);
    assert.match(event.release, /não equivale a homologação formal/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records isolated NebulaPython regression and Windows checkpoint durability without claiming Linux validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-checkpoint-windows-2026-10-26-0012');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /712 aprovados, 2 ignorados, 0 falhas/);
    assert.match(event.evidence, /MOVEFILE_REPLACE_EXISTING \| MOVEFILE_WRITE_THROUGH/);
    assert.match(event.release, /Linux.*CI.*produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('keeps Nebula POSIX validation open when WSL is absent and hosted Linux job has no steps', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-linux-execution-gate-recheck-2026-10-26-0031');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /WSL.*não está instalado/i);
    assert.match(event.evidence, /ubuntu-latest.*steps.*lista vazia/);
    assert.match(event.evidence, /não valida este checkout/);
    assert.match(event.release, /dois casos POSIX ignorados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records current Windows restore regression without crediting skipped Linux and macOS cases', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-fullsystembackup-windows-recheck-2026-10-26-0034');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /115 aprovados, 11 ignorados, 0 falhas \(126\)/);
    assert.match(event.evidence, /ACL POSIX.*openat\/symlink Linux/);
    assert.match(event.release, /11 ignorados não são aprovados/);
    assert.match(event.release, /Restore P0.*permanecem abertos/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records safe detachment of potentially shared image files without claiming deadlock resolution', () => {
    const event = statusData.events.find(item => item.id === 'server-shared-image-reference-preservation-2026-10-25-2054');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /BaseItem\.DeleteImageAsync e ItemImageProvider\.PruneImages/);
    assert.match(event.evidence, /SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /Esta alteração não corrige o deadlock/);
    assert.match(event.release, /sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('reports that the latest hosted server CI failed before any runner executed', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-no-runner-recheck-2026-10-25-0955');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /13 jobs concluídos como failure.*steps=null/);
    assert.match(event.evidence, /Reconsulta read-only em 26\/10\/2026.*steps=null/);
    assert.match(event.evidence, /working tree local segue 8 commits atrás/);
    assert.match(event.release, /Não foi disparado workflow.*push ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the confirmed billing lock that prevents hosted server CI from starting', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-billing-lock-confirmed-2026-10-27-0711');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /account is locked due to a billing issue/);
    assert.match(event.evidence, /13 jobs.*sem runner.*sem steps/);
    assert.match(event.release, /CI hospedada não executou testes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the fresh full .NET regression without merging raw and distinct audit totals', () => {
    const event = statusData.events.find(item => item.id === 'server-full-dotnet-unit-regression-2026-10-27-0722');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /17 TRX.*5\.450 aprovados, 98 NotExecuted, 0 falhas\/5\.548/);
    assert.match(event.evidence, /38 MongoDB opt-in.*32 MariaDB opt-in.*25 plataforma\/SO.*2 benchmarks.*1 \/dev\/shm/);
    assert.match(event.evidence, /não altera o agregado distinto publicado/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records remote release drift from the uncommitted image persistence lock fix', () => {
    const eventId = 'server-remote-base-image-lock-divergence-2026-10-27-0732';
    const event = statusData.events.find(item => item.id === eventId);

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /d51f4f38.*v12\.1\.35/);
    assert.match(event.evidence, /oito commits atrás/);
    assert.match(event.evidence, /commit remoto.*não contém ItemPersistenceDatabaseLock\.cs/);
    assert.match(event.evidence, /SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /DLL instalada 12\.1\.35, SHA-256 0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D/);
    assert.match(event.evidence, /ItemPersistenceServiceTests\|ItemPersistenceDatabaseLockTests passou 20\/20/);
    assert.match(event.evidence, /três integrações MariaDB foram ignoradas/);
    assert.match(event.evidence, /ItemPersistenceServiceTests\|ItemPersistenceDatabaseLockTests passou 20\/20/);
    assert.match(event.evidence, /três integrações MariaDB foram ignoradas/);
    assert.match(event.release, /nada foi mesclado, commitado ou publicado/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the repeated image deadlock log and fresh focused rerun without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-repeated-attachment-rerun-2026-10-25-0835');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /Reexecução nesta sessão.*40 aprovados, 0 ignorados\/falhas/);
    assert.match(event.release, /não prova runtime instalado nem produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the IntroSkipper participant rollback regression without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-introskipper-backup-transaction-rollback-2026-10-25-0750');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /passed 5\/5|passou 5\/5/);
    assert.match(event.evidence, /19, ignorou 35 e falhou 0\/54/);
    assert.match(event.evidence, /Contrato local confirmou que a classe está selecionada no job Linux MariaDB: 2\/2/);
    assert.match(event.evidence, /13\/13 jobs falharam antes de obter runner \(runner_id=0 entre 3–4 segundos\)/);
    assert.match(event.release, /permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Linux restore CI coverage without claiming an observable run', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-coverage-audit-2026-10-25-0519');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /server-build-test.*ubuntu-latest/s);
    assert.match(event.evidence, /13 jobs.*runner_id=0/s);
    assert.match(event.evidence, /404 `BlobNotFound`/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records native restore ACL API limits without claiming the P0 is fixed', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-native-api-research-2026-10-24-1315');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /GetSecurityInfo exige READ_CONTROL/);
    assert.match(event.evidence, /acl_get_fd depende de libacl/);
    assert.match(event.evidence, /não prova universalmente localidade/);
    assert.match(event.evidence, /continua pathname-based/);
    assert.match(event.release, /integrar apply, rollback, replay e cleanup/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('publishes current full server regression and repeated image deadlock limits without homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-main-full-regression-repeat-image-deadlock-log-2026-10-25-0456');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /12 DELETEs.*11 respostas MariaDB deadlock 1213/s);
    assert.match(event.evidence, /5\.287 aprovados, 256 NotExecuted, 0 falhas\/5\.543/);
    assert.match(event.evidence, /40\/40, 0 ignorados\/falhas/);
    assert.match(event.release, /Causa operacional não confirmada/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('publishes the current locked NebulaPython regression without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-full-regression-main-2026-10-25-0459');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /python -m pip install --require-hashes -r requirements\.lock/);
    assert.match(event.evidence, /python -m pip check` \(No broken requirements found\)/);
    assert.match(event.evidence, /697 passed em 15,73 s/);
    assert.match(event.release, /Linux\/CI remota, runtime instalado, transferência real e produção seguem pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('reports clean-baseline restore test result without overstating homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-clean-baseline-audit-2026-10-24-0937');
    assert.ok(event);
    assert.match(event.evidence, /FullSystemBackup Release 40\/40 aprovados, 0 ignorados e 0 falhas/);
    assert.match(event.evidence, /P0 aberto/);
    assert.match(event.evidence, /homologação formal 2\/7 \(28,6%\)/);
    const server = statusData.tracks.find(track => track.id === 'server');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes the latest restore P0 evidence without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-p0-lifecycle-reaudit-2026-10-24-1120');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /FullSystemBackup recompilado em Release passou 91, ignorou 10/);
    assert.match(event.evidence, /pathname/);
    assert.match(event.evidence, /ACL\/owner/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records repeated image deadlock evidence without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-repeat-log-2026-10-24-2212');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /passou 31\/31/);
    assert.match(event.evidence, /passou 40\/40/);
    assert.match(event.evidence, /nenhum banco\/runtime operacional foi acessado/);
    assert.match(event.release, /Homologação formal permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Nebula pathio merge validation without claiming production readiness', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-pathio-merge-validation-2026-10-24-2219');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /passou 167, ignorou 1 e falhou 0/);
    assert.match(event.evidence, /Ruff indisponível/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records packaged Nebula pathio parity without formal or production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-packaged-pathio-parity-2026-10-24-2224');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /pathio passaram 42\/42/);
    assert.match(event.evidence, /passou 696\/696/);
    assert.match(event.evidence, /WSL e Docker não estão disponíveis/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records recursive ObjectId visibility fix without production or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-objectid-directory-visibility-2026-10-24-2230');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Teste RED reproduziu `\/Series` invisível/);
    assert.match(event.evidence, /passou 697\/697/);
    assert.match(event.evidence, /passou 167, ignorou 1 e falhou 0/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('publishes installer ACL fixture evidence without treating restore ACL as verified', () => {
    const event = statusData.events.find(item => item.id === 'server-installer-acl-fixture-validation-2026-10-24-1125');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /sentinel externo permaneceu intacto/);
    assert.match(event.evidence, /não as ACLs\/owners das raízes reais de restore/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes focused restore regression without overstating security coverage', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-focused-regression-2026-10-24-1250');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /91 aprovados, 10 ignorados, 0 falhas \(101\)/);
    assert.match(event.evidence, /nenhum Linux nativo foi executado/);
    assert.match(event.evidence, /Restore P0 permanece aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes full solution regression without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-full-solution-regression-2026-10-24-1310');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /5\.272 aprovados, 267 ignorados, 0 falhas, 5\.539 resultados/);
    assert.match(event.evidence, /exit code 0/);
    assert.match(event.evidence, /restore P0 permanece aberto/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes the Windows container-inherit-only ACL regression without claiming restore safety', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-inherit-only-regression-2026-10-24-1356');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /94 aprovados, 10 ignorados, 0 falhas\/104/);
    assert.match(event.evidence, /INHERIT_ONLY/);
    assert.match(event.evidence, /FileSystemRestoreTransaction/);
    assert.match(event.evidence, /[Nn]ão há PID\/assembly/);
    assert.match(event.release, /não .*corrige/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes the image deadlock follow-up without claiming MariaDB or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-log-followup-2026-10-24-1805');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /passou 1\/1/);
    assert.match(event.evidence, /foi ignorado porque MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING está ausente/);
    assert.match(event.evidence, /não associa o log a assembly\/runtime/);
    assert.match(event.release, /Não aumentar timeout/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records CI runner allocation failure without treating skipped jobs as server test results', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-runner-allocation-audit-2026-10-24-1808');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /13 jobs/);
    assert.match(event.evidence, /runner_id=0/);
    assert.match(event.evidence, /steps=\[\]/);
    assert.match(event.evidence, /não revela causa raiz específica/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records current GitHub CI run failure and unavailable logs without claiming remote test execution', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-main-run-audit-2026-10-25-0507');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /13 jobs como failure/);
    assert.match(event.evidence, /steps=\[\]/);
    assert.match(event.evidence, /404 BlobNotFound/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('publishes image deadlock recheck and keeps local evidence separate from formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-status-recheck-2026-10-24-2119');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /40\/40/);
    assert.match(event.evidence, /runner_id=0/);
    assert.match(event.release, /formal permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records release runner failure and skipped packages without asserting no release exists elsewhere', () => {
    const event = statusData.events.find(item => item.id === 'server-release-runner-audit-2026-10-24-1809');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Validate source tag before packaging/);
    assert.match(event.evidence, /runner_id=0/);
    assert.match(event.evidence, /Build Linux x64 package e Build and publish release foram skipped/);
    assert.match(event.evidence, /não prova que a release v12\.1\.35 inexiste por outro caminho/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('reconciles the published v12.1.35 assets separately from the failed later workflow', () => {
    const event = statusData.events.find(item => item.id === 'server-release-12135-publication-reconciliation-2026-10-24-1810');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /v12\.1\.35 como publicada/);
    assert.match(event.evidence, /mulletaflix_12\.1\.35_linux-x64\.tar\.gz/);
    assert.match(event.evidence, /workflow Release 39608677913 começou em 13:30:56Z, depois da publicação/);
    assert.match(event.release, /não criar duplicata/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records release asset hashes without attributing upload to the failed workflow', () => {
    const event = statusData.events.find(item => item.id === 'server-release-12135-asset-provenance-audit-2026-10-24-1812');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /7cb01e2e35e3e17e786525d5d6fe649d7a6bf6eca7eec84ad564954837d5be8e/);
    assert.match(event.evidence, /2428fba0e9cc2417d40ddf0952da0db5e66e3637b7721efeb784a25a66001f1f/);
    assert.match(event.evidence, /6be65a66a145df23fd07681a71a5b26b0b091a019fb44717f73e29ec8f45bf6e/);
    assert.match(event.evidence, /não atribuir upload ao workflow falho/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes full implementations regression without granting restore homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-implementations-regression-2026-10-24-1130');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /1\.609 aprovados, 76 ignorados, 0 falhas/);
    assert.match(event.evidence, /não valida ACL efetiva, lifecycle transacional do restore, Linux nativo nem produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes read-only restore capacity check without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-readonly-capacity-check-2026-10-24-1139');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /DataPath, CachePath e ProgramDataPath ausentes não são criados/);
    assert.match(event.evidence, /93 aprovados, 10 ignorados.*0 falhas/);
    assert.match(event.release, /não implementa ACL/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
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

test('renders every roadmap item and matching coverage counters', async () => {
    const dashboard = createDashboard();
    await settleDashboard();

    assert.equal(dashboard.getElement('coverage-total').textContent, statusData.coverage.total);
    assert.equal(dashboard.getElement('coverage-completed').textContent, statusData.coverage.completed);
    assert.equal(dashboard.getElement('coverage-pending').textContent, statusData.coverage.pending);
    assert.equal(dashboard.getElement('coverage-in-progress').textContent, statusData.coverage.inProgress);
    assert.deepEqual(dashboard.percentElements.map(element => element.textContent), ['82%', '82%']);
    assert.match(dashboard.getElement('feature-count').textContent, new RegExp(`^${statusData.features.length} tarefas`));
    assert.equal((dashboard.getElement('feature-table').innerHTML.match(/<tr>/g) || []).length, 25);
    assert.equal(dashboard.getElement('feature-page-number').textContent, `Página 1 de ${Math.ceil(statusData.features.length / 25)}`);
});

test('coverage counters match the current roadmap feature statuses', () => {
    const count = status => statusData.features.filter(feature => feature.status === status).length;

    assert.equal(statusData.coverage.total, statusData.features.length);
    assert.equal(statusData.coverage.completed, count('completed'));
    assert.equal(statusData.coverage.pending, count('pending'));
    assert.equal(statusData.coverage.inProgress, count('in_progress'));
});

test('labels roadmap checklist percentage separately from formal homologation', () => {
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.match(homologationHtml, /Checklist do roadmap — não é homologação formal/);
    assert.match(homologationHtml, /não representa homologação formal nem aceite de produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('publishes the latest image deadlock evidence without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-recheck-2026-10-23-2352');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /1213/);
    assert.match(event.evidence, /29\/29/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});
test('publishes isolated MariaDB concurrency results without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-mariadb-isolated-2026-10-24-0005');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /29\/29/);
    assert.match(event.evidence, /2\/2/);
    assert.match(event.evidence, /3306 não foi consultado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});
test('publishes cross-session image lock evidence without granting formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-db-session-lock-2026-10-24-0105');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /GET_LOCK/);
    assert.match(event.evidence, /27\/27/);
    assert.match(event.evidence, /1\/1/);
    assert.match(event.evidence, /5\/5/);
    assert.match(event.evidence, /10\/10/);
    assert.match(event.evidence, /30 s compartilhados/);
    assert.match(event.evidence, /Produção\/3306 não consultada/);
    assert.deepEqual(event.links, []);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
});
test('publishes current image persistence regression without granting formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-regression-2026-10-24-0724');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /1\.607/);
    assert.match(event.evidence, /38\/38/);
    assert.match(event.evidence, /oito testes MariaDB selecionados compilaram e foram ignorados/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
});
test('publishes image deadlock test rerun limits without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-local-test-rerun-2026-10-24-1405');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /40\/40/);
    assert.match(event.evidence, /isoladamente passou 1\/1/);
    assert.match(event.evidence, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING/);
    assert.match(event.evidence, /não identifica PID\/assembly/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('publishes repeated image deadlock log rerun without advancing audit or formal counts', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-identical-log-unit-rerun-2026-10-26-2014');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /50\/50/);
    assert.match(event.evidence, /não identifica PID, assembly ou resultado final/);
    assert.match(event.release, /não alteram o agregado 5\.465\/5\.493/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('keeps installed image-persistence runtime mismatch open without release or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-installed-runtime-readonly-mismatch-2026-10-26-2016');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /12\.1\.35/);
    assert.match(event.evidence, /0D9465B7EC1549CB70C063A2B7B199E8A6B645F6E454CDBFD008A8D0B3F9D30D/);
    assert.match(event.evidence, /não identifica o assembly que produziu o log histórico/);
    assert.match(event.release, /nenhuma instalação foi feita/);
    assert.match(event.release, /formal continua 2\/7 \(28,6%\)/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});
test('records the active MariaDB listener as untested and keeps the image deadlock gate open', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-retest-2026-10-24-1538');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /40 aprovados, 0 ignorados, 0 falhas/);
    assert.match(event.evidence, /mysqld do servidor MulletaFlix já escuta em 3306/);
    assert.match(event.evidence, /nenhuma conexão ou consulta foi feita/);
    assert.match(event.release, /somente em instância descartável/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('preserves the restore journal when cleanup of created directories fails', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-directory-rollback-journal-2026-10-24-1545');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Assert\.NotNull falhou/);
    assert.match(event.evidence, /teste focado passou 1\/1/);
    assert.match(event.evidence, /FullSystemBackup passou 100 aprovados, 10 ignorados/);
    assert.match(event.release, /P0 segue aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('publishes image deadlock runtime divergence without claiming the historical process identity', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-runtime-recheck-2026-10-24-1550');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /12 DELETEs concorrentes/);
    assert.match(event.evidence, /11 deadlocks 1213/);
    assert.match(event.evidence, /v12\.1\.35.*v12\.1\.32/);
    assert.match(event.evidence, /log não traz PID\/hash/);
    assert.match(event.release, /MariaDB descartável\/CI/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('publishes disposable MariaDB persistence evidence without production or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-mariadb-disposable-2026-10-24-1601');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /MariaDB 11\.4\.4.*127\.0\.0\.1:33099/);
    assert.match(event.evidence, /ServerPersistenceMariaDbTests passou 12\/12/);
    assert.match(event.evidence, /processo MariaDB foi encerrado e a porta 33099 ficou fechada/);
    assert.match(event.evidence, /diretório descartável temporário permaneceu/);
    assert.match(event.evidence, /não runtime instalado, produção nem CI remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});
test('publishes deterministic restore cleanup test evidence without closing the restore P0', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-test-deterministic-sync-2026-10-24-1410');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /10\/10 repetições/);
    assert.match(event.evidence, /94 aprovados, 10 ignorados.*0 falhas\/104/);
    assert.match(event.evidence, /pathname-based/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('publishes the full server implementations regression without overstating homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-implementations-full-regression-2026-10-24-1415');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /1\.613 aprovados, 76 ignorados, 0 falhas\/1\.689/);
    assert.match(event.evidence, /Nenhum banco foi acessado/);
    assert.match(event.evidence, /Restore P0 pathname-based segue aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});
test('records restore directory-create ACL coverage without claiming the P0 is fixed', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-directory-create-bit-audit-2026-10-24-1423');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /FILE_ADD_SUBDIRECTORY.*FILE_APPEND_DATA.*0x4/);
    assert.match(event.evidence, /27 aprovados, 7 ignorados/);
    assert.match(event.evidence, /FileSystemRestoreTransaction, que segue pathname-based/);
    assert.match(event.release, /Não criada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records repeated image persistence deadlocks without claiming runtime correction', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-log-recheck-2026-10-24-1427');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /MariaDB 1213/);
    assert.match(event.evidence, /40 testes ItemPersistenceServiceTests \+ ItemPersistenceDatabaseLockTests passaram/);
    assert.match(event.evidence, /não identifica PID\/assembly/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('publishes image table writer audit and remote gap without granting homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-remote-gap-audit-2026-10-24-1430');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /SaveImagesAsync, SaveItems\/UpdateOrInsertItems, DeleteItem e FixDates/);
    assert.match(event.evidence, /origin\/main em d51f4f38.*não contém AcquireAsync/);
    assert.match(event.evidence, /3\/3 cenários foram ignorados/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});
test('publishes restore TOCTOU audit and focused tests without closing the formal gate', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-toctou-independent-review-2026-10-23-2358');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /88\/97/);
    assert.match(event.evidence, /7\/7/);
    assert.match(event.evidence, /ACL efetiva/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});
test('publishes restore source divergence and conditional write-boundary risks without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-source-reconciliation-2026-10-24-0147');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /ZIP traversal comum/);
    assert.match(event.evidence, /symlink\/junction/);
    assert.match(event.evidence, /137 caminhos modificados/);
    assert.match(event.evidence, /ACL efetiva/);
    assert.match(event.evidence, /40\/40/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
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
    const restoreRaceEvent = statusData.events.find(item => item.id === 'server-restore-linux-namespace-race-review-2026-10-24-1611');
    const restoreRegressionEvent = statusData.events.find(item => item.id === 'server-restore-windows-regression-linux-runtime-2026-10-25-0743');

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
    assert.ok(restoreRaceEvent, 'portal should retain the historical restore race audit');
    assert.ok(restoreRegressionEvent, 'portal should retain the Windows restore regression audit');

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

    assert.ok(event, 'portal should include the current server validation/release event');
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /1\.437 aprovados, 60 ignorados, 0 falhas/);
    assert.match(event.evidence, /repetição final inconclusiva/i);
    assert.match(event.evidence, /produção/i);
    assert.match(event.evidence, /dpl_22RZqxXtpqbAwL9EUW2Ys5sw6HQ1 READY/);
    assert.match(event.evidence, /HTTP 200/);
    assert.equal(audit.passed, 5465);
    assert.equal(audit.total, 5560);
    assert.equal(audit.skipped, 95);
    assert.equal(audit.failed, 0);
    assert.equal(audit.percent, 98.3);
    assert.match(audit.label, /FullyQualifiedName!~Integration/);

    const releaseEvent = statusData.events.find(item => item.title === 'Servidor 12.1.36 publicada com artefatos Windows e Linux');
    assert.ok(releaseEvent, 'portal source should retain the server release event');
});

test('portal shows the current server regression and keeps formal homologation separate', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-full-dotnet-regression-2026-10-24-1156');
    const restoreEvent = statusData.events.find(item => item.id === 'server-build-inclusive-regression-restore-focused-2026-10-23-1917');

    assert.ok(event);
    assert.ok(restoreEvent);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.equal(server.testAudit.passed, 5465);
    assert.equal(server.testAudit.total, 5560);
    assert.equal(server.testAudit.skipped, 95);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.testAudit.percent, 98.3);
    assert.match(server.testAudit.label, /5\.642 aprovados, 98 ignorados, 0 falhas\/5\.740/);
    assert.match(server.testAudit.label, /Providers teve 923\/923, API 828 aprovados e 3 ignorados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.match(server.homologation.label, /sign-off formal pendente/i);
    assert.match(event.evidence, /5\.272 aprovados, 265 ignorados, 0 falhas\/5\.537 resultados/);
    assert.match(event.evidence, /ACL e operações pathname em apply\/rollback\/replay\/cleanup/);
    assert.match(restoreEvent.evidence, /FullSystemBackup focal passou 86, ignorou 6 casos Linux-only, 0 falhas\/92/);
    assert.match(restoreEvent.evidence, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.ok(Date.parse(restoreEvent.updatedAt) <= Date.parse(statusData.generatedAt));

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /gate determinístico \.NET aprovado/);
});

test('portal records the orphaned Nebula worker and avoids claiming a production repair', async () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-orphaned-worker-2026-10-18-0254');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.deepEqual(event.featureIds, ['T2.1']);
    assert.match(event.evidence, /12\.1\.21/);
    assert.match(event.evidence, /PID pai já não existe/);
    assert.match(event.evidence, /não expõe PID/);
    assert.match(event.evidence, /não foi encerrado/);
    assert.match(event.evidence, /passou 1\/1/);
    assert.equal(server.testAudit.passed, 5465);
    assert.equal(server.testAudit.total, 5560);
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

test('homologation events without a measured percentage do not render as zero percent', async () => {
    const installerEvent = statusData.events.find(event => event.id === 'server-restore-installer-selected-service-acl-2026-10-23-1953');
    const restoreAuditEvent = statusData.events.find(event => event.id === 'server-restore-transaction-audit-2026-10-23-2012');
    const dashboard = createDashboard({ ...statusData, events: [installerEvent, restoreAuditEvent] });
    await settleDashboard();

    assert.match(dashboard.getElement('event-list').innerHTML, /ACL do instalador Windows limitada à identidade selecionada/);
    assert.doesNotMatch(dashboard.getElement('event-list').innerHTML, /ACL do instalador Windows limitada à identidade selecionada<\/strong><p>[^<]*Em execução · 0%/);

    assert.equal(installerEvent.status, 'in_progress');
    assert.equal(installerEvent.progressPercent, null);
    assert.match(installerEvent.evidence, /sc\.exe config/);
    assert.match(installerEvent.evidence, /NSIS compilou/);
    assert.match(installerEvent.evidence, /Nenhum instalador\/upgrade\/serviço real/);

    assert.equal(restoreAuditEvent.status, 'in_progress');
    assert.equal(restoreAuditEvent.progressPercent, null);
    assert.match(restoreAuditEvent.evidence, /86 aprovados, 6 Linux-only ignorados, 0 falhas/);
    assert.match(restoreAuditEvent.evidence, /política não está implementada/);
    assert.match(restoreAuditEvent.evidence, /Homologação formal permanece 2\/7/);
    assert.ok(Date.parse(restoreAuditEvent.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.match(dashboard.getElement('event-list').innerHTML, /Auditoria de segurança e testes focais do restore/);
});

test('GetAvatar visibility fix is complete in source while production and HTTP gates stay open', () => {
    const fixed = statusData.features.find(item => /T6\.1\.24 — Visibilidade de `GetAvatar` corrigida/.test(item.name));
    const pipelineGate = statusData.features.find(item => item.id === 'T6.1.24');
    const parent = statusData.features.find(item => item.id === 'T6.1');

    assert.ok(fixed);
    assert.equal(fixed.status, 'completed');
    assert.equal(pipelineGate.status, 'pending');
    assert.equal(parent.status, 'in_progress');
    const rollback = statusData.features.find(item => /T6\.1\.25 — `GetAvatar\.SaveAvatarAsync`/.test(item.name));
    assert.ok(rollback);
    assert.equal(rollback.status, 'completed');
    const rollbackEvidence = statusData.events.find(item => item.id === 'server-getavatar-persistence-rollback-2026-10-18-0751');
    assert.ok(rollbackEvidence);
    assert.match(rollbackEvidence.evidence, /12\/12/);
    const ambiguousClear = statusData.features.find(item => /T6\.1\.26 — `GetAvatar\.SetUserAvatarAsync`/.test(item.name));
    assert.ok(ambiguousClear);
    assert.equal(ambiguousClear.status, 'completed');
    assert.equal(statusData.coverage.completed, 357);
    assert.equal(statusData.coverage.pending, 74);
    assert.equal(statusData.coverage.inProgress, 7);
    assert.equal(statusData.coverage.total, 438);
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
    assert.match(server.testAudit.label, /5\.465 aprovados, 95 ignorados, 0 falhas/);
    assert.match(server.testAudit.label, /Homologação formal 2\/7/);
    assert.equal(statusData.coverage.pending, 65);
    assert.equal(statusData.coverage.inProgress, 4);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.ok(Date.parse(nebulaSafety.updatedAt) <= Date.parse(statusData.generatedAt));
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
    assert.match(dashboard.getElement('event-list').innerHTML, /PR #1: checks bloqueados por cobrança/);
    assert.equal(statusData.coverage.completed, 328);
    assert.equal(statusData.coverage.pending, 65);
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
    assert.equal(server.testAudit.passed, 5465);
    assert.equal(server.testAudit.total, 5560);
    assert.equal(server.testAudit.skipped, 95);
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
