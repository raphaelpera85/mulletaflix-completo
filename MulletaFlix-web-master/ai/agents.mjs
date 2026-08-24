/**
 * MulletaFlix Web — Orquestrador de Agentes Especialistas
 *
 * Uso:
 *   node ai/agents.mjs list                      # lista agentes
 *   node ai/agents.mjs prompt <agente>           # imprime o prompt do agente (para usar via Hermes)
 *   node ai/agents.mjs check                     # roda build:check + testes
 *   node ai/agents.mjs map-plugins               # imprime o mapa de plugins
 *
 * IMPORTANTE: os agentes são EXECUTADOS pelo Hermes (delegate_task), que usa o
 * modelo ativo da sessão — NÃO via LM Studio. Este script serve para listar,
 * inspecionar prompts, validar o projeto e mapear plugins.
 */

import { readFileSync, existsSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const PROMPTS_DIR = join(__dirname, 'prompts');

const AGENTS = [
    'supervisor',
    'backend',
    'frontend',
    'database',
    'plugins',
    'security',
    'performance',
    'deploy-review'
];

function listAgents() {
    console.log('Agentes especialistas disponíveis:');
    for (const agent of AGENTS) {
        const promptFile = join(PROMPTS_DIR, `${agent}.md`);
        const size = existsSync(promptFile) ? `${(readFileSync(promptFile).length / 1024).toFixed(1)} KB` : 'FALTANDO';
        console.log(`  - ${agent.padEnd(14)} ${size}`);
    }
}

function showPrompt(agent) {
    if (!AGENTS.includes(agent)) {
        console.error(`Agente desconhecido: ${agent}. Use: node ai/agents.mjs list`);
        process.exit(1);
    }

    const promptFile = join(PROMPTS_DIR, `${agent}.md`);
    if (!existsSync(promptFile)) {
        console.error(`Prompt não encontrado: ${promptFile}`);
        process.exit(1);
    }

    console.log(readFileSync(promptFile, 'utf8'));
}

function runCheck() {
    const checks = [
        ['build:check', 'npm run build:check'],
        ['test', 'npm test']
    ];
    let failed = false;
    for (const [name, cmd] of checks) {
        console.log(`▶ ${name}`);
        const result = spawnSync(cmd.split(' ')[0], cmd.split(' ').slice(1), {
            cwd: join(__dirname, '..'),
            shell: process.platform === 'win32',
            stdio: 'inherit'
        });
        if (result.status !== 0) {
            console.error(`✗ ${name} FALHOU (exit ${result.status})`);
            failed = true;
        } else {
            console.log(`✓ ${name} OK`);
        }
    }
    process.exit(failed ? 1 : 0);
}

function mapPlugins() {
    const mapFile = join(__dirname, 'plugins-map.md');
    if (existsSync(mapFile)) {
        console.log(readFileSync(mapFile, 'utf8'));
    } else {
        console.error('plugins-map.md não encontrado');
        process.exit(1);
    }
}

const [,, cmd, arg1, arg2] = process.argv;

switch (cmd) {
    case 'list':
        listAgents();
        break;
    case 'prompt':
        showPrompt(arg1);
        break;
    case 'check':
        runCheck();
        break;
    case 'map-plugins':
        mapPlugins();
        break;
    default:
        console.log(`Uso: node ai/agents.mjs {list|prompt <agente>|check|map-plugins}`);
        console.log(`Agentes: ${AGENTS.join(', ')}`);
        console.log('');
        console.log('Para EXECUTAR um agente, peça ao Hermes: "chame o agente <nome> para <tarefa>" —');
        console.log('ele usará o modelo ativo da sessão via delegate_task.');
        process.exit(1);
}
