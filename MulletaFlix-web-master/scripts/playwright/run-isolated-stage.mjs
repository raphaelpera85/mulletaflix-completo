#!/usr/bin/env node
import { spawn } from 'node:child_process';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const tempBase = path.join(process.env.USERPROFILE || 'C:\\Users\\Raphael', '.mulletaflix-tmp');

process.env.TEMP = tempBase;
process.env.TMP = tempBase;

const stageDirName = `mflx-playwright-stage-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
const stageDir = path.join(tempBase, stageDirName);
process.env.PW_ISOLATED_STAGE_DATA_DIR = stageDir;

const runSuiteScript = path.join(__dirname, 'run-suite.mjs');
const args = process.argv.slice(2);

const child = spawn(process.execPath, [runSuiteScript, ...args], {
    stdio: 'inherit',
    env: process.env,
    cwd: path.resolve(__dirname, '..', '..')
});

child.on('exit', (code, signal) => {
    if (signal) {
        process.kill(process.pid, signal);
    } else {
        process.exit(code ?? 0);
    }
});
