#!/usr/bin/env node
import { existsSync } from 'node:fs';
import { chmod, mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn, spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import net from 'node:net';
import os from 'node:os';
import fg from 'fast-glob';
import {
    DEFAULT_STAGE_BASE_URL,
    evaluatePlaywrightGate,
    extractPlaywrightStats,
    getStageBaseUrl,
    writePlaywrightReportArtifacts
} from '../../tests/playwright/support/index.mjs';
import {
    assertSafeRemovalTargets,
    assertStagePidFileAbsent,
    buildRestrictedDirectoryAclArgs,
    buildStageKillArgs,
    buildStageRemovalTargets,
    isDedicatedStageDataPath,
    parseStagePid,
    parseWindowsUserSid,
    assertSafeStageBaseUrl,
    assertStageVersion,
    resolveDotnetExecutable
} from './stage-safety.mjs';
import { seedPlaywrightMediaFixtures } from './media-fixtures.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..', '..');
const reportDir = path.join(repoRoot, 'reports', 'playwright');
let failureReportDir = reportDir;
const configPath = path.join(repoRoot, 'playwright.stage.config.ts');
const fixtureConfigPath = path.join(repoRoot, 'playwright.fixtures.config.ts');
const specDir = path.join(repoRoot, 'tests', 'playwright', 'specs');
const playwrightCli = path.join(repoRoot, 'node_modules', '@playwright', 'test', 'cli.js');
const viteFixtureSpecNames = new Set([
    '24-scrollbuttons.spec.ts',
    '25-myrequests.spec.ts',
    '26-request-autocomplete.spec.ts',
    '27-nebula-navigation.spec.ts',
    '28-backup-coverage.spec.ts',
    '29-accessibility.spec.ts'
]);
const workspaceRoot = path.resolve(repoRoot, '..');
const stageDll = process.env.PW_STAGE_DLL ? path.resolve(process.env.PW_STAGE_DLL) : path.join(workspaceRoot, 'stage', 'MulletaFlix.dll');
const defaultStageDataDir = path.join(workspaceRoot, 'stage-data');
const configuredStageDataDir = process.env.PW_ISOLATED_STAGE_DATA_DIR;
const stageDataDir = configuredStageDataDir ? path.resolve(configuredStageDataDir) : defaultStageDataDir;
if (configuredStageDataDir) {
    if (!isDedicatedStageDataPath(stageDataDir, workspaceRoot, os.tmpdir())) {
        throw new Error('PW_ISOLATED_STAGE_DATA_DIR must be a dedicated mflx-playwright-stage-* child of the workspace or be inside the system temp folder.');
    }
}
const legacyStageDataDir = path.join(workspaceRoot, 'stage', 'data', 'jellyfin-test');
const stagePidFile = path.join(stageDataDir, 'stage.pid');
const taskkillExecutable = path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'taskkill.exe');
const dotnetExecutable = resolveDotnetExecutable(process.platform, process.env.ProgramFiles);
const allowedStageRoots = [
    path.join(workspaceRoot, 'stage'),
    stageDataDir
];
const stageRemovalTargets = configuredStageDataDir ? [ stageDataDir ] : buildStageRemovalTargets({
    workspaceRoot,
    stageDataDir,
    legacyStageDataDir
});
let ownsStageProcess = false;

function parseBaseUrl(baseUrl) {
    const parsed = new URL(baseUrl);
    if (parsed.protocol !== 'http:' || parsed.hostname !== '127.0.0.1' && parsed.hostname !== 'localhost') {
        throw new Error(`Stage base URL must use local HTTP: ${baseUrl}`);
    }

    const port = parsed.port ? Number(parsed.port) : 80;
    if (!Number.isInteger(port) || port < 1 || port > 65535) {
        throw new Error(`Invalid stage port in base URL: ${baseUrl}`);
    }

    return { parsed, port };
}

function isPortAvailable(port) {
    return new Promise(resolve => {
        const server = net.createServer();
        server.once('error', () => resolve(false));
        server.once('listening', () => server.close(() => resolve(true)));
        server.listen(port, '127.0.0.1');
    });
}

function stripTrailingSlashes(value) {
    let end = value.length;
    while (end > 0 && value[end - 1] === '/') {
        end--;
    }

    return value.slice(0, end);
}

async function selectStageBaseUrl(requestedBaseUrl) {
    assertSafeStageBaseUrl(requestedBaseUrl);
    const { parsed, port } = parseBaseUrl(requestedBaseUrl);
    const existingStage = await probeStage(requestedBaseUrl);
    if (process.env.PW_BASE_URL && existingStage.reachable) {
        throw new Error(`PW_BASE_URL points to an already-running stage: ${requestedBaseUrl}`);
    }

    if (!process.env.PW_BASE_URL && !existingStage.reachable && await isPortAvailable(port)) {
        return stripTrailingSlashes(requestedBaseUrl);
    }

    for (let candidate = 18096; candidate <= 18196; candidate++) {
        if (await isPortAvailable(candidate)) {
            parsed.port = String(candidate);
            const selected = stripTrailingSlashes(parsed.toString());
            console.error(`[playwright] requested stage port ${port} is busy; using isolated port ${candidate}`);
            return selected;
        }
    }

    throw new Error(`No isolated stage port is available near ${port}. Set PW_BASE_URL explicitly.`);
}

async function selectStageDatabasePort() {
    for (let candidate = 33100; candidate <= 33200; candidate++) {
        if (await isPortAvailable(candidate)) {
            return candidate;
        }
    }

    throw new Error('No isolated MariaDB port is available in the Playwright stage range 33100-33200.');
}

async function selectFixturePort() {
    for (let candidate = 18197; candidate <= 18297; candidate++) {
        if (await isPortAvailable(candidate)) {
            return candidate;
        }
    }

    throw new Error('No isolated Vite fixture port is available in the Playwright range 18197-18297.');
}

async function writeStageNetworkConfiguration(baseUrl) {
    const { port } = parseBaseUrl(baseUrl);
    const configDir = path.join(stageDataDir, 'config');
    await mkdir(configDir, { recursive: true });
    // The first startup may run the legacy network migration, while a reused
    // migration state reads the current schema directly. Keep both spellings
    // so an isolated stage never falls back to the production port 8096.
    const networkConfig = `<NetworkConfiguration>\n  <HttpServerPortNumber>${port}</HttpServerPortNumber>\n  <PublicPort>${port}</PublicPort>\n  <InternalHttpPort>${port}</InternalHttpPort>\n  <PublicHttpPort>${port}</PublicHttpPort>\n  <HttpsPortNumber>8920</HttpsPortNumber>\n  <PublicHttpsPort>8920</PublicHttpsPort>\n  <EnableIPV4>true</EnableIPV4>\n  <EnableIPV6>false</EnableIPV6>\n  <EnableIPv4>true</EnableIPv4>\n  <EnableIPv6>false</EnableIPv6>\n  <EnableRemoteAccess>false</EnableRemoteAccess>\n  <AutoDiscovery>false</AutoDiscovery>\n  <LocalNetworkAddresses>\n    <string>127.0.0.1</string>\n  </LocalNetworkAddresses>\n</NetworkConfiguration>\n`;
    await writeFile(path.join(configDir, 'network.xml'), networkConfig, 'utf8');

    // The default Nebula configuration is production-oriented. A browser test
    // must never inherit live Telegram/Mongo/media integrations from the host.
    const nebulaConfig = '<NebulaFtpConfiguration>\n  <Enabled>false</Enabled>\n  <NotificationsEnabled>false</NotificationsEnabled>\n  <NotificationsChannelIds />\n  <TelegramNotificationsEnabled>false</TelegramNotificationsEnabled>\n  <TelegramNotificationChatIds />\n  <ChatId />\n  <MonitorPaths />\n  <StagePaths />\n  <UseMappedDrive>false</UseMappedDrive>\n  <DeleteSourceAfterUpload>false</DeleteSourceAfterUpload>\n  <BotTokens />\n  <ApiHash />\n  <SupabaseKey />\n</NebulaFtpConfiguration>\n';
    await writeFile(path.join(configDir, 'nebulaftp.xml'), nebulaConfig, 'utf8');
}

function parseArgs(argv) {
    const args = {
        baseUrl: getStageBaseUrl(),
        reportDir,
        specDir,
        grep: null,
        resetStage: process.env.PW_RESET_STAGE !== 'false'
    };

    for (const entry of argv) {
        if (entry.startsWith('--base-url=')) {
            args.baseUrl = entry.slice('--base-url='.length);
        } else if (entry.startsWith('--report-dir=')) {
            args.reportDir = path.resolve(entry.slice('--report-dir='.length));
        } else if (entry.startsWith('--spec-dir=')) {
            args.specDir = path.resolve(entry.slice('--spec-dir='.length));
        } else if (entry.startsWith('--grep=')) {
            args.grep = entry.slice('--grep='.length);
        } else if (entry === '--no-reset-stage') {
            args.resetStage = false;
        }
    }

    assertSafeStageBaseUrl(args.baseUrl);

    return args;
}

async function probeStage(baseUrl) {
    try {
        const response = await fetch(`${stripTrailingSlashes(baseUrl)}/System/Info/Public`, {
            cache: 'no-cache',
            signal: AbortSignal.timeout(1500)
        });
        if (!response.ok) {
            throw new Error(`Stage probe returned HTTP ${response.status}.`);
        }

        const info = await response.json();
        return {
            reachable: true,
            startupWizardCompleted: Boolean(info?.StartupWizardCompleted),
            systemId: info?.Id || null,
            version: info?.Version || null,
            error: null
        };
    } catch (error) {
        return {
            reachable: false,
            startupWizardCompleted: null,
            systemId: null,
            version: null,
            error: error instanceof Error ? error.message : String(error)
        };
    }
}

function sleep(ms) {
    return new Promise(resolve => setTimeout(resolve, ms));
}

async function stopTrackedStageProcess() {
    let pidContent;
    try {
        pidContent = await readFile(stagePidFile, 'utf8');
    } catch (error) {
        if (error?.code === 'ENOENT') {
            return;
        }

        throw error;
    }

    const pid = parseStagePid(pidContent);
    if (!pid) {
        throw new Error(`Invalid stage pid file: ${stagePidFile}`);
    }

    if (process.platform === 'win32') {
        const result = spawnSync(taskkillExecutable, buildStageKillArgs(pid), {
            windowsHide: true,
            stdio: 'ignore'
        });
        if (result.error) {
            throw result.error;
        }
    } else {
        try {
            // The child was started detached, so its process group belongs to this stage.
            process.kill(-pid, 'SIGTERM');
        } catch (error) {
            if (error?.code !== 'ESRCH') {
                throw error;
            }
        }
    }
    await rm(stagePidFile, { force: true });
    ownsStageProcess = false;
}

async function removeTargets(targets) {
    for (const target of targets) {
        await rm(target, { recursive: true, force: true });
    }
}

async function resetStageWorkspace() {
    console.error('[playwright] resetting isolated stage workspace');
    assertStagePidFileAbsent(existsSync(stagePidFile));
    assertSafeRemovalTargets(stageRemovalTargets, allowedStageRoots);
    await removeTargets(stageRemovalTargets);
    await mkdir(stageDataDir, { recursive: true });
    if (process.platform === 'win32') {
        const systemRoot = process.env.SystemRoot || 'C:\\Windows';
        const whoami = spawnSync(path.join(systemRoot, 'System32', 'whoami.exe'), [ '/user', '/fo', 'csv', '/nh' ], {
            encoding: 'utf8',
            windowsHide: true,
            stdio: [ 'ignore', 'pipe', 'pipe' ]
        });
        if (whoami.error || whoami.status !== 0) {
            throw whoami.error || new Error(`Could not identify the Playwright stage user: ${whoami.stderr || whoami.status}`);
        }

        const userSid = parseWindowsUserSid(whoami.stdout);
        if (!userSid) {
            throw new Error('Could not parse the Windows SID for the Playwright stage user.');
        }

        const icacls = spawnSync(path.join(systemRoot, 'System32', 'icacls.exe'), buildRestrictedDirectoryAclArgs(stageDataDir, userSid), {
            encoding: 'utf8',
            windowsHide: true,
            stdio: [ 'ignore', 'pipe', 'pipe' ]
        });
        if (icacls.error || icacls.status !== 0) {
            throw icacls.error || new Error(`Could not restrict the Playwright stage data ACL: ${icacls.stderr || icacls.stdout || icacls.status}`);
        }
    } else {
        await chmod(stageDataDir, 0o700);
    }
}

async function startStageAndWaitClean(baseUrl, databasePort) {
    console.error('[playwright] starting clean stage');
    const child = spawn(dotnetExecutable, [ stageDll, `--datadir=${stageDataDir}` ], {
        windowsHide: true,
        env: {
            ...process.env,
            // Keep EF data out of the developer's production database.
            MulletaFlix_DATABASE_NAME: 'mulletaflix_e2e',
            // Start the bundled MariaDB under this stage's own data directory,
            // never against the installed server's default port 3306.
            MULLETAFLIX_DB_PORT: String(databasePort),
            // Keep the startup smoke test hermetic: no online avatar catalog,
            // Telegram, FTP or other external bootstrap side effects.
            MFLX_DISABLE_EXTERNAL_BOOTSTRAP: 'true',
            // The wizard legitimately performs more than the production
            // anonymous burst threshold while bootstrapping its first page.
            MFLX_E2E_TEST_MODE: 'true',
            // Never let an isolated browser run query GitHub or download an
            // update in the background. The loopback sink fails closed.
            MulletaFlix_UPDATE_MANIFEST_URL: 'http://127.0.0.1:1/e2e-disabled'
        },
        stdio: [ 'ignore', 'inherit', 'inherit' ],
        detached: true
    });

    if (!child.pid) {
        throw new Error('Stage process did not return a process id.');
    }

    await writeFile(stagePidFile, String(child.pid), 'utf8');
    ownsStageProcess = true;
    let exitState = null;
    child.once('exit', (code, signal) => {
        exitState = { code, signal };
    });
    child.unref();

    for (let attempt = 1; attempt <= 120; attempt++) {
        if (exitState) {
            throw new Error(`Stage process exited before readiness (code=${String(exitState.code)}, signal=${String(exitState.signal)}).`);
        }

        const stageProbe = await probeStage(baseUrl);
        if (stageProbe.reachable) {
            if (stageProbe.startupWizardCompleted !== false) {
                throw new Error(`Stage is not clean after reset. StartupWizardCompleted=${String(stageProbe.startupWizardCompleted)}`);
            }

            try {
                // The temporary startup host serves /System/Info/Public before
                // the application API is ready. Do not let browser tests race
                // that host and fail their first authenticated/setup request.
                const response = await fetch(`${baseUrl}/System/Info`, {
                    cache: 'no-cache',
                    signal: AbortSignal.timeout(1000)
                });
                if (response.ok) {
                    console.error('[playwright] stage is clean and the application API is ready');
                    return stageProbe;
                }
            } catch {
                // The primary API is not ready yet; keep polling the owned stage.
            }
        }

        await sleep(2000);
    }

    throw new Error('Stage public info not available after clean start.');
}

function createSummaryBase(baseUrl, specCount) {
    return {
        title: 'MulletaFlix Playwright Summary',
        generatedAt: new Date().toISOString(),
        baseUrl,
        status: 'no-specs',
        specCount,
        tests: {
            total: 0,
            passed: 0,
            failed: 0,
            flaky: 0,
            skipped: 0,
            durationMs: 0
        },
        stageProbe: {
            reachable: false,
            startupWizardCompleted: null,
            systemId: null,
            error: null
        },
        stageChecks: {
            wizard: false,
            admin: false,
            user: false,
            login: false
        },
        assumptions: []
    };
}

async function loadRawReport(rawReportPath) {
    if (!existsSync(rawReportPath)) {
        return null;
    }

    return JSON.parse(await readFile(rawReportPath, 'utf8'));
}

async function writeNoSpecsSummary(args, specCount, initialStageProbe) {
    const stageProbe = initialStageProbe || await probeStage(args.baseUrl || DEFAULT_STAGE_BASE_URL);
    const summary = createSummaryBase(args.baseUrl, specCount);
    summary.stageProbe = stageProbe;
    summary.status = stageProbe.reachable ? 'no-specs' : 'probe-failed';
    summary.stageChecks.wizard = stageProbe.reachable && stageProbe.startupWizardCompleted === false;
    summary.assumptions = [
        'A suíte usa somente loopback e portas isoladas 18096-18196; a porta 8096 não é permitida para stage.',
        'Nenhum spec Playwright foi encontrado no diretório configurado.',
        stageProbe.reachable ?
            'O stage respondeu ao probe do endpoint público.' :
            `Probe do stage falhou: ${stageProbe.error || 'erro desconhecido'}`,
        stageProbe.reachable ?
            `StartupWizardCompleted=${String(stageProbe.startupWizardCompleted)}` :
            'Não foi possível confirmar o estado limpo do stage.'
    ];

    const { jsonPath, markdownPath } = await writePlaywrightReportArtifacts(summary, args.reportDir);
    console.log(`Relatório Playwright salvo em:\n- ${jsonPath}\n- ${markdownPath}`);
    process.exitCode = stageProbe.reachable ? 0 : 1;
}

async function prepareStage(args) {
    if (!args.resetStage) {
        return probeStage(args.baseUrl);
    }

    await resetStageWorkspace();
    const mediaRoot = path.join(stageDataDir, 'playwright-media');
    const sampleVideoPath = path.join(workspaceRoot, 'MulletaFlix-master', 'Jellyfin.Server', 'media', 'mulletaflix_intro.mp4');
    await seedPlaywrightMediaFixtures(mediaRoot, sampleVideoPath, {
        ffmpegPath: process.env.PW_FFMPEG_PATH || 'ffmpeg',
        realStrmFilePath: process.env.PW_REAL_STRM_FILE
    });
    process.env.PW_MEDIA_LIBRARY_ROOT = mediaRoot;
    args.baseUrl = await selectStageBaseUrl(args.baseUrl || DEFAULT_STAGE_BASE_URL);
    args.databasePort = await selectStageDatabasePort();
    await writeStageNetworkConfiguration(args.baseUrl);
    return startStageAndWaitClean(args.baseUrl, args.databasePort);
}

function buildPlaywrightArgs(args, config, specs) {
    const playwrightArgs = [ playwrightCli, 'test', ...specs.map(spec => path.relative(repoRoot, spec).replaceAll(path.sep, '/')), '--config', config ];
    if (args.grep) {
        playwrightArgs.push('--pass-with-no-tests');
    }
    if (args.grep) {
        playwrightArgs.push('--grep', args.grep);
    }

    return playwrightArgs;
}

async function main() {
    const args = parseArgs(process.argv.slice(2));
    failureReportDir = args.reportDir;
    const stageRawReportPath = path.join(args.reportDir, 'raw-stage-results.json');
    const fixtureRawReportPath = path.join(args.reportDir, 'raw-vite-fixture-results.json');
    await Promise.all([
        rm(stageRawReportPath, { force: true }),
        rm(fixtureRawReportPath, { force: true })
    ]);
    const specPatterns = [ '**/*.spec.ts', '**/*.spec.js', '**/*.spec.mjs' ];
    const specs = await fg(specPatterns, {
        cwd: args.specDir,
        absolute: true,
        onlyFiles: true
    });
    const fixtureSpecs = specs.filter(spec => viteFixtureSpecNames.has(path.basename(spec)));
    const stageSpecs = specs.filter(spec => !viteFixtureSpecNames.has(path.basename(spec)));

    const initialStageProbe = await prepareStage(args);

    if (!specs.length) {
        await writeNoSpecsSummary(args, specs.length, initialStageProbe);
        return;
    }

    const versionSource = await readFile(path.join(workspaceRoot, 'MulletaFlix-master', 'SharedVersion.cs'), 'utf8');
    const expectedVersion = versionSource.match(/AssemblyVersion\("([^"]+)"\)/)?.[1];
    assertStageVersion(expectedVersion, initialStageProbe?.version);

    const env = {
        ...process.env,
        PW_BASE_URL: args.baseUrl,
        PW_STAGE_CLIENT_INDEX: `${args.baseUrl}/web/index.html`,
        PW_JSON_REPORT: stageRawReportPath,
        // The runner always resets a disposable stage. Keep the wizard
        // executable out of the box while allowing CI/users to override the
        // credentials explicitly when exercising an existing account flow.
        MFLX_ADMIN_USER: process.env.MFLX_ADMIN_USER || 'mflx-admin-e2e',
        MFLX_ADMIN_PASSWORD: process.env.MFLX_ADMIN_PASSWORD || crypto.randomBytes(24).toString('base64url'),
        PW_STAGE_DLL: stageDll,
        PW_STAGE_DATA_DIR: stageDataDir,
        PW_STAGE_PID_FILE: stagePidFile,
        PW_STAGE_DOTNET: dotnetExecutable,
        PW_STAGE_DB_PORT: String(args.databasePort)
    };

    const stageResult = stageSpecs.length ? spawnSync(process.execPath, buildPlaywrightArgs(args, configPath, stageSpecs), {
        cwd: repoRoot,
        env,
        stdio: 'inherit'
    }) : null;
    if (stageResult) {
        console.error(`[playwright:stage] exit code: ${String(stageResult.status)} signal=${String(stageResult.signal || '')} error=${String(stageResult.error?.message || '')}`);
    }

    let fixtureResult = null;
    let fixtureBaseUrl = null;
    if (fixtureSpecs.length) {
        const fixturePort = await selectFixturePort();
        fixtureBaseUrl = `http://127.0.0.1:${fixturePort}`;
        fixtureResult = spawnSync(process.execPath, buildPlaywrightArgs(args, fixtureConfigPath, fixtureSpecs), {
            cwd: repoRoot,
            env: {
                ...process.env,
                PW_FIXTURE_PORT: String(fixturePort),
                PW_JSON_REPORT: fixtureRawReportPath
            },
            stdio: 'inherit'
        });
        console.error(`[playwright:vite-fixtures] exit code: ${String(fixtureResult.status)} signal=${String(fixtureResult.signal || '')} error=${String(fixtureResult.error?.message || '')}`);
    }

    const stageRawReport = await loadRawReport(stageRawReportPath);
    const fixtureRawReport = await loadRawReport(fixtureRawReportPath);
    const stageProbe = await probeStage(args.baseUrl);
    const getSuiteResult = (result, rawReport, suiteSpecs) => {
        if (!suiteSpecs.length) {
            return { status: 'not-run', exitCode: 0, specCount: 0, tests: { total: 0, passed: 0, failed: 0, flaky: 0, skipped: 0, durationMs: 0 } };
        }

        const exitCode = result?.status;
        const tests = rawReport ? extractPlaywrightStats(rawReport) : {
            total: 0,
            passed: 0,
            failed: exitCode === 0 ? 0 : 1,
            flaky: 0,
            skipped: 0,
            durationMs: 0
        };
        if (args.grep && exitCode === 0 && rawReport && tests.total === 0) {
            return { status: 'filtered-out', exitCode: 0, specCount: suiteSpecs.length, tests };
        }
        const gate = evaluatePlaywrightGate(exitCode, tests, !!rawReport);
        return { status: gate.status, exitCode: gate.exitCode, specCount: suiteSpecs.length, tests };
    };
    const stageSuite = getSuiteResult(stageResult, stageRawReport, stageSpecs);
    const fixtureSuite = getSuiteResult(fixtureResult, fixtureRawReport, fixtureSpecs);
    const suiteStats = [ stageSuite.tests, fixtureSuite.tests ];
    const stats = Object.fromEntries([ 'total', 'passed', 'failed', 'flaky', 'skipped', 'durationMs' ].map(key => [
        key,
        suiteStats.reduce((total, suite) => total + suite[key], 0)
    ]));
    const exitCode = Math.max(stageSuite.exitCode, fixtureSuite.exitCode);
    const gate = evaluatePlaywrightGate(exitCode, stats, (!stageSpecs.length || !!stageRawReport) && (!fixtureSpecs.length || !!fixtureRawReport));

    const summary = {
        title: 'MulletaFlix Playwright Summary',
        generatedAt: new Date().toISOString(),
        baseUrl: args.baseUrl,
        status: gate.status,
        specCount: specs.length,
        tests: stats,
        suites: {
            stage: { ...stageSuite, baseUrl: stageSpecs.length ? args.baseUrl : null },
            viteFixtures: { ...fixtureSuite, baseUrl: fixtureBaseUrl }
        },
        stageProbe,
        stageChecks: {
            wizard: stageProbe.reachable && stageProbe.startupWizardCompleted === false,
            admin: stageProbe.reachable && stageProbe.startupWizardCompleted === true,
            user: stageProbe.reachable && stageProbe.startupWizardCompleted === true,
            login: stageProbe.reachable && stageProbe.startupWizardCompleted === true
        },
        assumptions: [
            'Os specs devem continuar separados do código de apoio em tests/playwright/specs.',
            'O runner nunca acessa nem inicia stage na porta de produção 8096; usa portas isoladas 18096-18196.',
            initialStageProbe ?
                `Stage inicial limpo confirmado com StartupWizardCompleted=${String(initialStageProbe.startupWizardCompleted)}` :
                'Reset limpo do stage foi desabilitado para esta execuÃ§Ã£o.',
            `StartupWizardCompleted=${String(stageProbe.startupWizardCompleted)}`,
            'Specs que importam módulos React/TypeScript por /@fs ou imports Vite executam em uma instância Vite isolada, separada do stage .NET.',
            stageRawReport ?
                'Relatório JSON da suíte .NET gerado e consolidado.' :
                'A suíte .NET não gerou relatório JSON válido.',
            fixtureSpecs.length === 0 ?
                'Nenhum spec Vite-only foi encontrado.' :
                fixtureRawReport ?
                    'Relatório JSON da suíte Vite gerado e consolidado.' :
                    'A suíte Vite não gerou relatório JSON válido.'
        ]
    };

    const { jsonPath, markdownPath } = await writePlaywrightReportArtifacts(summary, args.reportDir);
    console.log(`Relatório Playwright salvo em:\n- ${jsonPath}\n- ${markdownPath}`);

    if (ownsStageProcess) {
        await stopTrackedStageProcess();
    }
    process.exitCode = gate.exitCode;
}

main().catch(async error => {
    if (ownsStageProcess) {
        await stopTrackedStageProcess().catch(cleanupError => {
            console.error('[playwright] failed to stop owned stage process', cleanupError);
        });
    }

    const summary = {
        title: 'MulletaFlix Playwright Summary',
        generatedAt: new Date().toISOString(),
        baseUrl: getStageBaseUrl(),
        status: 'failed',
        specCount: 0,
        tests: {
            total: 0,
            passed: 0,
            failed: 1,
            flaky: 0,
            skipped: 0,
            durationMs: 0
        },
        stageProbe: {
            reachable: false,
            startupWizardCompleted: null,
            systemId: null,
            error: error instanceof Error ? error.message : String(error)
        },
        stageChecks: {
            wizard: false,
            admin: false,
            user: false,
            login: false
        },
        assumptions: [
            `Runner failure: ${error instanceof Error ? error.message : String(error)}`
        ]
    };

    await writePlaywrightReportArtifacts(summary, failureReportDir);
    console.error(error);
    process.exitCode = 1;
});
