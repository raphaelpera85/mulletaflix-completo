import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';

function formatDate(value) {
    if (!value) {
        return 'n/a';
    }

    return new Date(value).toISOString();
}

function formatWizardStatus(value) {
    if (typeof value !== 'boolean') {
        return 'n/a';
    }

    return value ? 'true' : 'false';
}

export function extractPlaywrightStats(rawReport) {
    const stats = rawReport?.stats || {};
    return {
        total: Number(stats.expected || 0) + Number(stats.unexpected || 0) + Number(stats.flaky || 0) + Number(stats.skipped || 0),
        passed: Number(stats.expected || 0),
        failed: Number(stats.unexpected || 0),
        flaky: Number(stats.flaky || 0),
        skipped: Number(stats.skipped || 0),
        durationMs: Number(stats.duration || 0)
    };
}

export function evaluatePlaywrightGate(processExitCode, stats, hasRawReport) {
    if (processExitCode !== 0 || stats.failed > 0) {
        return { status: 'failed', exitCode: 1 };
    }

    if (!hasRawReport) {
        return { status: 'unverified', exitCode: 1 };
    }

    if (stats.flaky > 0) {
        return { status: 'flaky', exitCode: 1 };
    }

    if (stats.passed === 0) {
        return { status: 'no-tests', exitCode: 1 };
    }

    return { status: 'success', exitCode: 0 };
}

function getConclusion(summary) {
    if (summary.status === 'no-specs') {
        return 'Infraestrutura pronta, mas ainda não há specs Playwright no diretório configurado.';
    }

    if (summary.status === 'probe-failed') {
        return 'O stage não respondeu ao probe básico. Verifique se o servidor limpo está no ar.';
    }

    if (summary.status === 'failed' || summary.tests.failed > 0) {
        return 'A suíte falhou. Corrija os pontos listados no relatório antes de expandir a cobertura.';
    }

    if (summary.tests.flaky > 0) {
        return 'A suíte teve testes instáveis que passaram apenas no retry. Analise trace e screenshot antes de aprovar.';
    }

    if (summary.status === 'unverified') {
        return 'O Playwright terminou sem relatório JSON. A execução não pode ser considerada aprovada.';
    }

    if (summary.status === 'no-tests' || summary.tests.passed === 0) {
        return 'Nenhum teste foi executado. Verifique o diretório de specs configurado.';
    }

    return 'A suíte terminou sem falhas inesperadas e o fluxo base está pronto para expansão.';
}

export function buildPlaywrightReport(summary) {
    const conclusion = getConclusion(summary);

    return {
        ...summary,
        conclusion
    };
}

export function buildMarkdownReport(summary) {
    const report = buildPlaywrightReport(summary);

    return [
        `# ${report.title}`,
        '',
        `- Gerado em: ${formatDate(report.generatedAt)}`,
        `- Base URL: ${report.baseUrl}`,
        `- Status: ${report.status}`,
        `- Especificações encontradas: ${report.specCount}`,
        `- Total de testes: ${report.tests.total}`,
        `- Passou: ${report.tests.passed}`,
        `- Falhou: ${report.tests.failed}`,
        `- Instáveis: ${report.tests.flaky}`,
        `- Ignorados: ${report.tests.skipped}`,
        `- Duração: ${report.tests.durationMs} ms`,
        '',
        '## Probe do Stage',
        '',
        `- Acessível: ${report.stageProbe.reachable ? 'sim' : 'não'}`,
        `- StartupWizardCompleted: ${formatWizardStatus(report.stageProbe.startupWizardCompleted)}`,
        report.stageProbe.error ? `- Erro: ${report.stageProbe.error}` : null,
        '',
        '## Conclusão',
        '',
        report.conclusion,
        '',
        '## Premissas',
        '',
        ...report.assumptions.map(item => `- ${item}`),
        '',
        '## Verificações de estado',
        '',
        `- Wizard limpo: ${report.stageChecks.wizard ? 'sim' : 'não'}`,
        `- Admin: ${report.stageChecks.admin ? 'sim' : 'não'}`,
        `- Usuário comum: ${report.stageChecks.user ? 'sim' : 'não'}`,
        `- Login: ${report.stageChecks.login ? 'sim' : 'não'}`
    ].filter(Boolean).join('\n');
}

export function buildJsonReport(summary) {
    return JSON.stringify(buildPlaywrightReport(summary), null, 2);
}

export async function writePlaywrightReportArtifacts(summary, outputDir) {
    const report = buildPlaywrightReport(summary);
    const absoluteDir = path.resolve(outputDir);

    await mkdir(absoluteDir, { recursive: true });

    const jsonPath = path.join(absoluteDir, 'playwright-summary.json');
    const markdownPath = path.join(absoluteDir, 'playwright-summary.md');

    await writeFile(jsonPath, buildJsonReport(report), 'utf8');
    await writeFile(markdownPath, buildMarkdownReport(report), 'utf8');

    return {
        report,
        jsonPath,
        markdownPath
    };
}
