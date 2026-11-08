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

    const dashboard = createDashboard({ ...statusData, events: [restoreRegressionEvent] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Restore: regressão Windows e disponibilidade do runtime Linux/);
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
});

test('portal shows the current server regression and keeps formal homologation separate', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-full-dotnet-regression-2026-10-24-1156');
    const restoreEvent = statusData.events.find(item => item.id === 'server-build-inclusive-regression-restore-focused-2026-10-23-1917');

    assert.ok(event);
    assert.ok(restoreEvent);
    assert.equal(event.front, 'server');
    assert.equal(event.status, 'in_progress');
    assert.equal(restoreEvent.status, 'completed');
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
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

});

test('portal records the passing full Release rerun and Newtonsoft audit correction', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-full-regression-newtonsoft-audit-2026-10-25-0900');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.298 aprovados, 256 NotExecuted, 0 falhas\/5\.554 resultados em 17 assemblies/);
    assert.match(event.evidence, /exit code 0/);
    assert.match(event.release, /Homologação formal segue 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the broad non-integration .NET rerun without replacing the MariaDB audit', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-dotnet-no-integration-regression-2026-10-25-2107');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /15 projetos, 5\.291 aprovados, 80 ignorados, 0 falhas\/5\.371 resultados/);
    assert.match(event.evidence, /testes de integração foram excluídos pelo filtro/);
    assert.match(event.evidence, /maior cobertura registrada e não foi substituída/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the isolated full Nebula Python regression without production credit', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-nebula-python-full-suite-2026-10-25-0906');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /requirements\.lock.*requirements-test\.txt/);
    assert.match(event.evidence, /pytest -q` passou 701\/701/);
    assert.match(event.evidence, /nenhuma conexão aos processos locais mysqld:3306\/mongod:27017 foi feita/);
    assert.match(event.release, /Não valida integração com Mongo\/MariaDB reais, produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
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
    assert.match(server.testAudit.label, /5\.642 aprovados.*0 falhas\/5\.740/);
    assert.match(server.testAudit.label, /Server\.Implementations 1\.752 aprovados e 95 ignorados/);
    assert.match(server.testAudit.label, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/i);
    const nebulaSafety = statusData.events.find(item => item.id === 'server-nebula-upload-download-safety-2026-10-23-2132');
    assert.ok(nebulaSafety);
    assert.equal(nebulaSafety.status, 'in_progress');
    assert.match(nebulaSafety.evidence, /cancela e aguarda tarefas irmãs/);
    assert.match(nebulaSafety.evidence, /origem sem Range/);
    assert.equal(statusData.coverage.pending, 74);
    assert.equal(statusData.coverage.inProgress, 7);
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

test('portal records English artwork fallback for known non-preferred remote images', () => {
    const event = statusData.events.find(item => item.id === 'server-known-image-language-english-fallback-2026-10-23-2020');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /RefreshImages passou 55\/55/);
    assert.match(event.evidence, /passou 94\/94/);
    assert.match(event.evidence, /runtime instalado ou produção/);
    assert.match(event.release, /Nenhuma/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records macOS restore startup guard without claiming platform validation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-macos-startup-guard-2026-10-23-2028');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /87\/95 \(8 ignorados, 0 falhas\)/);
    assert.match(event.evidence, /runner macOS para iOS, não para a suíte do servidor/);
    assert.match(event.evidence, /ACL\/TOCTOU P0 continua aberto/);
    assert.match(event.release, /Nenhuma/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records MariaDB image persistence deadlock fix without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-persistence-deadlock-2026-10-23-2039');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /deadlock MariaDB 1213/);
    assert.match(event.evidence, /ItemPersistenceServiceTests passou 25\/25/);
    assert.match(event.evidence, /77\/77/);
    assert.match(event.evidence, /sem instalação\/produção/);
    assert.match(event.release, /Rascunho local/);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records image deadlock log review and its unverified writer boundaries', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-log-review-2026-10-23-2146');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /MySQL error 1213/);
    assert.match(event.evidence, /DeleteItem como outro escritor.*sem esse lock/);
    assert.match(event.evidence, /MariaDB descartável indisponível/);
    assert.match(event.evidence, /produção ou release nesta revisão/);
    assert.match(event.release, /Rascunho local/);
    assert.equal(statusData.tracks.find(track => track.id === 'server').homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records DeleteItem image lock coordination without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-delete-lock-coordination-2026-10-23-2201');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /29\/29/);
    assert.match(event.evidence, /ignorado sem MariaDB descartável/);
    assert.match(event.evidence, /não coordena processos separados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('records the earlier sandbox ACL regression as resolved without formal credit', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-full-regression-mariadb-acl-fixture-2026-10-25-0923');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /5\.503 aprovados, 49 NotExecuted e 2 falhas/);
    assert.match(event.evidence, /identidade de sandbox com escrita/);
    assert.match(event.release, /permanece 2\/7 \(28,6%\) sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.percent, 28.6);
});

test('records the green MariaDB-enabled full server regression without homologation credit', () => {
    const server = statusData.tracks.find(track => track.id === 'server');
    const event = statusData.events.find(item => item.id === 'server-full-regression-mariadb-acl-fixed-2026-10-25-0945');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /5\.505, ignorou 49 e falhou 0\/5\.554/);
    assert.match(event.evidence, /IntroSkipper\.Integration\.Tests completo 54\/54/);
    assert.match(event.release, /sem release, instalação/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal records safe journal cleanup while restore TOCTOU remains open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-journal-safe-link-cleanup-2026-10-23-2251');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /BackupRestoreDirectoryTests passou 23, ignorou 6 testes Linux-only/);
    assert.match(event.evidence, /continuam pathname-based/);
    assert.match(event.evidence, /ACL efetiva não validada/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('portal records Docker restore ACL evidence without claiming runtime validation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-docker-identity-audit-2026-10-23-2303');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /não estão disponíveis neste host/);
    assert.match(event.evidence, /Não alterado Dockerfile/);
    assert.match(event.evidence, /produção/);
    assert.match(event.evidence, /TOCTOU\/ACL P0 continuam abertos/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records Nebula directory refresh retries without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-refresh-retry-2026-10-23-2320');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Suíte .NET ampla ficou sem saída/);
    assert.match(event.evidence, /NebulaPython isolada: 694\/694/);
    assert.match(event.evidence, /Sem rclone\/disco N, Linux, produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records cross-cut server audit without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-crosscut-audit-2026-10-23-2335');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /5\.240 aprovados, 78 ignorados, 0 falhas\/5\.318/);
    assert.match(event.evidence, /Restore segue P0/);
    assert.match(event.evidence, /provider\/runtime\/DB não foram exercitados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records image deadlock runtime reconciliation without production DB access', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-runtime-reconciliation-2026-10-23-2342');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /12\.1\.35/);
    assert.match(event.evidence, /12\.1\.32/);
    assert.match(event.evidence, /29\/29/);
    assert.match(event.evidence, /helper rejeita 3306/);
    assert.match(event.evidence, /Banco ativo não consultado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records item persistence lock coverage without production or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-item-persistence-reattach-locks-2026-10-24-0203');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Unitários ItemPersistence Release passaram 33\/33/);
    assert.match(event.evidence, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não configurada/);
    assert.match(event.evidence, /eficácia em produção não confirmada/);
    assert.match(event.evidence, /information_schema\.PROCESSLIST no estado User lock, filtrado pelo CONNECTION_ID/);
    assert.match(event.evidence, /pool MySqlConnector da conexão é invalidado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the restore lifecycle audit without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-lifecycle-audit-2026-10-24-0453');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /zero chamadas de BackupRestoreDirectory/);
    assert.match(event.evidence, /WSL não está instalado/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records missing-archive error precedence without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-missing-archive-precedence-2026-10-24-0513');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /lease ocupado continua sendo verificado primeiro/);
    assert.match(event.evidence, /5\.260 aprovados, 261 ignorados, 0 falhas\/5\.521/);
    assert.match(event.evidence, /TOCTOU\/ACL P0 aberto/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records relational restore rollback coverage without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-relational-insert-rollback-2026-10-24-0702');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Server\.Implementations recompilada em Release passou 1\.598, ignorou 75, falhou 0\/1\.673/);
    assert.match(event.evidence, /BackupRestoreMariaDbTests com MariaDB 11\.4\.4 descartável: 4\/4 aprovados/);
    assert.match(event.evidence, /Não cobre cenários gerais de falha\/crash do filesystem/);
    assert.match(event.evidence, /3306\/produção não consultada/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records isolated Nebula Python regression without production or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-isolated-regression-2026-10-24-0712');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Python 3\.10 global parou na coleta \(0 testes\)/);
    assert.match(event.evidence, /requirements\.lock.*694\/694 em 13,07 s/);
    assert.match(event.evidence, /nenhum serviço MongoDB\/Supabase\/Telegram/);
    assert.match(event.evidence, /limpeza foi bloqueada pela política local/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records current restore P0 audit without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-lifecycle-p0-audit-2026-10-24-0728');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /apply, rollback, cleanup e remoção de diretórios.*pathname/i);
    assert.match(event.evidence, /não valida ACL\/identidade efetiva/i);
    assert.match(event.evidence, /91 aprovados, 9 ignorados/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records full .NET solution regression without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-solution-regression-2026-10-24-0738');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /dotnet test MulletaFlix\.sln.*exit code 0/);
    assert.match(event.evidence, /terminal truncou partes intermediárias/);
    assert.match(event.evidence, /ACL real/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records clean restore ACL baseline without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-contract-baseline-2026-10-24-0838');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /Base limpa d51f4f38: FullSystemBackup Release 40\/40/);
    assert.match(event.evidence, /gate precisa cobrir roots, ancestrais, ZIP, journal/);
    assert.match(event.evidence, /Nenhum código\/runtime\/banco\/produção alterado/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records repeated .NET regression without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-regression-rerun-2026-10-24-0851');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /5\.254 aprovados, 78 ignorados, 0 falhas\/5\.332/);
    assert.match(event.evidence, /checkout sujo/);
    assert.match(event.evidence, /Restore TOCTOU\/ACL segue P0 aberto/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal keeps restore handle integration and Linux ACL gates open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-lifecycle-reconciliation-2026-10-24-0857');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /BackupRestoreDirectory é chamado somente por BackupRestoreRecoveryJournal/);
    assert.match(event.evidence, /82 aprovados, 9 ignorados Linux\/macOS/);
    assert.match(event.evidence, /wsl --list --verbose.*não está instalada/);
    assert.match(event.evidence, /porta 3306.*não recebeu conexão/i);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records Linux restore ancestor handles and keeps formal gate open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-ancestor-handle-gap-2026-10-24-0901');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /OpenLinuxDirectoryPath agora mantém os descritores ancestrais/);
    assert.match(event.evidence, /O teste Linux novo foi ignorado no host Windows/);
    assert.match(event.evidence, /sem validar ACL\/owner/);
    assert.match(event.evidence, /P0 aberto/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records correction of the superseded Linux ancestor-handle finding without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-ancestor-handle-doc-correction-2026-10-24-1321');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /A afirmação de 09:01 era histórica/);
    assert.match(event.evidence, /mantém os handles ancestrais Linux/);
    assert.match(event.evidence, /BackupRestoreDirectoryTests Release: 23 aprovados, 7 ignorados por plataforma, 0 falhas\/30/);
    assert.match(event.evidence, /teste de retenção Linux ficou ignorado/);
    assert.match(event.evidence, /ACL\/owner.*pendentes/);
    assert.match(event.evidence, /P0 permanece aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records FullSystemBackup regression results without counting skipped platforms as homologated', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-fullsystembackup-regression-2026-10-24-1326');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /FullSystemBackup Release no Windows\/\.NET 10: 91 aprovados, 10 ignorados \(Linux\/macOS\), 0 falhas\/101/);
    assert.match(event.evidence, /Sem teste Linux nativo, ACL efetiva ou lifecycle integrado/);
    assert.match(event.evidence, /P0 continua aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records insecure TEMP ACL fixtures as a fail-closed test prerequisite', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-test-fixture-boundary-2026-10-24-1330');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /fixtures randômicas sob pasta dedicada no perfil/);
    assert.match(event.evidence, /Suíte focal Release recompilada: 23 aprovados, 7 ignorados Linux\/macOS, 0 falhas\/30/);
    assert.match(event.evidence, /Nenhuma alteração runtime/);
    assert.match(event.evidence, /P0 aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('portal records orphan image deletion lock coordination without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-orphan-lock-coordination-2026-10-24-0920');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /SaveLinkedChildren agora reporta as stripes dos itens órfãos/);
    assert.match(event.evidence, /1\.609 aprovados, 76 ignorados, 0 falhas/);
    assert.match(event.evidence, /espera fixa e cobertura apenas local/);
    assert.match(event.evidence, /PROCESSLIST em espera GET_LOCK/);
    assert.match(event.evidence, /não exercita processo OS separado/);
    assert.match(event.evidence, /foi ignorado sem conexão descartável configurada/);
    assert.match(event.evidence, /causa não foi correlacionada conclusivamente/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal distinguishes local image deadlock fix from installed and remote versions', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-runtime-reconciliation-2026-10-24-1147');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /O anexo recebido novamente reproduz a evidência de 23\/10/);
    assert.match(event.evidence, /o log não informa PID\/assembly/);
    assert.match(event.evidence, /DLL instalada 12\.1\.35 e checkout local 12\.1\.32/);
    assert.match(event.evidence, /Portal validado agora: 109\/109/);
    assert.match(event.evidence, /144 paths alterados\/não rastreados/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal publishes full .NET regression while keeping restore and formal gates open', () => {
    const event = statusData.events.find(item => item.id === 'server-full-dotnet-regression-2026-10-24-1156');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /17 projetos, 5\.272 aprovados, 265 ignorados, 0 falhas\/5\.537/);
    assert.match(event.evidence, /ACL e operações pathname em apply\/rollback\/replay\/cleanup/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal reports Windows Python 3.10 Nebula regression without awarding formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-regression-2026-10-24-1205');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.local, /Windows; Python 3\.10/);
    assert.match(event.evidence, /694 passed em 11\.23 s/);
    assert.match(event.evidence, /Sem MongoDB, Telegram, Supabase, runtime instalado ou produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the repeated historical image deadlock without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-log-recheck-2026-10-24-1212');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /23\/10\/2026 20:36/);
    assert.match(event.evidence, /MariaDB 1213/);
    assert.match(event.evidence, /31\/31/);
    assert.match(event.evidence, /não prova que o binário instalado inclui a correção/);
    assert.match(event.evidence, /MariaDB, runtime instalado e produção não foram acessados/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records restore ACL and handle-lifecycle gaps without advancing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-p0-independent-crosscheck-2026-10-24-1217');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /cinco raízes de destino/);
    assert.match(event.evidence, /não pela FileSystemRestoreTransaction/);
    assert.match(event.evidence, /Nenhuma ACL\/owner efetiva é validada/);
    assert.match(event.evidence, /não foram encontrados testes de ACL/);
    assert.match(event.evidence, /Auditoria estática apenas/);
    assert.match(event.release, /P0 permanece aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records image advisory-lock regression as compiled but not executed', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-regression-test-2026-10-24-1220');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /SaveImagesAsync_WaitsForDatabaseAdvisoryLockHeldByAnotherSession/);
    assert.match(event.evidence, /compilou em Release/);
    assert.match(event.evidence, /foi ignorado/);
    assert.match(event.evidence, /não valida execução real MariaDB/);
    assert.match(event.evidence, /não identifica PID\/assembly/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the Linux MariaDB CI gate for the image advisory-lock regression', () => {
    const event = statusData.events.find(item => item.id === 'server-image-advisory-lock-ci-2026-10-24-1226');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /MariaDB 11\.4 no runner Linux/);
    assert.match(event.evidence, /CI remoto ainda não foi executado/);
    assert.match(event.release, /Aguardando execução do workflow remoto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal publishes the latest restore re-audit without increasing formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-p0-reaudit-2026-10-24-1230');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /apply, rollback, replay e cleanup/);
    assert.match(event.evidence, /40\/40/);
    assert.match(event.release, /Não aplicar gate ACL isolado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal publishes FixDates image-lock reconciliation without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-fixdates-lock-reconciliation-2026-10-24-1240');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /a fonte deste checkout não tinha o lock/);
    assert.match(event.evidence, /integração focada compilou, porém 1 caso foi ignorado/);
    assert.match(event.evidence, /sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the latest image-deadlock revalidation without production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-local-revalidation-2026-10-24-1258');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /ItemPersistenceServiceTests.*31\/31/);
    assert.match(event.evidence, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING.*não está configurada/);
    assert.match(event.evidence, /não prova que o binário em execução contém essa proteção/);
    assert.match(event.release, /MariaDB descartável\/CI/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records cross-stripe MariaDB deadlock coverage as pending until disposable DB execution', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-cross-stripe-batch-2026-10-26-0620');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /SHA-256.*idêntico.*deadlocks 1213/);
    assert.match(event.evidence, /11 deadlocks 1213 e 12 avisos de retry no mesmo ItemId/);
    assert.match(event.evidence, /heartbeat Kestrel atrasado em 2,35 s/);
    assert.match(event.evidence, /MariaDB 11\.4\.13 descartável autenticada em 127\.0\.0\.1:33099.*cinco testes.*passaram 5\/5/);
    assert.match(event.evidence, /A instância foi encerrada e a porta fechada/);
    assert.match(serverCiWorkflow, /FullyQualifiedName~SaveItemsBatchAndSaveImagesAsync_ConcurrentItemsAcrossStripes_CompletesWithoutDeadlock/);
    assert.match(event.release, /não explicam a origem do log.*CI Linux deste checkout, runtime instalado, produção e homologação formal continuam pendentes/i);
});

test('portal records the handle ACL validator without claiming restore integration or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-handle-validator-2026-10-24-1342');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /BackupRestoreDirectoryTests 25 aprovados, 7 ignorados/);
    assert.match(event.evidence, /ainda não tem caller em FileSystemRestoreTransaction/);
    assert.match(event.release, /Não criada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the Release restore regression without claiming ACL integration or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-release-regression-2026-10-24-1348');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /93 aprovados, 10 ignorados, 0 falhas\/103/);
    assert.match(event.evidence, /`FileSystemRestoreTransaction` ainda muta por pathname/);
    assert.match(event.release, /Não criada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records conditional Linux restore namespace races without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-namespace-race-review-2026-10-24-1611');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /mkdirat seguido de openat/);
    assert.match(event.evidence, /statx seguido de renameat\/renameat2/);
    assert.match(event.evidence, /nenhuma exploração determinística foi executada/);
    assert.match(event.release, /executar a suíte no Linux/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records restore filesystem-anchor validation and remaining mount risk', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-filesystem-anchor-acl-2026-10-24-1632');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ignorava a âncora/);
    assert.match(event.evidence, /STATX_MNT_ID identifica a montagem, não sua origem/);
    assert.match(event.release, /Linux nativo/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records Linux restore namespace races without claiming the P0 is closed', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-race-audit-2026-10-24-1713');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /mkdirat→openat/);
    assert.match(event.evidence, /statx→renameat\[2\]/);
    assert.match(event.evidence, /Não foi feita exploração nem execução Linux/);
    assert.match(event.release, /Restore ACL\/TOCTOU P0 permanece aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records image persistence test rerun without claiming production resolution', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-focal-rerun-2026-10-24-1718');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /40 aprovados, 0 ignorados, 0 falhas/);
    assert.match(event.evidence, /não informa PID\/assembly/);
    assert.match(event.release, /Causa-raiz\/runtime emissor permanecem não identificados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records current restore regression while keeping Linux TOCTOU gate open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-current-source-regression-2026-10-24-1721');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /102 aprovados, 10 ignorados por plataforma, 0 falhas\/112/);
    assert.match(event.evidence, /não é correto afirmar que todo o lifecycle segue pathname-based/);
    assert.match(event.evidence, /mkdirat\/openat, statx\/renameat\[2\] e statx\/unlinkat/);
    assert.match(event.release, /Restore ACL\/TOCTOU P0 permanece aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the full server regression rerun without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-rerun-2026-10-24-1727');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /5\.283 aprovados, 267 ignorados, 0 falhas\/5\.550/);
    assert.match(event.evidence, /Nebula Python via uv.*694\/694/);
    assert.match(event.release, /8 commits atrás/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal distinguishes trusted-principal restore races from untrusted ACL bypass', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-trust-boundary-audit-2026-10-24-1731');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /AcquireOperationLease.*serializa processos/);
    assert.match(event.evidence, /não foi provado escape para fora do pai/);
    assert.match(event.evidence, /não como bypass de usuário não confiável/);
    assert.match(event.evidence, /server-build-test.*exclui testes.*FullyQualifiedName!~Integration/);
    assert.match(event.evidence, /jobs Linux separados executam filtros focados/);
    assert.match(event.release, /Gate formal Linux continua aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the latest remote Linux CI failure without inferring its cause', () => {
    const event = statusData.events.find(item => item.id === 'server-linux-ci-unobservable-failure-2026-10-24-1735');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /39608614450.*completed\/failure/);
    assert.match(event.evidence, /13 jobs/);
    assert.match(event.evidence, /steps: \[\].*404 BlobNotFound/);
    assert.match(event.evidence, /etapa e causa desconhecidas/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the image deadlock rerun without claiming runtime resolution', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-focal-rerun-2026-10-24-1813');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /40 aprovados, 0 ignorados, 0 falhas/);
    assert.match(event.evidence, /não identifica PID\/assembly do log/);
    assert.match(event.release, /runtime continuam gates/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records restore Windows regression without counting Linux skips as validation', async () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-windows-regression-rerun-2026-10-24-1801');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /102 aprovados, 10 ignorados, 0 falhas\/112/);
    assert.match(event.evidence, /Subsistema do Windows para Linux não está instalado; Docker não está disponível/);
    assert.match(event.evidence, /não contam como validação Linux/);
    assert.match(event.evidence, /Contrato do portal passou 145\/145/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));

    const dashboard = createDashboard({ ...statusData, events: [event] });
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /regressão Windows aprovada; validação Linux segue pendente/);
});

test('portal records restore file ACL protection without claiming formal or Linux validation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-existing-file-acl-2026-10-24-1823');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /103 aprovados, 10 ignorados, 0 falhas\/113/);
    assert.match(event.evidence, /teste ACL de arquivo passou 1\/1/);
    assert.match(event.release, /Linux nativo\/ACL POSIX/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal distinguishes the deadlock patch worktree from published main', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-release-source-reconciliation-2026-10-24-1835');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /origin\/main\/v12\.1\.35.*não contém ItemPersistenceDatabaseLock/);
    assert.match(event.evidence, /40 aprovados, 0 ignorados, 0 falhas/);
    assert.match(event.evidence, /histórica registrada, não reexecutada/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal reports candidate implementation regression without MariaDB or release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-candidate-implementations-regression-2026-10-24-1842');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /solução Release\/\.NET 10 passou 5\.139 testes, ignorou 242 e falhou 0\/5\.381 em 17 assemblies/);
    assert.match(event.evidence, /excluiu DramaFindsLiveIntegrationTests/);
    assert.match(event.evidence, /NebulaPython.*660\/660/);
    assert.match(event.evidence, /writers diretos de BaseItemImageInfos.*SaveImagesCoreAsync/);
    assert.match(event.evidence, /MariaDB não executad[oa] nesta sessão/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal reports synchronous ambiguous advisory-lock cleanup fix without release credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-sync-lock-ambiguous-release-2026-10-24-1911');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /resposta GET_LOCK ambígua/);
    assert.match(event.evidence, /5\.142 aprovados, 242 ignorados, 0 falhas\/5\.384/);
    assert.match(event.evidence, /ItemPersistenceDatabaseLockTests 12\/12/);
    assert.match(event.release, /Nenhuma/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal publishes the Windows restore rerun without implying Linux validation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-windows-regression-rerun-2026-10-24-2131');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /passou 105, ignorou 10 e falhou 0 \(115\) em Windows\/\.NET 10/);
    assert.match(event.evidence, /WSL não está instalado/);
    assert.match(event.evidence, /não valida ACL POSIX\/syscalls Linux nem fecha o gate P0/);
    assert.match(event.release, /Sem crédito formal/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal reports the full solution rerun with skips and exclusions explicit', () => {
    const event = statusData.events.find(item => item.id === 'server-full-solution-regression-2026-10-24-2150');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /5\.286 aprovados, 255 ignorados, 0 falhas em 17 projetos/);
    assert.match(event.evidence, /LiveIntegrationTests/);
    assert.match(event.evidence, /Não valida produção\/runtime, MariaDB\/MongoDB reais, Linux\/ACL nem provedores live/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.testAudit.percent, 98.3);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal keeps the MariaDB isolation gate pending and does not claim production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-isolation-and-ca1416-review-2026-10-24-2155');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /recusa a porta padrão 3306/);
    assert.match(event.evidence, /1 aprovado, 1 ignorado, 0 falhas/);
    assert.match(event.evidence, /CA1416 é aviso do analisador/);
    assert.match(event.evidence, /Nenhuma validação MariaDB real, Linux ou produção é inferida/);
    assert.match(event.release, /MariaDB descartável/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the local installed runtime mismatch without release or homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-installed-runtime-recheck-2026-10-24-2233');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /SharedVersion\.cs declara 12\.1\.32/);
    assert.match(event.evidence, /módulos .* reportam 12\.1\.35/);
    assert.match(event.evidence, /40 aprovados, 0 ignorados, 0 falhas/);
    assert.match(event.evidence, /MariaDB 11\.4\.4 descartável.*ServerPersistenceMariaDbTests passou 13\/13 em 6 min 11 s/s);
    assert.match(event.evidence, /Datadir ficou em TEMP porque a remoção foi bloqueada/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.match(event.release, /Não houve .* restart, instalação, commit ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records MariaDB validation of the candidate without claiming runtime or production', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-candidate-mariadb-validation-2026-10-24-2300');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ServerPersistenceMariaDbTests` passou 12\/12/);
    assert.match(event.evidence, /ItemPersistenceServiceTests\|ItemPersistenceDatabaseLockTests` passaram 43\/43/);
    assert.match(event.evidence, /candidata local, não o binário\/runtime que gerou o log nem produção/);
    assert.match(event.release, /Não houve alteração no runtime, instalação, commit, release ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records lyric root symlink rejection without claiming TOCTOU or Linux validation', () => {
    const event = statusData.events.find(item => item.id === 'server-lyrics-configured-root-reparse-check-2026-10-24-2307');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /`LyricManagerPathSafetyTests` passou 22\/22/);
    assert.match(event.release, /Corrida TOCTOU, ancestrais configurados, ACL\/runtime Linux/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.progressPercent, null);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal reconciles current restore ZIP trust checks without claiming Linux production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-zip-trust-boundary-recheck-2026-10-24-2310');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /`FileShare\.Read`/);
    assert.match(event.evidence, /passou 105, ignorou 10 e falhou 0\/115/);
    assert.match(event.evidence, /UID confiável do serviço ainda poderia escrever/);
    assert.match(event.release, /Corridas de namespace Linux, escrita concorrente pelo mesmo UID/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.progressPercent, null);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records integrated restore handle evidence while Linux race gates remain open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-handle-acl-regression-2026-10-24-2203');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /BackupRestoreRecoveryJournal usando BackupRestoreDirectory por handles/);
    assert.match(event.evidence, /105 aprovados, 10 ignorados, 0 falhas \(115\)/);
    assert.match(event.evidence, /corridas ainda não têm reprodução determinística/);
    assert.match(event.release, /não valida Linux\/ACL efetiva, instalação ou produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records CI log unavailability and inconclusive NuGet scan without homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-ci-log-nuget-recheck-2026-10-25-0242');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /13 jobs da CI retornaram steps=null/);
    assert.match(event.evidence, /endpoints de logs amostrados responderam 404 BlobNotFound/);
    assert.match(event.evidence, /scan .* não produziu resultado e foi interrompido/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.progressPercent, null);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records a clean NuGet snapshot across every server project without production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-nuget-audit-2026-10-25-0251');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.match(event.evidence, /48\/48 exit 0 e mensagem de nenhum pacote vulnerável/);
    assert.match(event.evidence, /não tinha assets; foi restaurado individualmente/);
    assert.match(event.evidence, /não garantia futura/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records cross-session DeleteItem lock regression without homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-cross-session-delete-test-2026-10-25-0319');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession/);
    assert.match(event.evidence, /três testes selecionados.*NotExecuted/);
    assert.match(event.evidence, /não identifica PID\/versão/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.progressPercent, null);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records MariaDB Linux CI coverage for cross-session DeleteItem without claiming a hosted pass', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-delete-ci-coverage-2026-10-25-0324');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /job `user-mariadb-linux-tests`.*MariaDB 11\.4 descartável/);
    assert.match(event.evidence, /filtro agora inclui DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession/);
    assert.match(event.evidence, /Execução hospedada ainda não ocorreu/);
    assert.match(event.release, /não equivale a execução ou resultado verde/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.progressPercent, null);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records isolated MariaDB execution for cross-session DeleteItem without claiming Linux or production validation', () => {
  const event = statusData.events.find(item => item.id === 'server-image-deadlock-delete-isolated-mariadb-2026-10-25-0330');
  const server = statusData.tracks.find(track => track.id === 'server');

  assert.ok(event);
  assert.equal(event.status, 'in_progress');
  assert.equal(event.progressPercent, null);
  assert.match(event.evidence, /DeleteItem_WaitsForDatabaseAdvisoryLockHeldByAnotherSession` passou 1\/1/);
  assert.match(event.evidence, /MariaDB 11\.4\.4 isolado/);
  assert.match(event.evidence, /nenhuma execução hospedada desta alteração/);
  assert.match(event.release, /não substitui execução CI Linux nem valida o binário\/runtime de produção/);
  assert.equal(server.homologation.completed, 2);
  assert.equal(server.homologation.total, 7);
  assert.equal(server.homologation.percent, 28.6);
  assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records current server and Nebula regression without claiming production deadlock is fixed', () => {
    const event = statusData.events.find(item => item.id === 'server-dotnet-nebula-regression-deadlock-log-2026-10-25-0311');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /17 projetos, 5\.287 Passed, 255 NotExecuted, 0 Failed\/5\.542 resultados/);
    assert.match(event.evidence, /697 passaram em 13,95 s/);
    assert.match(event.evidence, /12 falhas de DELETE BaseItemImageInfos/);
    assert.match(event.evidence, /não informa PID\/versão/);
    assert.match(event.release, /Linux nativo, runtime\/produção, DB operacional e CI remota continuam sem validação/);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.equal(server.testAudit.skipped, 98);
    assert.equal(server.testAudit.failed, 0);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.progressPercent, null);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records full NuGet CI project coverage without claiming a hosted run', () => {
    const event = statusData.events.find(item => item.id === 'server-ci-nuget-coverage-2026-10-25-0620');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /43 projetos; cinco `.csproj` fora da solução.*NU1900–NU1905.*são tratados como erros/);
    assert.match(event.evidence, /cinco scans NuGet individuais locais passaram sem advisory conhecido/);
    assert.match(event.evidence, /Alteração de CI ainda não foi executada em runner hospedado/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records the IntroSkipper backup gap without formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-introskipper-backup-gap-2026-10-25-0628');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 90);
    assert.match(event.evidence, /IBackupDatabaseParticipant e manifesto DatabaseParticipants implementados no core/);
    assert.match(event.evidence, /IntroSkipperBackupParticipant registra as oito tabelas duráveis/);
    assert.match(event.evidence, /testes SQLite 4\/4/);
    assert.match(event.evidence, /focal da classe passou 4\/4, contrato de seleção CI 2\/2 e portal 181\/181/);
    assert.match(event.evidence, /preflight consulta o formato mapeado para detectar colunas faltantes antes do journal/);
    assert.match(event.evidence, /O fixture E2E de restore configura o banco central e os dois contextos IntroSkipper em schemas MariaDB/);
    assert.match(event.evidence, /cleanup usa CancellationToken\.None/);
    assert.match(event.evidence, /O job CI Linux seleciona o teste de roteamento de schema real e também `IntroSkipperBackupParticipantTests`/);
    assert.match(event.evidence, /os dois contextos IntroSkipper em schemas MariaDB 11\.4 exclusivos por teste/);
    assert.match(event.evidence, /execução E2E MariaDB local foi ignorada porque a conexão descartável não foi configurada/);
    assert.match(event.evidence, /Não há run remoto observável/);
    assert.match(event.release, /execução observável segue pendente/);
    assert.match(event.release, /Homologação formal servidor permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal records source-bound STRM resume without claiming runtime homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-strm-resume-source-identity-2026-10-25-0725');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /ambos os caminhos|Ambos os caminhos/);
    assert.match(event.evidence, /`feed_ftp`/);
    assert.match(event.evidence, /112\/112/);
    assert.match(event.evidence, /701\/701/);
    assert.match(event.evidence, /sem teste de rede\/runtime instalado ou transferência real/);
    assert.match(event.release, /mesma URL e o mesmo tamanho não são detectadas sem ETag\/If-Range/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal reports MariaDB deadlock evidence without claiming the patch is deployed', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-log-reconciliation-2026-10-25-0731');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /12 DELETE BaseItemImageInfos, 11 erros MariaDB 1213/);
    assert.match(event.evidence, /40\/40/);
    assert.match(event.evidence, /três integrações MariaDB selecionadas passaram pelo runner, mas foram ignoradas sem connection string local/);
    assert.match(event.evidence, /observado em d51f4f38 não contém `ItemPersistenceDatabaseLock`/);
    assert.match(event.release, /Nenhum DB operacional, instalação, restart, CI remota pós-patch, produção, release ou publicação remota/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the repeated deadlock log and focused rerun without MariaDB or production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-unit-rerun-2026-10-25-0758');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /SHA-256 do anexo é .* idêntico ao log já auditado/);
    assert.match(event.evidence, /ItemPersistenceServiceTests passou 31\/31/);
    assert.match(event.evidence, /foi selecionada e ignorada, pois MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não está configurada/);
    assert.match(event.evidence, /Contrato do portal passou 186\/186/);
    assert.match(event.release, /não prova que o binário instalado contém a proteção/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records post-participant-commit restore replay coverage without MariaDB or formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-backup-participant-postcommit-replay-2026-10-25-0806');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /encerra um processo filho real após o IntroSkipperBackupParticipant concluir/);
    assert.match(event.evidence, /um segundo processo executa RecoverInterruptedRestoreAsync com o participante real/);
    assert.match(event.evidence, /[Oo] cenário de encerramento\/reinício ainda não foi executado/);
    assert.match(event.evidence, /IntroSkipperBackupParticipantTests passou 5\/5/);
    assert.match(event.evidence, /FullSystemBackup passou 107 testes, ignorou 10 por plataforma\/ambiente e falhou 0\/117/);
    assert.match(event.evidence, /foi compilado, mas ignorado porque MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não está configurada/);
    assert.match(event.evidence, /WSL não está instalado e Docker\/Podman não foram encontrados/);
    assert.match(event.release, /ponto de falha multi-schema ainda não foi executado em MariaDB nem no runner Linux/);
    assert.match(event.release, /Homologação permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records isolated startup recovery subprocess evidence without implying full runtime homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-startup-restore-fail-closed-audit-2026-10-25-0819');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /RunAfterStartupRecoveryAsync/);
    assert.match(event.evidence, /recovery falho terminou com exit code 1 e sem marcador/);
    assert.match(event.evidence, /recovery bem-sucedido terminou com exit 0 e marcador/);
    assert.match(event.evidence, /ProgramStartupRecoveryTests Windows\/\.NET 10 Debug passou 6\/6/);
    assert.match(event.evidence, /não inicia StartApp nem valida MariaDB, proxy, migrações/);
    assert.match(event.release, /processo completo isolado, runtime Windows\/Linux e CI hospedada/);
    assert.match(event.release, /2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('portal publishes real disposable MariaDB persistence results without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-persistence-disposable-mariadb-2026-10-25-0736');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /MariaDB 11\.4\.4/);
    assert.match(event.evidence, /33217/);
    assert.match(event.evidence, /ServerPersistenceMariaDbTests` passou 14\/14, 0 ignorados\/falhas, em 24 s/);
    assert.match(event.release, /não do binário do incidente, CI Linux ou produção/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});
test('records migration restore lifecycle without claiming runtime concurrency verification', () => {
    const event = statusData.events.find(item => item.id === 'server-migration-restore-lifecycle-audit-2026-10-25-1005');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /nenhuma pede Metadata, Subtitles ou Trickplay/);
    assert.match(event.evidence, /não é alcançável pelas migrações atuais/);
    assert.match(event.evidence, /não exclui efeitos colaterais na descoberta de plugins/);
    assert.match(event.evidence, /não há base para atribuir o deadlock 1213/);
    assert.match(event.release, /sem alteração de runtime nem release/);
    assert.match(event.release, /2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the focused Nebula, localized-image, and restore audit without overstating verification', () => {
    const eventId = 'server-nebula-tmdb-restore-audit-2026-10-25-1033';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /TmdbUtilsTests \+ ProviderManagerImageProviderTests 32\/32/);
    assert.match(event.evidence, /NebulaDownloaderStartupTests \+ NebulaHostedServiceConfigurationTests 48\/48/);
    assert.match(event.evidence, /Python 3\.10 global foi bloqueada na coleta pelo guard de runtime isolado/);
    assert.match(event.evidence, /uv em venv temporária isolada usando requirements lock\/test passaram 53\/53/);
    assert.match(event.evidence, /restore IntroSkipper crash\/replay MariaDB\/Linux continua sem execução/);
    assert.match(event.release, /não recebe crédito de homologação formal/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('publishes the build-inclusive Release regression with skips and exclusions explicit', () => {
    const event = statusData.events.find(item => item.id === 'server-full-release-regression-2026-10-25-1050');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /exit 0: 5\.298 aprovados, 256 ignorados, 0 falhas\/5\.554 resultados em 17 assemblies/);
    assert.match(event.evidence, /NebulaPython completo via uv, Python 3\.10.*701\/701/);
    assert.match(event.evidence, /provedores live foram excluídos pelo filtro/);
    assert.match(event.release, /não comprova runtime instalado, Linux nativo, CI hospedada ou produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore platform skips and MariaDB opt-in without granting homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-gate-revalidation-2026-10-25-2018');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /BackupServiceTests \+ BackupRestoreDirectoryTests Release passaram 99\/110, 11 ignorados, 0 falhas/);
    assert.match(event.evidence, /BackupRestoreMariaDbTests passou 2\/4/);
    assert.match(event.evidence, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não está definida/);
    assert.match(event.evidence, /WSL\/distribuição não está instalado/);
    assert.match(event.release, /Homologação formal permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(statusData.coverage.completed, 357);
    assert.equal(statusData.coverage.pending, 74);
    assert.equal(statusData.coverage.inProgress, 7);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the OverlayFS restore rejection and keeps storage provenance open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-overlayfs-rejection-2026-10-25-2024');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /OverlayFS.*allowlist Linux/);
    assert.match(event.evidence, /BackupRestoreDirectoryTests passou 36.*8 ignorados/);
    assert.match(event.evidence, /matriz focal.*passou 6\/6/);
    assert.match(event.evidence, /GetVolumeInformationByHandleW.*NTFS\/ReFS/);
    assert.match(event.evidence, /ProgramStartupRecoveryTests passou 6\/6/);
    assert.match(event.evidence, /volumes Windows/);
    assert.match(event.release, /2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records image deadlock recheck without implying the local lock is installed', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-installation-recheck-2026-10-25-2032');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ItemPersistenceServiceTests \+ ItemPersistenceDatabaseLockTests passou 40\/40/);
    assert.match(event.evidence, /Dois testes MariaDB focados foram descobertos, mas ignorados/);
    assert.match(event.evidence, /DLL instalada consultada é 12\.1\.35/);
    assert.match(event.evidence, /não contém os marcadores ItemPersistenceDatabaseLock/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Nebula failure tracking with platform-appropriate path casing', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-failure-path-case-2026-10-25-2039');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /OrdinalIgnoreCase no Windows e Ordinal nos demais sistemas/);
    assert.match(event.evidence, /NebulaDownloaderEngineTests 44\/44/);
    assert.match(event.evidence, /suíte Python empacotada 703\/703/);
    assert.match(event.evidence, /Nebula aninhado 168 aprovados, 1 ignorado, 0 falhas/);
    assert.match(event.evidence, /em Windows; Linux nativo não foi validado/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the image and metadata MariaDB race test without claiming execution', () => {
    const event = statusData.events.find(item => item.id === 'server-image-metadata-concurrency-test-2026-10-25-2045');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /novo teste compila e foi descoberto, mas ficou ignorado/);
    assert.match(event.evidence, /MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING não está configurada/);
    assert.match(event.evidence, /não houve acesso ao banco operacional/);
    assert.match(event.evidence, /filtro do workflow CI inclui o caso/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records synchronous MariaDB advisory lock cleanup and keeps production gates open', () => {
    const event = statusData.events.find(item => item.id === 'server-db-advisory-sync-lock-cleanup-2026-10-25-2115');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /caminho síncrono agora considera o lock corrente potencialmente adquirido/);
    assert.match(event.evidence, /ItemPersistenceDatabaseLockTests\|ItemPersistenceServiceTests`: 44\/44/);
    assert.match(event.evidence, /1\.639 aprovados, 77 ignorados, 0 falhas\/1\.716/);
    assert.match(event.evidence, /Não foi executada integração MariaDB nesta sessão/);
    assert.match(event.release, /sem instalação, release, commit, push ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records behavioral lock cleanup tests without claiming MariaDB or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-db-advisory-lock-ambiguous-response-tests-2026-10-25-2123');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /provider MySQL ativa o ramo real da classe/);
    assert.match(event.evidence, /síncrono e assíncrono confirmam RELEASE_LOCK com o mesmo valor de nome/);
    assert.match(event.evidence, /1\.641 aprovados, 77 ignorados, 0 falhas\/1\.718/);
    assert.match(event.evidence, /Revisão Cavecrew: nenhum achado/);
    assert.match(event.release, /sem MariaDB descartável, Linux, CI hospedada, runtime instalado ou produção/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records repeated image deadlock and the retained Windows namespace ACL policy', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-and-restore-acl-recheck-2026-10-25-2129');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /12 DELETEs em BaseItemImageInfos e 11 deadlocks MariaDB 1213/);
    assert.match(event.evidence, /ItemPersistenceServiceTests\|ItemPersistenceDatabaseLockTests` passou 46\/46/);
    assert.match(event.evidence, /BackupRestoreDirectoryTests` final passou 36\/44, com 8 ignorados/);
    assert.match(event.evidence, /A alteração foi revertida/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records fail-closed ACL validation for the restore operation lease', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-lease-file-acl-2026-10-25-2134');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /AcquireOperationLease` agora chama `ValidateTrustedFile` antes de `FileStream\.Lock/);
    assert.match(event.evidence, /pede READ_CONTROL no Windows/);
    assert.match(event.evidence, /Teste focal passou 1\/1/);
    assert.match(event.evidence, /FullSystemBackup` Release Windows\/\.NET 10 passou 115, ignorou 11, falhou 0\/126/);
    assert.match(event.release, /homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records durable Nebula upload checkpoint replacement without claiming production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-checkpoint-directory-durability-2026-10-25-2145');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /POSIX sincroniza o diretório após os\.replace/);
    assert.match(event.evidence, /MoveFileExW com MOVEFILE_REPLACE_EXISTING \| MOVEFILE_WRITE_THROUGH/);
    assert.match(event.evidence, /NebulaPython completo: 703 aprovados, 2 ignorados/);
    assert.match(event.evidence, /não identifica PID\/assembly nem confirma o runtime/);
    assert.match(event.release, /sem commit, push, release ou deploy/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records pending MariaDB replay coverage for two independent backup participants', () => {
    const event = statusData.events.find(item => item.id === 'server-backup-restore-two-participants-2026-10-25-2212');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /participante real IntroSkipper e segundo banco em schema independente/);
    assert.match(event.evidence, /MariaDB real foi ignorado por ausência de connection string descartável/);
    assert.match(event.evidence, /Contrato do portal: 211\/211; contrato do filtro CI MariaDB: 2\/2/);
    assert.match(event.release, /homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the static restore ACL review without closing the Linux gate', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-acl-static-review-2026-10-25-2220');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /não confirmou bypass explorável por identidade não confiável/);
    assert.match(event.evidence, /janela check-then-act permanece nas primitivas Linux.*sem reprodução determinística/);
    assert.match(event.evidence, /FullSystemBackup` passou 115, ignorou 11, falhou 0\/126/);
    assert.match(event.evidence, /GitHub Actions mais recente continua em 21\/10\/2026, sem execução hospedada nova/);
    assert.match(event.release, /P0 de restore permanece aberto/);
    assert.match(event.release, /[Hh]omologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('reclassifies final-component Linux symlink race without overstating traversal risk', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-linux-final-entry-race-classification-2026-10-26-0112');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /sem seguir seu alvo/);
    assert.match(event.evidence, /integridade ao restore/);
    assert.match(event.release, /Linux nativo.*pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records explicit lyric persistence failures without claiming runtime validation', () => {
    const event = statusData.events.find(item => item.id === 'server-lyric-save-failure-propagation-2026-10-25-2228');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /TrySaveLyric retorna falha quando não há destino seguro/);
    assert.match(event.evidence, /Providers 25\/25; LyricsController 12\/12/);
    assert.match(event.release, /[Rr]untime instalado, produção.*não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records repeated image deadlock and the installed/source version mismatch', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-runtime-mismatch-2026-10-25-2228');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /byte-idêntico ao log já auditado/);
    assert.match(event.evidence, /DLL instalada 12\.1\.35.*checkout declara 12\.1\.32/);
    assert.match(event.evidence, /ItemPersistenceServiceTests \+ ItemPersistenceDatabaseLockTests: 46\/46/);
    assert.match(event.evidence, /Teste MariaDB: 0 aprovados, 2 ignorados/);
    assert.match(event.release, /correção local ainda não está na DLL instalada/);
    assert.match(event.release, /[Ii]ntegração MariaDB descartável permanece pendente/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records disposable MariaDB image concurrency validation without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-installation-recheck-2026-10-26-0050');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /16\/16 em MariaDB 11\.4\.13 descartável/);
    assert.match(event.evidence, /porta fechada/);
    assert.match(event.release, /DLL instalada permanece 12\.1\.35/);
    assert.match(event.release, /Linux\/CI hospedada, instalação e produção não foram validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records isolated Nebula MongoDB integration coverage without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-mongodb-opt-in-2026-10-26-0526');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /26\/26, 0 ignorados\/falhas/);
    assert.match(event.evidence, /127\.0\.0\.1:27099/);
    assert.match(event.evidence, /Mongo em 27017 não foi consultado/);
    assert.match(event.release, /Linux\/CI hospedada, Telegram real, runtime instalado e produção não foram validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records broad implementations regression with isolated MariaDB and MongoDB', () => {
    const event = statusData.events.find(item => item.id === 'server-impl-full-suite-mariadb-mongo-2026-10-26-0536');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /1\.742 aprovados, 24 ignorados, 0 falhas\/1\.766/);
    assert.match(event.evidence, /127\.0\.0\.1:33099.*127\.0\.0\.1:27099/);
    assert.match(event.release, /MariaDB foi iniciado com `--skip-grant-tables`/);
    assert.match(event.release, /Linux\/CI hospedada, runtime instalado e produção não foram validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records transient image-save retry coverage without claiming MariaDB or formal sign-off', () => {
    const event = statusData.events.find(item => item.id === 'server-image-save-retry-budget-tests-2026-10-25-2237');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /MariaDB 1205 e 1213 propagando a exceção após exatamente três tentativas/);
    assert.match(event.evidence, /falha permanente após uma tentativa/);
    assert.match(event.evidence, /ItemPersistenceServiceTests \+ ItemPersistenceDatabaseLockTests passou 49\/49/);
    assert.match(event.evidence, /dois cenários já descobertos seguem ignorados sem connection string/);
    assert.match(event.release, /sem crédito formal/);
    assert.match(event.release, /sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records localized image refresh provenance without claiming database persistence', () => {
    const event = statusData.events.find(item => item.id === 'server-localized-image-refresh-provenance-2026-10-25-2244');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /RefreshImages_DownloadsPreferredLanguageImageAndPreservesProvenance/);
    assert.match(event.evidence, /ItemImageProviderTests: 64\/64; ProviderManagerTests: 76\/76/);
    assert.match(event.release, /SaveImage foi mockada/);
    assert.match(event.release, /não valida bytes gravados nem persistência EF\/MariaDB/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records bounded library scan queue without claiming Trickplay or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-library-scan-bounded-queue-2026-10-25-2319');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /capacidade igual à concorrência configurada e FullMode=Wait/);
    assert.match(event.evidence, /dois itens pendentes e enqueue ainda incompleto/);
    assert.match(event.evidence, /Jellyfin\.Controller\.Tests 66\/66/);
    assert.match(event.release, /não tarefas fire-and-forget de Trickplay/);
    assert.match(event.release, /Sem carga representativa, Linux, runtime instalado ou produção/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore CI runner provisioning failure without counting it as executed tests', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-ci-runner-provisioning-recheck-2026-10-25-2349');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /13 jobs.*runner_id=0.*steps vazios/);
    assert.match(event.evidence, /Release 39608677913.*empacotamento Linux\/publicação (?:foram )?ignorados/);
    assert.match(event.evidence, /FullSystemBackup.*115 aprovados, 11 ignorados, 0 falhas\/126/);
    assert.match(event.evidence, /um único caller.*RecoverInterruptedRestoreAsync.*Program\.StartServer/);
    assert.match(event.release, /formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.match(event.release, /Sem alteração de runtime, release, instalação ou produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records trickplay temporary cleanup fix without claiming workload or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-trickplay-temp-cleanup-2026-10-25-2359');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /antes da mudança.*IOException/);
    assert.match(event.evidence, /TrickplayManagerAdmissionTests: 3 aprovados/);
    assert.match(event.evidence, /NonBlocking ainda pode acumular tarefas/);
    assert.match(event.release, /Linux, runtime instalado, carga representativa e produção não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('reconciles restore Add failure handling without claiming MariaDB or Linux validation', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-rollback-reaudit-2026-10-26-0002');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /DbContext\.Add em InvalidDataException/);
    assert.match(event.evidence, /115, ignorou 11 e falhou 0\/126/);
    assert.match(event.evidence, /Testes opt-in MariaDB não foram executados/);
    assert.match(event.release, /Linux nativo.*crash entre commit relacional e arquivos.*produção seguem pendentes/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records localized image MariaDB flow as locally verified without formal credit', () => {
    const event = statusData.events.find(item => item.id === 'server-localized-image-mariadb-flow-wiring-2026-10-26-0126');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /ImageSaver real/);
    assert.match(event.evidence, /MariaDB 11\.4\.13 descartável, isolado em 127\.0\.0\.1:33099/);
    assert.match(event.evidence, /17\/17/);
    assert.match(event.evidence, /57\/57/);
    assert.match(event.evidence, /datadir temporário específico permaneceu/);
    assert.match(event.release, /job Linux hospedado/);
    assert.match(event.release, /runtime instalado ou produção/);
    assert.match(event.release, /Nenhum crédito de homologação formal/);
    assert.match(event.release, /Homologação formal continua 2\/7 \(28,6%\), sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Windows restore ACL tests without closing Linux or formal restore gates', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-windows-acl-regression-2026-10-26-0130');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /36 aprovados, 8 ignorados, 0 falhas\/44 casos em Windows/);
    assert.match(event.release, /ACL\/syscalls Linux, corrida de entrada final, crash\/replay/);
    assert.match(event.release, /Restore P0 e homologação formal permanecem abertos em 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records IntroSkipper backup coverage without claiming Linux or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-introskipper-backup-mariadb-coverage-2026-10-26-0208');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /58 aprovados, 0 ignorados\/falhas/);
    assert.match(event.evidence, /oito tabelas persistentes/);
    assert.match(event.evidence, /limpeza de DetectionCache/);
    assert.match(event.evidence, /injeção do participante no BackupService/);
    assert.match(event.release, /CI Linux, runtime instalado e produção não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('reconciles the repeated image deadlock log with installed release and local regression evidence', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-repeat-installed-v12-1-35-2026-10-26-0212');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /SHA-256 do anexo coincide com o log já registrado/);
    assert.match(event.evidence, /DLL instalada reporta 12\.1\.35/);
    assert.match(event.evidence, /tag v12\.1\.35 não contém locks/);
    assert.match(event.evidence, /49\/49/);
    assert.match(event.evidence, /17\/17/);
    assert.match(event.release, /Nenhuma release foi criada ou instalada nesta rodada/);
    assert.match(event.release, /checkout HEAD está em v12\.1\.31/);
    assert.match(event.release, /Formal permanece 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore ZIP preflight coverage without claiming Linux or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-backup-restore-archive-preflight-2026-10-26-0230');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /76 aprovados, 3 ignorados, 0 falhas\/79/);
    assert.match(event.evidence, /traversal com.*caminho absoluto/);
    assert.match(event.evidence, /symlink ZIP/);
    assert.match(event.evidence, /não criou active-restore\.json/);
    assert.match(event.release, /Linux nativo, CI observável.*runtime instalado e produção não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Linux CI selection without claiming the modified checkout ran remotely', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-zip-ci-linux-selection-2026-10-26-0237');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /ubuntu-latest/);
    assert.match(event.evidence, /FullyQualifiedName!~Integration/);
    assert.match(event.evidence, /WSL reporta que não está instalado/);
    assert.match(event.release, /não execução do checkout modificado/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Windows restore path rules without claiming Linux execution', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-zip-platform-path-names-2026-10-26-0240');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /NUL\.txt.*COM¹\/²\/³\.txt.*ponto final.*espaço final/);
    assert.match(event.evidence, /82 aprovados, 3 ignorados, 0 falhas\/85/);
    assert.match(event.release, /cinco outras extrações ZIP sem preflight explícito/);
    assert.match(event.release, /Linux nativo e execução remota.*não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records bounded GetAvatar ZIP processing without claiming package authenticity or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-getavatar-zip-import-limits-2026-10-26-0252');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /OnlinePackServiceTests.*4\/4/);
    assert.match(event.evidence, /10\.000 entradas/);
    assert.match(event.evidence, /256 MiB/);
    assert.match(event.evidence, /Content-Length/);
    assert.match(event.release, /Autenticidade do pacote remoto.*pendentes/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records bounded ZIP extraction for plugin and server update callers without claiming Linux or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-bounded-zip-extraction-all-callers-2026-10-26-0314');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /BoundedZipArchiveExtractorTests.*14\/14/);
    assert.match(event.evidence, /1 GiB.*4 GiB.*50\.000/);
    assert.match(event.evidence, /quatro callers/);
    assert.match(event.evidence, /contrato da página de homologação passou 240\/240/);
    assert.match(event.release, /Linux nativo\/CI deste checkout.*produção não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records current full server implementations and restore regression without crediting skipped or Linux cases', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-and-implementations-regression-2026-10-26-0320');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /FullSystemBackup` passou 127, ignorou 11 e falhou 0/);
    assert.match(event.evidence, /passou 1\.674, ignorou 77 e falhou 0/);
    assert.match(event.evidence, /WSL informa não instalado/);
    assert.match(event.evidence, /Contrato portal 241\/241/);
    assert.match(event.release, /Linux\/ACL\/syscall.*produção não foram validados/);
    assert.match(event.release, /Homologação formal continua 2\/7 \(28,6%\), sem crédito ou release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records full solution release regression from TRX without crediting skips or integrations', () => {
    const event = statusData.events.find(item => item.id === 'server-full-solution-release-regression-2026-10-26-0329');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /5\.346 Passed, 80 NotExecuted\/skips, 0 Failed, 5\.426 no total/);
    assert.match(event.evidence, /31 MariaDB opt-in, 26 MongoDB opt-in, 19 Linux\/Unix, 2 macOS, 2 benchmarks/);
    assert.match(event.evidence, /MariaDB em 33099 e MongoDB em 27099 não estavam disponíveis/);
    assert.match(event.evidence, /17 TRX.*15 projetos.*2 projetos sem casos/);
    assert.match(event.release, /Filtro exclui todos os nomes de teste com `Integration`/);
    assert.match(event.release, /MongoDB local em 27017 não foi acessado/);
    assert.match(event.release, /formal permanece 2\/7 \(28,6%\), sem crédito ou release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore journal identity checks and remaining mount risk without homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-ancestor-acl-and-journal-alias-2026-10-26-0337');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /identidade por handle/);
    assert.match(event.evidence, /mount diferente de ProgramData/);
    assert.match(event.evidence, /passou 132, ignorou 12 e falhou 0\/144/);
    assert.match(event.evidence, /mesmo SHA-256 já auditado/);
    assert.match(event.release, /o próprio ProgramData for bind mount/);
    assert.match(event.release, /Linux nativo\/CI/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records GetAvatar redirect host validation without claiming live GitHub or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-getavatar-redirect-allowlist-2026-10-26-0503');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /desativa redirects automáticos/);
    assert.match(event.evidence, /CDN confiável/);
    assert.match(event.evidence, /bloqueio HTTP loopback antes de requisitar o alvo/);
    assert.match(event.evidence, /OnlinePackServiceTests.*13\/13/);
    assert.match(event.evidence, /AllowAutoRedirect=false/);
    assert.match(event.release, /comportamento ao vivo do CDN, Linux\/CI, runtime instalado e produção não foram validados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Trickplay admission backpressure without claiming load validation', () => {
    const event = statusData.events.find(item => item.id === 'server-trickplay-nonblocking-admission-check-2026-10-26-0724');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /o nono FetchAsync aguarda/);
    assert.match(event.evidence, /oito refreshes em background/);
    assert.match(event.release, /atraso potencial da varredura sob carga não foi medido/);
    assert.match(event.release, /Homologação formal 2\/7 \(28,6%\), sem crédito\/release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Windows restore regressions without crediting skipped Linux or production gates', () => {
    const event = statusData.events.find(item => item.id === 'server-fullsystembackup-windows-rerun-2026-10-26-0728');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /133 aprovados, 14 ignorados, 0 falhas\/147/);
    assert.match(event.evidence, /bind mount/);
    assert.match(event.release, /não fecha TOCTOU\/mount namespace/);
    assert.match(event.release, /Homologação formal segue 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Nebula lease roadmap reconciliation without claiming external validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-lease-roadmap-reconciliation-2026-10-26-0731');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /lease_id por tentativa/);
    assert.match(event.evidence, /lease_until de 300 s/);
    assert.match(event.evidence, /a nota antiga dizia que o claim dependia apenas de modified_at/i);
    assert.match(event.release, /retry_wait ainda foi registrado como pendente; a implementação foi concluída depois/);
    assert.match(event.release, /reconciliação local de ack Telegram também existe; sua integração real ainda não foi homologada/);
    assert.match(event.release, /identidade canônica/);
    assert.match(event.release, /Formal segue 2\/7 \(28,6%\), sem crédito\/release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Nebula retry_wait reconciliation without homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-retry-wait-doc-reconciliation-2026-10-26-0814');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /A segunda menção obsoleta em T2\.2\/T2\.5 foi corrigida/);
    assert.match(event.evidence, /reconciliação local de ack Telegram por UUID\/parte\/SHA-256/);
    assert.match(event.release, /Telegram\/Mongo reais/);
    assert.match(event.release, /Exactly-once não é garantido/);
    assert.match(event.release, /Homologação formal 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the repeated image deadlock without claiming MariaDB or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-duplicate-rerun-2026-10-26-1208');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /SHA-256 do anexo coincide exatamente/);
    assert.match(event.evidence, /passou 49\/49/);
    assert.match(event.evidence, /Três testes MariaDB.*ignorados/);
    assert.match(event.release, /não prova o runtime/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore mount-boundary hardening without Linux or homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-nested-mount-boundary-2026-10-26-1217');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /0 falhas\/182/);
    assert.match(event.evidence, /teste Linux de mount foi ignorado no Windows/i);
    assert.match(event.release, /Linux nativo/i);
    assert.match(event.release, /sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('clarifies that old restore helper findings are historical and keeps Linux gates open', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-helper-status-reconciliation-2026-10-26-1224');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /helper sem caller.*históricos/i);
    assert.match(event.evidence, /restore, replay, journal, rollback e cleanup/i);
    assert.match(event.release, /Linux nativo/i);
    assert.match(event.release, /casefold/i);
    assert.match(event.release, /Homologação formal 2\/7 \(28,6%\), sem crédito/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the v12.1.35 image lock gap and stronger MariaDB contention test without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-tag-gap-stress-regression-2026-10-26-1234');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /fonte da tag v12\.1\.35 não contém o advisory\/process lock/i);
    assert.match(event.evidence, /49\/49/);
    assert.match(event.evidence, /32 refreshes/);
    assert.match(event.evidence, /ignorado por ausência de MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING/);
    assert.match(event.release, /Nenhuma release, instalação ou publicação remota/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('requires all privileged Linux restore boundary tests to pass instead of silently skipping them', () => {
    const event = statusData.events.find(item => item.id === 'server-linux-restore-privileged-gate-2026-10-26-1327');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 0);
    assert.match(event.evidence, /dez casos Linux.*TRX.*resultado Passed.*Skipped/i);
    const filter = serverCiWorkflow.match(/--filter '([^']+)'/)?.[1] ?? '';
    for (const name of ['IsSameOrAncestorOf_DetectsBindMountedProgramDataInsideRestoreRoot', 'IsOnSameMountAs_DetectsBindMountedDirectory', 'ValidateTrustedWriteBoundary_RejectsWritableNamedUserInLinuxDefaultAcl']) {
        assert.ok(filter.includes(name));
        assert.ok(serverCiWorkflow.includes(`"${name}"`));
    }
    assert.match(event.release, /CI hospedada ainda não executou este checkout/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore gate expansion and keeps TrustedInstaller owner-only exception explicit', () => {
    const eventId = 'server-restore-linux-gate-acl-owner-separation-2026-10-26-2225';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /gate privilegiado agora exige 14 testes/i);
    assert.match(event.evidence, /136 aprovados, 16 ignorados por plataforma, 0 falhas\/152/);
    assert.match(event.evidence, /ValidateLinuxAclBytes aplicava a máscara POSIX a other::/);
    assert.match(event.evidence, /rejeita ausentes, duplicatas, testes inesperados, Skip/);
    assert.match(event.evidence, /Fixtures executadas no parser: 14 Passed aceita/);
    assert.match(event.evidence, /TrustedInstaller permanece exceção apenas para owner da âncora/);
    assert.match(event.release, /nenhum dos 14 testes Linux do gate foi executado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records broad Windows server regression without converting skipped tests into homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-full-regression-windows-2026-10-26-1338');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.378 Passed, 96 NotExecuted, 0 Failed/);
    assert.match(event.evidence, /37 MongoDB opt-in, 32 MariaDB opt-in, 24 plataforma\/SO, 2 benchmarks e 1 dependente de `\/dev\/shm`/);
    assert.match(event.evidence, /filtro excluiu testes Integration/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records installed image-persistence runtime mismatch without claiming production correction', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-installed-runtime-recheck-2026-10-26-1343');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.ok(event.evidence.includes('Program Files'));
    assert.match(event.evidence, /12\.1\.35/);
    assert.match(event.evidence, /VersionPrefix 12\.1\.32/);
    assert.match(event.evidence, /53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /11 deadlocks 1213 e 12 avisos de retry/);
    assert.match(event.release, /não pode ser correlacionado/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('keeps T5.6 in progress after verifying MariaDB pool and session recovery', () => {
    const feature = statusData.features.find(item => item.id === 'T5.6');
    const event = statusData.events.find(item => item.id === 'server-t56-database-degradation-coverage-audit-2026-10-26-1354');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(feature);
    assert.equal(feature.status, 'in_progress');
    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /100 conexões do pool/);
    assert.match(event.evidence, /encerra a sessão MariaDB/);
    assert.match(event.evidence, /passou 21\/21/);
    assert.match(event.evidence, /não reinicia o processo do banco/);
    assert.match(event.release, /T5\.6 continua aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records the MariaDB image query plan without marking all T5.6 gates complete', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-image-query-plan-test-2026-10-26-1429');
    const feature = statusData.features.find(item => item.id === 'T5.6');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /2\.048 linhas irrelevantes/);
    assert.match(event.evidence, /passou 22\/22/);
    assert.match(event.evidence, /IX_BaseItemImageInfos_ItemId_ImageType/);
    assert.match(event.release, /benchmark e demais cenários T5\.6 continuam pendentes/);
    assert.equal(feature.status, 'in_progress');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records missing image-index degradation behavior without promoting T5.6 or formal homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-missing-image-index-2026-10-26-1448');
    const feature = statusData.features.find(item => item.id === 'T5.6');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /type=ALL.*sem chave/);
    assert.match(event.evidence, /SaveImagesAsync.*(duas|2) imagens/);
    assert.match(event.evidence, /23\/23/);
    assert.match(event.release, /latência comparativa.*pendente/);
    assert.equal(feature.status, 'in_progress');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the synthetic image-index latency benchmark without claiming production performance', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-image-index-latency-benchmark-2026-10-26-1500');
    const feature = statusData.features.find(item => item.id === 'T5.6');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /100\.000 linhas sintéticas/);
    assert.match(event.evidence, /0,068 s/);
    assert.match(event.evidence, /28,809 s/);
    assert.match(event.evidence, /423x/);
    assert.match(event.evidence, /não valida DELETE, concorrência ou produção/);
    assert.match(event.release, /carga concorrente\/escritas, restart.*upgrade/);
    assert.equal(feature.status, 'in_progress');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the MariaDB restart test isolation requirement without claiming restart coverage', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-database-restart-isolation-audit-2026-10-26-1505');
    const feature = statusData.features.find(item => item.id === 'T5.6');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /loopback e porta diferente de 3306/);
    assert.match(event.evidence, /mata uma sessão, não reinicia o servidor/);
    assert.match(event.release, /harness dedicado.*PID criado por ele/);
    assert.equal(feature.status, 'in_progress');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records local image persistence restart and keeps hosted validation pending', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-mariadb-process-restart-retry-2026-10-26-1514');
    const feature = statusData.features.find(item => item.id === 'T5.6');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 90);
    assert.match(event.evidence, /até dez tentativas/);
    assert.match(event.evidence, /deadlocks\/lock timeouts 1205\/1213 mantêm limite três/);
    assert.match(event.evidence, /passou 1\/1 em 38,7 s/);
    assert.match(event.evidence, /MariaDB operacional 3306\/PID 31236 não foi acessado/);
    assert.match(event.release, /execução hospedada do CI/);
    assert.match(event.release, /Sem release, commit, push ou publicação remota/);
    assert.equal(feature.status, 'in_progress');
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('publishes MariaDB image persistence suite without overstating restart or homologation', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-mariadb-persistence-suite-2026-10-26-1537');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 90);
    assert.match(event.evidence, /23\/24: 23 aprovados, 1 ignorado, 0 falhas/);
    assert.match(event.evidence, /restart real passou separadamente 1\/1/);
    assert.match(event.evidence, /13 jobs sem runner\/steps\/logs/);
    assert.match(event.release, /homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records MariaDB image provenance schema upgrade while preserving existing rows', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-mariadb-migration-preserves-images-2026-10-26-1542');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, 90);
    assert.match(event.evidence, /LatestImageProvenanceMigration_PreservesExistingImageRows passou 1\/1/);
    assert.match(event.evidence, /ServerPersistenceMariaDbTests passou 24 aprovados, 1 ignorado, 0 falhas\/25/);
    assert.match(event.evidence, /não é o ciclo normal suportado/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records broad Windows server regression without crediting filtered integrations', () => {
    const event = statusData.events.find(item => item.id === 'server-regression-windows-release-2026-10-26-1610');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /terminou exit 0 nesta repetição com compilação/);
    assert.match(event.evidence, /5\.379 aprovados, 96 ignorados, 0 falhas\/5\.475/);
    assert.match(event.evidence, /dois projetos de integração não tiveram testes selecionados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\), sem crédito ou release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records repeated image deadlock log and focused lock tests without production claims', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-log-recheck-2026-10-26-1628');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /SHA-256 53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /passou 50\/50/);
    assert.match(event.evidence, /GitHub.*21\/10\/2026.*d51f4f38.*failure/);
    assert.match(event.evidence, /zero steps reportados/);
    assert.match(event.release, /PID\/assembly\/data de produção não constam no log/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the Windows restore regression while keeping Linux coverage pending', () => {
    const event = statusData.events.find(item => item.id === 'server-restore-windows-regression-2026-10-26-1632');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /133 aprovados, 15 ignorados, 0 falhas\/148/);
    assert.match(event.evidence, /WSL não está instalado/);
    assert.match(event.release, /Linux nativo, CI hospedada do checkout atual/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the isolated Nebula Python regression without claiming platform or production validation', () => {
    const event = statusData.events.find(item => item.id === 'server-nebula-python-suite-2026-10-26-1636');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /723 testes e ignorou 2\/725/);
    assert.match(event.evidence, /MoveFileExW/);
    assert.match(event.release, /Linux\/produção\/CI hospedada seguem sem validação/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the identical image deadlock log and keeps runtime causality unverified', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-identical-log-revalidation-2026-10-26-1638');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /SHA-256 do anexo `53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2`/);
    assert.match(event.evidence, /passaram 50\/50/);
    assert.match(event.release, /não identifica PID\/assembly\/versão/);
    assert.match(event.release, /Sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records current isolated MariaDB persistence integration without formal or production credit', () => {
    const event = statusData.events.find(item => item.id === 'server-t56-mariadb-persistence-current-2026-10-26-1643');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /passou 24, ignorou 1 e falhou 0\/25/);
    assert.match(event.evidence, /max_connections=256/);
    assert.match(event.evidence, /porta 33167 fechada/);
    assert.match(event.release, /carga prolongada\/representativa.*produção seguem pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records repeated image deadlock log without production homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-image-deadlock-identical-log-recheck-2026-10-26-1953');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /não contém PID\/AssemblyVersion/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
});

test('records the MariaDB startup mutex ownership fix without release or formal homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-startup-mutex-thread-ownership-fix-2026-10-26-2003');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /4\/4/);
    assert.match(event.evidence, /86\/86/);
    assert.match(event.release, /nenhuma release foi publicada/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
});

test('records MariaDB startup mutex thread ownership as an unverified runtime P1', () => {
    const event = statusData.events.find(item => item.id === 'server-mariadb-startup-mutex-thread-ownership-2026-10-26-1957');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.match(event.evidence, /ReleaseMutex/);
    assert.match(event.evidence, /thread diferente/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
});

test('records the serial full regression pass and parallel testhost crash without homologation credit', () => {
    const event = statusData.events.find(item => item.id === 'server-regression-full-solution-testhost-crash-isolated-api-pass-2026-10-26-2025');
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /serial.*15 assemblies.*5.401 aprovados, 97 ignorados, 0 falhas\/5.498/);
    assert.match(event.evidence, /exit 0/);
    assert.match(event.evidence, /erro fatal do CLR 0x80131506/);
    assert.match(event.evidence, /API passou 792\/795/);
    assert.match(event.evidence, /workflow agora limita.*-m:1/);
    assert.match(event.evidence, /38 requerem MULLETAFLIX_TEST_MONGODB_CONNECTION_STRING/);
    assert.match(event.evidence, /32 requerem MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING/);
    assert.match(event.evidence, /25 dependem de Linux\/macOS\/Unix e 2 são benchmarks/);
    assert.match(event.evidence, /CI tem jobs dedicados Mongo e MariaDB, mas .*falhou sem executar steps/);
    assert.match(event.evidence, /não prova sua causa/);
    assert.match(event.release, /sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records deterministic image refresh and delete concurrency without production or release credit', () => {
    const eventId = 'server-t56-deterministic-image-delete-overlap-2026-10-26-2115';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /passou 3\/3/);
    assert.match(event.evidence, /DeleteItem_ConcurrentWithBlockedImageRefresh_CompletesWithoutDeadlock/);
    assert.match(event.evidence, /33259 fechou/);
    assert.match(event.release, /runtime instalado, produção, CI hospedada e carga prolongada continuam pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Nebula full-mode startup coverage without real Mongo or production credit', () => {
    const eventId = 'server-nebula-full-mode-startup-orchestration-2026-10-26-2123';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /724 testes, com 2 skips POSIX intencionais e 0 falhas/);
    assert.match(event.evidence, /modo full-upload/);
    assert.match(event.release, /Mongo\/Telegram reais/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the repeated image deadlock log and Windows restore ACL boundary without formal credit', () => {
    const eventId = 'server-image-deadlock-and-restore-acl-audit-2026-10-26-2129';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /SHA-256.*53030F76669CB3FF502062EFD09E4B536C64C3229339B3D23F59628F8F8577E2/);
    assert.match(event.evidence, /BackupRestoreDirectoryTests.*39 aprovados, 10 ignorados/);
    assert.match(event.release, /homologação formal, que permanece 2\/7/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records MariaDB startup mutex revalidation without treating platform skips as passes', () => {
    const eventId = 'server-mariadb-startup-mutex-revalidation-2026-10-26-2243';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /MariaDbStartupMutexTests: 4 Passed, 0 Skipped, 0 Failed/);
    assert.match(event.evidence, /MariaDbProcessManagerLifecycleTests: 1 Skipped/);
    assert.match(event.release, /runtime instalado, produção e CI hospedada permanecem pendentes/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records an isolated MariaDB start-stop-start smoke without production credit', () => {
    const eventId = 'server-mariadb-start-stop-start-runtime-smoke-2026-10-26-2247';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /MariaDB 11\.4\.4/);
    assert.match(event.evidence, /StartStopStart_UsesTheSameIsolatedDataDirectory: 1 Passed/);
    assert.match(event.evidence, /porta e datadir descartáveis/);
    assert.match(event.release, /MariaDB operacional não foi acessado/);
    assert.match(event.release, /CI hospedada e validação de produção continuam pendentes/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records lyric upload path-safety regressions without production or formal credit', () => {
    const eventId = 'server-lyric-upload-path-safety-2026-10-27-0335';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /LyricManagerPathSafetyTests.*26\/26/);
    assert.match(event.evidence, /LyricsControllerTests.*12\/12/);
    assert.match(event.release, /produção.*sem release/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records item and branding image admin-route coverage without formal credit', () => {
    const eventId = 'server-image-admin-route-coverage-2026-10-27-0338';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /Sete rotas sensíveis/);
    assert.match(event.evidence, /401.*403/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records discovered RequiresElevation route coverage without closing formal homologation', () => {
    const eventId = 'server-requires-elevation-discovered-route-matrix-2026-10-27-0342';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /enumera cada endpoint RouteEndpoint/);
    assert.match(event.evidence, /AllowAnonymous/);
    assert.match(event.evidence, /34\/34/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records the local-access policy audit without formal or production credit', () => {
    const eventId = 'server-local-access-policy-route-audit-2026-10-27-0347';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /handler real passaram 8\/8/);
    assert.match(event.evidence, /Jellyfin\.Api, GetAvatar e MediaBrowser\.Providers/);
    assert.match(event.release, /bootstrap completo nem identidade real/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records MidiaStorage admin protection without formal or production credit', () => {
    const eventId = 'server-midia-storage-admin-authorization-2026-10-27-0353';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /SavePluginConfig.*GetStatus.*Sync/);
    assert.match(event.evidence, /ProtectedApiSurfacesHttpTests 5\/5/);
    assert.match(event.release, /sem release\/commit\/push/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records explicit public route intent and repeated image deadlock without production credit', () => {
    const eventId = 'server-public-route-intent-and-image-deadlock-repeat-2026-10-27-0408';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ProtectedApiSurfacesHttpTests passou 7\/7/);
    assert.match(event.evidence, /idêntico ao anterior/);
    assert.match(event.release, /não homologados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records current image deadlock validation limits without homologation credit', () => {
    const eventId = 'server-image-deadlock-reconciliation-status-2026-10-27-1318';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'in_progress');
    assert.equal(event.progressPercent, null);
    assert.match(event.evidence, /ignorado por falta de MULLETAFLIX_TEST_MARIADB_CONNECTION_STRING/);
    assert.match(event.evidence, /bloqueada pela politica do executor/);
    assert.match(event.release, /Sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records backup restore admission coverage without formal credit', () => {
    const eventId = 'server-backup-restore-heavy-admission-2026-10-27-1325';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.equal(event.progressPercent, 100);
    assert.match(event.evidence, /RestorePointInTime.*passou 2\/2/);
    assert.match(event.release, /Sem commit, push, release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records plugin MVC authorization coverage without runtime or release credit', () => {
    const eventId = 'server-auth-route-coverage-introskipper-2026-10-27-0411';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /IntroSkipper/);
    assert.match(event.evidence, /ProtectedApiSurfacesHttpTests passou 7\/7/);
    assert.match(event.release, /Sem release, commit ou push/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore destination root anchoring without Linux or formal homologation credit', () => {
    const eventId = 'server-restore-root-anchored-path-traversal-2026-10-27-0418';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /128 aprovados, 16 ignorados.*0 falhas/);
    assert.match(event.evidence, /IsSameDirectoryAs/);
    assert.match(event.release, /Linux.*não validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Windows restore regression and keeps Linux restore homologation open', () => {
    const eventId = 'server-restore-win-regression-linux-contract-2026-10-27-0427';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /137 aprovados, 16 ignorados por plataforma, 0 falhas\/153/);
    assert.match(event.evidence, /contrato dedicado.*passou 2\/2/i);
    assert.match(event.evidence, /14 nomes.*filtro.*TRX.*xUnit/i);
    assert.match(event.release, /Linux, ACL POSIX e mounts sem validação/);
    assert.match(event.release, /Restore P0 permanece aberto/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Nebula shutdown drain and pending restore replay before startup without formal credit', () => {
    const nebulaId = 'server-nebula-inflight-upload-shutdown-drain-2026-10-27-0440';
    const restoreId = 'server-restore-startup-real-recovery-gate-gap-2026-10-27-0440';
    const nebula = statusData.events.find(item => item.id === nebulaId);
    const restore = statusData.events.find(item => item.id === restoreId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(nebula);
    assert.ok(restore);
    assert.equal(statusData.events.filter(item => item.id === nebulaId).length, 1);
    assert.equal(statusData.events.filter(item => item.id === restoreId).length, 1);
    assert.equal(nebula.status, 'completed');
    assert.match(nebula.evidence, /725 aprovados, 2 ignorados, 0 falhas/);
    assert.equal(restore.status, 'completed');
    assert.equal(restore.progressPercent, 100);
    assert.match(restore.evidence, /RecoverInterruptedRestoreAsync_ProgramStartupGateReplaysJournalBeforeInitialization/);
    assert.match(restore.evidence, /Release 1\/1/);
    assert.match(restore.evidence, /percorre RunAfterBackupRecoveryAsync e ContinueAfterBackupRecoveryAsync usados pelo entry point/);
    assert.match(restore.evidence, /valida payload restaurado antes da continuação/);
    assert.match(restore.evidence, /dotnet format style passou nos três projetos/);
    assert.match(restore.release, /não inicia host real nem executa migrations\/InitializeServices/);
    assert.match(restore.release, /Linux\/ACL operacional.*não validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(nebula.updatedAt) <= Date.parse(statusData.generatedAt));
    assert.ok(Date.parse(restore.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records restore before pre-initialization migrations without formal credit', () => {
    const eventId = 'server-restore-before-preinitialisation-2026-10-27-0515';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.scope, /recovery.*antes de ApplyStartupMigrationAsync/);
    assert.match(event.evidence, /ProgramStartupRecoveryTests passou 4\/4/);
    assert.match(event.evidence, /journal real/);
    assert.match(event.release, /não executa host integral/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records stable NextUp series ordering without claiming MariaDB or production validation', () => {
    const eventId = 'server-nextup-series-tie-order-2026-10-27-1024';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /NextUpQueryOptimizationTests passou 8\/8/);
    assert.match(event.evidence, /timestamps iguais/);
    assert.match(event.release, /não valida tradução Pomelo\/MariaDB, CI hospedada, runtime instalado nem produção/);
    assert.match(event.release, /sem release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records HTTP conditional precedence at controller level without formal credit', () => {
    const eventId = 'server-if-none-match-rfc-2026-10-27-1037';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /controller.*44\/44/i);
    assert.match(event.evidence, /If-Modified-Since.*If-None-Match/);
    assert.match(event.release, /runtime instalado nem produção/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records collision-safe HLS cache key framing without formal homologation credit', () => {
    const eventId = 'server-hls-cache-key-framing-2026-10-27-1340';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /StreamStateCacheTests passou 18\/18/);
    assert.match(event.evidence, /DynamicHlsControllerTests passou 7\/7/);
    assert.match(event.release, /Sem commit, push, release/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records backup restore exclusion re-audit without closing formal homologation', () => {
    const eventId = 'server-backup-restore-exclusion-reaudit-2026-10-27-1345';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ConcurrentRestoreRequests_AreRejectedWithoutBlockingPlayback passou 2\/2/);
    assert.match(event.evidence, /CreateBackupAsync_RejectsConcurrentOperationAndReleasesGateAfterFailure passou 1\/1/);
    assert.match(event.release, /sem mudança de código/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records reverse backup restore exclusion regression without homologation credit', () => {
    const eventId = 'server-backup-restore-exclusion-reverse-test-2026-10-27-1400';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /RestoreBackupAsync_RejectsConcurrentBackupUntilRestoreCompletes/);
    assert.match(event.evidence, /passaram 3\/3/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records current-head full backup restore regression without Linux or formal credit', () => {
    const eventId = 'server-backup-fullsystem-current-head-2026-10-27-1404';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /142 aprovados, 16 ignorados, 0 falhas\/158/);
    assert.match(event.evidence, /14 casos exclusivos de Linux/);
    assert.match(event.release, /não validam Linux\/macOS/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records current-head solution regression with per-TRX reconciliation and no formal credit', () => {
    const eventId = 'server-solution-regression-head-fa1289b-2026-10-27-1425';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /5\.473 Passed, 98 NotExecuted, 0 falhas\/5\.571/);
    assert.match(event.release, /filtro exclui casos cujo FQN contém Integration/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records scheduled restore shutdown signal without claiming a process restart', () => {
    const eventId = 'server-restore-scheduling-shutdown-signal-2026-10-27-1434';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /StopApplication\(\).*verifica exatamente uma chamada/);
    assert.match(event.release, /[Nn]ão valida encerramento\/relançamento de processo/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records scheduled restore recovery before startup without claiming a process relaunch', () => {
    const eventId = 'server-restore-scheduled-archive-startup-gate-2026-10-27-1445';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /ScheduledRestore_RestoresArchiveBeforeStartupInitialization passou 1\/1/);
    assert.match(event.evidence, /Program\.CaptureRestartState/);
    assert.match(event.evidence, /migração\/inicialização foram omitidas/);
    assert.match(event.release, /não executa Program\.Main, relançamento real do processo/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records scheduler cleanup and disposal races without crediting formal homologation', () => {
    const eventId = 'server-library-scheduler-cancellation-race-2026-10-27-1517';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /Jellyfin\.Controller\.Tests 69\/69/);
    assert.match(event.evidence, /Enqueue_CleansIdleWorkersAfterGracePeriod passou 1\/1 após 65 s/);
    assert.match(event.evidence, /EnqueueDuringCleanupStartsNewWorkersAndCompletes manteve o enqueue bloqueado até o término dos callbacks de cancelamento/);
    assert.match(event.evidence, /DisposeAsync_WaitsForAdmittedEnqueueToDrain validou duas chamadas DisposeAsync simultâneas/);
    assert.match(event.evidence, /5\.477 aprovados, 98 NotExecuted, 0 falhas/);
    assert.match(event.evidence, /Revalidação focal no checkout atual passou 4\/4 em Windows \.NET 10 Release/);
    assert.match(event.release, /CI hospedada, Linux, runtime instalado e produção não foram validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records passed real DataStore logout test without APK homologation credit', () => {
    const eventId = 'android-logout-real-datastore-failure-2026-10-27-1406';
    const event = statusData.events.find(item => item.id === eventId);
    const android = statusData.tracks.find(track => track.id === 'android');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /AndroidJUnit passou 1\/1/);
    assert.match(event.evidence, /zero requisições ao MockWebServer/);
    assert.match(event.release, /formalSignoff=pending/);
    assert.equal(android.homologation.completed, 10);
    assert.equal(android.homologation.total, 21);
    assert.equal(android.homologation.percent, 47.6);
    assert.equal(statusData.androidHomologation.formalSignoff, 'pending');
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records scheduler graceful-shutdown fix without formal homologation credit', () => {
    const eventId = 'server-library-scheduler-host-stopping-2026-11-02-2057';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /LimitedConcurrencyLibrarySchedulerTests passou 7\/7/);
    assert.match(event.evidence, /Jellyfin\.Controller\.Tests passou 72\/72/);
    assert.match(event.release, /Linux, CI hospedada, runtime instalado e produção não foram validados/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records Live TV and IPTV URL log redaction without production credit', () => {
    const eventId = 'server-livetv-url-log-redaction-2026-11-02-2326';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'completed');
    assert.match(event.evidence, /Jellyfin\.LiveTv\.Tests focal: 19\/19; Jellyfin\.Providers\.Tests focal: 11\/11/);
    assert.match(event.evidence, /exceção sintética com URL sensível/);
    assert.match(event.evidence, /Providers focal cumulativo: 152\/152/);
    assert.match(event.evidence, /ItemImageProvider, DramaBox, ListenBrainz/);
    assert.match(event.evidence, /falhas de transporte em ShortMax\/YouTube imagens/);
    assert.match(event.evidence, /outros logs de exceção brutos/);
    assert.match(event.evidence, /Providers completo na continuação: 893 aprovados, 13 ignorados, 0 falhas/);
    assert.match(event.evidence, /Live TV completo: 81\/81/);
    assert.match(event.evidence, /OpenLibrary, letras, legendas/);
    assert.match(event.evidence, /resposta bruta da Schedules Direct foi removida/);
    assert.match(event.evidence, /T6\.4 permanece parcial/);
    assert.match(event.release, /não instalada nem publicada em release.*formal permanece 2\/7 \(28,6%\); T6\.4 segue parcial/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records upstream media info, image, cover, and TMDb audit fixes without formal credit', () => {
    const eventId = 'server-media-images-covers-tmdb-upstream-audit-2026-11-03-2212';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /MetadataService\.RefreshWithProviders agora faz merge de localItem\.Images antes de localItem\.RemoteImages/);
    assert.match(event.evidence, /ItemImageProvider\.MergeImages usa IsImageStoredWithMedia/);
    assert.match(event.evidence, /TmdbPersonImageProvider, TmdbMovieProvider, TmdbSeriesProvider, TmdbSeasonProvider, TmdbEpisodeProvider/);
    assert.match(event.evidence, /LibraryManager\.ConvertImageToLocal tenta todas as URLs separadas por pipe/);
    assert.match(event.evidence, /Jellyfin\.Providers\.Tests 944 aprovados, 13 ignorados, 0 falhas\/957/);
    assert.match(event.evidence, /Jellyfin\.Server\.Implementations\.Tests 1\.755 aprovados, 95 ignorados, 0 falhas\/1\.850/);
    assert.match(event.evidence, /Jellyfin\.Api\.Tests 831 aprovados, 3 ignorados, 0 falhas\/834/);
    assert.match(event.release, /runtime instalado 12\.1\.45 não contém as mudanças/i);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.equal(server.testAudit.passed, 5642);
    assert.equal(server.testAudit.total, 5740);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records disposable MongoDB and MariaDB integration homologation, LocalMetadata fixes, NebulaPython, and Web suites without formal credit', () => {
    const eventId = 'server-local-disposable-mongo-mariadb-homologation-2026-11-03-2345';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /Category=RequiresMongo passou 38 aprovados, 0 ignorados, 0 falhas\/38/);
    assert.match(event.evidence, /IntroSkipper\.Integration\.Tests passou 96 aprovados, 0 ignorados, 0 falhas\/96/);
    assert.match(event.evidence, /BackupRestoreMariaDbTests 4\/4/);
    assert.match(event.evidence, /SaveImagesAsync_RetriesAfterMariaDbProcessRestartDuringDelete 1\/1/);
    assert.match(event.evidence, /Jellyfin\.Server\.Integration\.Tests executada por classe isolada com mysqldump no PATH passou 146 aprovados, 0 ignorados, 0 falhas\/146 casos únicos nas 26 classes/);
    assert.match(event.evidence, /ImageControllerProfileUploadTests 1\/1/);
    assert.match(event.evidence, /EpisodeLocalImageProvider usa OrdinalIgnoreCase para subpasta metadata/);
    assert.match(event.evidence, /LocalImageProvider normaliza Extension\.ToLowerInvariant\(\)/);
    assert.match(event.evidence, /NebulaPython pytest passou 726 aprovados, 2 ignorados \(POSIX\), 0 falhas/);
    assert.match(event.evidence, /MulletaFlix-web-master Vitest 76 arquivos\/421 testes aprovados/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\)/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});

test('records system-wide unhomologated mapping, isolated live server /metrics smoke on 18096/33188, and Playwright/WCAG 2.2 AA suites without formal credit', () => {
    const eventId = 'system-unhomologated-mapping-isolated-smoke-playwright-2026-11-04-0030';
    const event = statusData.events.find(item => item.id === eventId);
    const server = statusData.tracks.find(track => track.id === 'server');

    assert.ok(event);
    assert.equal(statusData.events.filter(item => item.id === eventId).length, 1);
    assert.equal(event.status, 'partial');
    assert.match(event.evidence, /MariaDbProcessManager\.ResolveStartupMutexNames escopa o mutex por porta não padrão/);
    assert.match(event.evidence, /SecureConnectionProcessManager\.ShouldSkipForIsolatedTestEnvironment/);
    assert.match(event.evidence, /MetricsLabelRedactionTests passou 14\/14 em Release/);
    assert.match(event.evidence, /\/metrics respondeu HTTP 200 \(326 linhas, 28\.317 bytes\) com 0 ocorrências de mysqlconnector, pool_name, User ID, Server= ou 33188/);
    assert.match(event.evidence, /playwright\.a11y\.config\.ts passou 3\/3/);
    assert.match(event.evidence, /playwright\.scrollbuttons\.config\.ts passou 2\/2/);
    assert.match(event.evidence, /playwright\.requests\.config\.ts passou 8\/8/);
    assert.match(event.evidence, /294,3 KB gzip \/ budget 420 KB/);
    assert.match(event.release, /Homologação formal permanece 2\/7 \(28,6%\) no servidor e 10\/21 \(47,6%\) no Android/);
    assert.equal(server.homologation.completed, 2);
    assert.equal(server.homologation.total, 7);
    assert.equal(server.homologation.percent, 28.6);
    assert.ok(Date.parse(event.updatedAt) <= Date.parse(statusData.generatedAt));
});
