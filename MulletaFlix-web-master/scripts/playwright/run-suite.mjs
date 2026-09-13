#!/usr/bin/env node
import { existsSync } from 'node:fs';
import { mkdir, readFile, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawn, spawnSync } from 'node:child_process';
import crypto from 'node:crypto';
import net from 'node:net';
import fg from 'fast-glob';
import {
    DEFAULT_STAGE_BASE_URL,
    fetchStagePublicInfo,
    getStageBaseUrl,
    writePlaywrightReportArtifacts
} from '../../tests/playwright/support/index.mjs';
import {
    assertSafeRemovalTargets,
    assertStagePidFileAbsent,
    buildStageKillArgs,
    buildStageRemovalTargets,
    parseStagePid
} from './stage-safety.mjs';

const scriptDir = path.dirname(fileURLToPath(import.meta.url));
const repoRoot = path.resolve(scriptDir, '..', '..');
const reportDir = path.join(repoRoot, 'reports', 'playwright');
const configPath = path.join(repoRoot, 'playwright.stage.config.ts');
const specDir = path.join(repoRoot, 'tests', 'playwright', 'specs');
const playwrightCli = path.join(repoRoot, 'node_modules', '@playwright', 'test', 'cli.js');
const workspaceRoot = path.resolve(repoRoot, '..');
const stageDll = path.join(workspaceRoot, 'stage', 'MulletaFlix.dll');
const stageDataDir = path.join(workspaceRoot, 'stage-data');
const legacyStageDataDir = path.join(workspaceRoot, 'stage', 'data', 'jellyfin-test');
const stagePidFile = path.join(stageDataDir, 'stage.pid');
const taskkillExecutable = path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'taskkill.exe');
const dotnetExecutable = path.join(process.env.ProgramFiles || 'C:\\Program Files', 'dotnet', 'dotnet.exe');
const allowedStageRoots = [
    path.join(workspaceRoot, 'stage'),
    stageDataDir
];
const stageRemovalTargets = buildStageRemovalTargets({
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
    const nebulaConfig = '<NebulaFtpConfiguration>\n  <Enabled>false</Enabled>\n  <MonitorPaths />\n  <StagePaths />\n  <UseMappedDrive>false</UseMappedDrive>\n  <DeleteSourceAfterUpload>false</DeleteSourceAfterUpload>\n  <BotTokens />\n  <ApiHash />\n  <SupabaseKey />\n</NebulaFtpConfiguration>\n';
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

    return args;
}

async function probeStage(baseUrl) {
    try {
        const info = await fetchStagePublicInfo(baseUrl);
        return {
            reachable: true,
            startupWizardCompleted: Boolean(info?.StartupWizardCompleted),
            systemId: info?.Id || null,
            error: null
        };
    } catch (error) {
        return {
            reachable: false,
            startupWizardCompleted: null,
            systemId: null,
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

    spawnSync(taskkillExecutable, buildStageKillArgs(pid), {
        windowsHide: true,
        stdio: 'ignore'
    });
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
}

async function startStageAndWaitClean(baseUrl) {
    console.error('[playwright] starting clean stage');
    const child = spawn(dotnetExecutable, [ stageDll, `--datadir=${stageDataDir}` ], {
        windowsHide: true,
        env: {
            ...process.env,
            // Keep EF data out of the developer's production database.
            MulletaFlix_DATABASE_NAME: 'mulletaflix_e2e',
            // Keep the startup smoke test hermetic: no online avatar catalog,
            // Telegram, FTP or other external bootstrap side effects.
            MFLX_DISABLE_EXTERNAL_BOOTSTRAP: 'true',
            // The wizard legitimately performs more than the production
            // anonymous burst threshold while bootstrapping its first page.
            MFLX_E2E_TEST_MODE: 'true'
        },
        stdio: [ 'ignore', 'inherit', 'inherit' ],
        detached: true
    });

    if (!child.pid) {
        throw new Error('Stage process did not return a process id.');
    }

    await writeFile(stagePidFile, String(child.pid), 'utf8');
    ownsStageProcess = true;
    child.unref();

    for (let attempt = 1; attempt <= 120; attempt++) {
        const stageProbe = await probeStage(baseUrl);
        if (stageProbe.reachable) {
            if (stageProbe.startupWizardCompleted !== false) {
                throw new Error(`Stage is not clean after reset. StartupWizardCompleted=${String(stageProbe.startupWizardCompleted)}`);
            }

            console.error('[playwright] stage is clean');
            await sleep(10000);
            return stageProbe;
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

function extractStats(rawReport) {
    const stats = rawReport?.stats || {};
    return {
        total: Number(stats.expected || 0) + Number(stats.unexpected || 0) + Number(stats.flaky || 0) + Number(stats.skipped || 0),
        passed: Number(stats.expected || 0),
        failed: Number(stats.unexpected || 0),
        skipped: Number(stats.skipped || 0),
        durationMs: Number(stats.duration || 0)
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
        'A suíte deve continuar mirando o stage limpo em http://127.0.0.1:8096 por padrão.',
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
        return null;
    }

    await resetStageWorkspace();
    args.baseUrl = await selectStageBaseUrl(args.baseUrl || DEFAULT_STAGE_BASE_URL);
    await writeStageNetworkConfiguration(args.baseUrl);
    return startStageAndWaitClean(args.baseUrl);
}

function buildPlaywrightArgs(args) {
    const playwrightArgs = [ playwrightCli, 'test', '--config', configPath ];
    if (args.grep) {
        playwrightArgs.push('--grep', args.grep);
    }

    return playwrightArgs;
}

async function main() {
    const args = parseArgs(process.argv.slice(2));
    const rawReportPath = path.join(args.reportDir, 'raw-results.json');
    await rm(rawReportPath, { force: true });
    const specPatterns = [ '**/*.spec.ts', '**/*.spec.js', '**/*.spec.mjs' ];
    const specs = await fg(specPatterns, {
        cwd: args.specDir,
        absolute: true,
        onlyFiles: true
    });

    const initialStageProbe = await prepareStage(args);

    if (!specs.length) {
        await writeNoSpecsSummary(args, specs.length, initialStageProbe);
        return;
    }

    const env = {
        ...process.env,
        PW_BASE_URL: args.baseUrl,
        PW_STAGE_CLIENT_INDEX: `${args.baseUrl}/web/index.html`,
        PW_JSON_REPORT: rawReportPath,
        // The runner always resets a disposable stage. Keep the wizard
        // executable out of the box while allowing CI/users to override the
        // credentials explicitly when exercising an existing account flow.
        MFLX_ADMIN_USER: process.env.MFLX_ADMIN_USER || 'mflx-admin-e2e',
        MFLX_ADMIN_PASSWORD: process.env.MFLX_ADMIN_PASSWORD || crypto.randomBytes(24).toString('base64url')
    };

    const result = spawnSync(process.execPath, buildPlaywrightArgs(args), {
        cwd: repoRoot,
        env,
        stdio: 'inherit'
    });
    console.error(`[playwright] exit code: ${String(result.status)} signal=${String(result.signal || '')} error=${String(result.error?.message || '')}`);

    const rawReport = await loadRawReport(rawReportPath);
    const stageProbe = await probeStage(args.baseUrl);

    const summary = {
        title: 'MulletaFlix Playwright Summary',
        generatedAt: new Date().toISOString(),
        baseUrl: args.baseUrl,
        status: result.status === 0 ? 'success' : 'failed',
        specCount: specs.length,
        tests: rawReport ? extractStats(rawReport) : {
            total: 0,
            passed: 0,
            failed: result.status === 0 ? 0 : 1,
            skipped: 0,
            durationMs: 0
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
            'O runner escolhe uma porta local isolada quando 127.0.0.1:8096 já está ocupada.',
            initialStageProbe ?
                `Stage inicial limpo confirmado com StartupWizardCompleted=${String(initialStageProbe.startupWizardCompleted)}` :
                'Reset limpo do stage foi desabilitado para esta execuÃ§Ã£o.',
            `StartupWizardCompleted=${String(stageProbe.startupWizardCompleted)}`,
            rawReport ?
                'O relatório JSON cru do Playwright foi gerado e consolidado pelo runner.' :
                'O relatório JSON cru do Playwright não foi encontrado; o resumo foi derivado do exit code.'
        ]
    };

    const { jsonPath, markdownPath } = await writePlaywrightReportArtifacts(summary, args.reportDir);
    console.log(`Relatório Playwright salvo em:\n- ${jsonPath}\n- ${markdownPath}`);

    if (ownsStageProcess) {
        await stopTrackedStageProcess();
    }
    process.exitCode = result.status === 0 && summary.tests.failed === 0 ? 0 : 1;
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

    await writePlaywrightReportArtifacts(summary, reportDir);
    console.error(error);
    process.exitCode = 1;
});
