import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

const statusData = JSON.parse(readFileSync(new URL('../homologacao-status.json', import.meta.url), 'utf8'));
const dashboardSource = readFileSync(new URL('../homologacao.js', import.meta.url), 'utf8');

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
    assert.match(dashboard.getElement('feature-count').textContent, new RegExp(`^${statusData.features.length} tarefas`));
    assert.equal((dashboard.getElement('feature-table').innerHTML.match(/<tr>/g) || []).length, 25);
    assert.equal(dashboard.getElement('feature-page-number').textContent, `Página 1 de ${Math.ceil(statusData.features.length / 25)}`);
});
test('search, status filter and pagination expose the matching roadmap rows', async () => {
    const dashboard = createDashboard();
    await settleDashboard();

    dashboard.getElement('feature-search').value = 'T6.1';
    dashboard.getElement('feature-search').listeners.input();
    assert.match(dashboard.getElement('feature-table').innerHTML, /T6\.1/);
    const t61Count = statusData.features.filter(feature => `${feature.id} ${feature.name} ${feature.area}`.includes('T6.1')).length;
    assert.equal((dashboard.getElement('feature-table').innerHTML.match(/<tr>/g) || []).length, t61Count);
    assert.equal(dashboard.getElement('feature-page-status').textContent, `Itens 1–${t61Count} de ${t61Count}`);

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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

test('portal renders blocked validation events with a Portuguese warning state', async () => {
    const blockedEvent = statusData.events.find(item => item.id === 'android-local-network-api37-emulator-boot-blocked-2026-10-13');
    const dashboard = createDashboard({
        ...statusData,
        events: [blockedEvent, ...statusData.events.filter(item => item !== blockedEvent)]
    });
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
    assert.equal(android.testAudit.passed, 1788);
    assert.equal(android.testAudit.total, 1788);
    assert.match(android.testAudit.label, /1\.788 testes.*256 XML/);
    assert.ok(event, 'portal should expose the latest bounded embedded CSS reader evidence');
    assert.match(event.evidence, /39\/39.*256 Ki caracteres.*2\.048 regras/);
    assert.equal(matrix.areas.filter(item => item.status === 'completed').length, 10);
    assert.equal(matrix.areas.filter(item => item.status === 'in_progress').length, 2);
    assert.equal(matrix.areas.filter(item => item.status === 'pending').length, 9);
    assert.equal(matrix.formalSignoff, 'pending');

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Preservar ênfase CSS embutido no leitor HTML\/KF8/);
});

test('portal mirrors accessible paginated PDF OCR progress without promoting APK-H21', async () => {
    const android = statusData.tracks.find(track => track.id === 'android');
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-book-text-search-accessible-progress-2026-10-15');

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    const dashboard = createDashboard();
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

    const dashboard = createDashboard();
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

    const dashboard = createDashboard();
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

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Estilos aninhados e decoração CSS propagada/);
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

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Comandos SyncPlay aplicados ao Media3 durante preparação da mídia/);
});

test('portal mirrors offline download fixture race correction without formal promotion', async () => {
    const matrix = statusData.androidHomologation;
    const event = statusData.events.find(item => item.id === 'android-offline-download-completion-race-2026-10-16');

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Corrigir a sequência do fixture de retomada offline/);
});

test('portal records each Android work-round review without fabricating homologation progress', async () => {
    const event = statusData.events.find(item => item.id === 'android-homologation-round-sync-2026-10-16-1819');
    const matrix = statusData.androidHomologation;

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Revisão da homologação Android nesta rodada/);
});

test('portal records the API 37.1 boot blocker without counting tests or closing APK-H14', async () => {
    const event = statusData.events.find(item => item.id === 'android-api37-1gapi-systemserver-restart-2026-10-16-1841');
    const matrix = statusData.androidHomologation;

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Nova imagem Android 17 sem boot completo/);
});

test('portal records API 37.0 system-server death with zero Android tests and no formal promotion', async () => {
    const event = statusData.events.find(item => item.id === 'android-api37-0gapi-systemserver-deadobject-2026-10-16-1906');
    const matrix = statusData.androidHomologation;

    assert.match(statusData.generatedAt, /^2026-10-\d{2}T\d{2}:\d{2}:\d{2}-03:00$/);
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

    const dashboard = createDashboard();
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
    assert.equal(audit.passed, 1437);
    assert.equal(audit.total, 1497);
    assert.equal(audit.skipped, 60);
    assert.equal(audit.failed, 0);
    assert.match(audit.label, /sem validação em produção/i);

    const dashboard = createDashboard();
    await settleDashboard();
    assert.match(dashboard.getElement('event-list').innerHTML, /Nebula restrito às pastas de biblioteca configuradas/);
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

    const dashboard = createDashboard();
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

    const dashboard = createDashboard();
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

    const dashboard = createDashboard();
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

    const dashboard = createDashboard();
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
