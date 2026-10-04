import { execFileSync, spawn } from 'node:child_process';
import { readFile, rm, writeFile } from 'node:fs/promises';
import net from 'node:net';
import path from 'node:path';
import { getStageBaseUrl } from './stage.mjs';

function readStageConfiguration() {
    const stageDll = process.env.PW_STAGE_DLL;
    const stageDataDir = process.env.PW_STAGE_DATA_DIR;
    const stagePidFile = process.env.PW_STAGE_PID_FILE;
    const dotnet = process.env.PW_STAGE_DOTNET;
    const databasePort = Number(process.env.PW_STAGE_DB_PORT);
    if (!stageDll || !stageDataDir || !stagePidFile || !dotnet || !Number.isInteger(databasePort)) {
        throw new Error('Playlist restart verification requires an owned Playwright stage from run-suite.mjs.');
    }

    return {
        stageDll: path.resolve(stageDll),
        stageDataDir: path.resolve(stageDataDir),
        stagePidFile: path.resolve(stagePidFile),
        dotnet: path.resolve(dotnet),
        databasePort
    };
}

async function waitForPort(port, shouldBeAvailable) {
    for (let attempt = 0; attempt < 80; attempt++) {
        const available = await new Promise(resolve => {
            const probe = net.createServer();
            probe.once('error', () => resolve(false));
            probe.once('listening', () => probe.close(() => resolve(true)));
            probe.listen(port, '127.0.0.1');
        });
        if (available === shouldBeAvailable) {
            return;
        }

        await new Promise(resolve => setTimeout(resolve, 250));
    }

    throw new Error(`Owned Playwright stage did not ${shouldBeAvailable ? 'release' : 'bind'} port ${port}.`);
}

function assertOwnedStageProcess(pid, config) {
    if (process.platform === 'win32') {
        const script = `$p=Get-CimInstance Win32_Process -Filter 'ProcessId=${pid}'; if (-not $p) { exit 3 }; $p | Select-Object ExecutablePath,CommandLine | ConvertTo-Json -Compress`;
        const powershell = 'C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe';
        const processInfo = JSON.parse(execFileSync(powershell, [ '-NoProfile', '-NonInteractive', '-Command', script ], { encoding: 'utf8' }));
        if (path.resolve(processInfo.ExecutablePath).toLowerCase() !== config.dotnet.toLowerCase()
            || !processInfo.CommandLine.toLowerCase().includes(config.stageDll.toLowerCase())
            || !processInfo.CommandLine.toLowerCase().includes(`--datadir=${config.stageDataDir}`.toLowerCase())) {
            throw new Error('Refusing to restart a process that does not match the owned Playwright stage paths.');
        }
        return;
    }

    const ps = process.platform === 'darwin' ? '/bin/ps' : '/usr/bin/ps';
    const commandLine = execFileSync(ps, [ '-ww', '-p', String(pid), '-o', 'args=' ], { encoding: 'utf8' }).trim();
    if (!commandLine.includes(config.stageDll) || !commandLine.includes(`--datadir=${config.stageDataDir}`)) {
        throw new Error('Refusing to restart a process that does not match the owned Playwright stage paths.');
    }
}

/** Restarts only the isolated server process started by run-suite.mjs; its stage data and MariaDB port remain unchanged. */
export async function restartOwnedStage() {
    const config = readStageConfiguration();
    const pid = Number.parseInt((await readFile(config.stagePidFile, 'utf8')).trim(), 10);
    if (!Number.isSafeInteger(pid) || pid < 1) {
        throw new Error('The owned Playwright stage PID file is invalid.');
    }

    assertOwnedStageProcess(pid, config);
    if (process.platform === 'win32') {
        execFileSync(path.join(process.env.SystemRoot || 'C:\\Windows', 'System32', 'taskkill.exe'), [ '/PID', String(pid), '/T', '/F' ], { stdio: 'ignore' });
    } else {
        process.kill(-pid, 'SIGTERM');
    }

    const stageUrl = new URL(getStageBaseUrl());
    const port = Number(stageUrl.port || 80);
    await waitForPort(port, true);
    await rm(config.stagePidFile);

    const child = spawn(config.dotnet, [ config.stageDll, `--datadir=${config.stageDataDir}` ], {
        detached: true,
        windowsHide: true,
        stdio: 'inherit',
        env: {
            ...process.env,
            MulletaFlix_DATABASE_NAME: 'mulletaflix_e2e',
            MULLETAFLIX_DB_PORT: String(config.databasePort),
            MFLX_DISABLE_EXTERNAL_BOOTSTRAP: 'true',
            MFLX_E2E_TEST_MODE: 'true'
        }
    });
    if (!child.pid) {
        throw new Error('Restarted Playwright stage did not return a process id.');
    }

    // Update ownership immediately so runner cleanup still targets this child if readiness fails.
    await writeFile(config.stagePidFile, String(child.pid), 'utf8');
    child.unref();
    await waitForPort(port, false);

    for (let attempt = 0; attempt < 120; attempt++) {
        try {
            const response = await fetch(new URL('/System/Info/Public', stageUrl), { signal: AbortSignal.timeout(1000) });
            if (response.ok) {
                return;
            }
        } catch {
            // The stage is expected to be unavailable while its host and isolated DB start.
        }

        await new Promise(resolve => setTimeout(resolve, 500));
    }

    throw new Error('Owned Playwright stage did not become ready after restart.');
}
