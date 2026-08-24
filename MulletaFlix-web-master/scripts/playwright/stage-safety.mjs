import path from 'node:path';

function isInsideOrEqual(target, root) {
    const relative = path.relative(path.resolve(root), path.resolve(target));
    return relative === '' || (!relative.startsWith('..') && !path.isAbsolute(relative));
}

export function buildStageRemovalTargets({ workspaceRoot, stageDataDir, legacyStageDataDir }) {
    return [
        path.join(workspaceRoot, 'stage', 'data'),
        stageDataDir,
        legacyStageDataDir
    ];
}

export function assertSafeRemovalTargets(targets, allowedRoots) {
    for (const target of targets) {
        if (!allowedRoots.some(root => isInsideOrEqual(target, root))) {
            throw new Error(`Refusing to remove path outside allowed stage roots: ${target}`);
        }
    }
}

export function assertStagePidFileAbsent(pidFileExists) {
    if (pidFileExists) {
        throw new Error('Refusing to reset while an existing stage pid file has ambiguous ownership.');
    }
}

export function parseStagePid(value) {
    const pid = Number.parseInt(String(value).trim(), 10);
    return Number.isInteger(pid) && pid > 0 ? pid : null;
}

export function buildStageKillArgs(pid) {
    if (!Number.isInteger(pid) || pid <= 0) {
        throw new Error('Cannot stop stage without a valid stage pid.');
    }

    return [ '/PID', String(pid), '/T', '/F' ];
}
