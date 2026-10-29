import path from 'node:path';

export function assertSafeStageBaseUrl(value) {
    let parsed;
    try {
        parsed = new URL(String(value || ''));
    } catch {
        throw new Error('Playwright stage URL must be a valid loopback HTTP URL.');
    }

    const port = Number(parsed.port || 80);
    if (parsed.protocol !== 'http:'
        || ![ '127.0.0.1', 'localhost', '[::1]', '::1' ].includes(parsed.hostname.toLowerCase())
        || !Number.isInteger(port)
        || port < 18096
        || port > 18196) {
        throw new Error('Playwright stage URL must use loopback HTTP on an isolated port from 18096 through 18196.');
    }

    return value;
}

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

export function parseWindowsUserSid(output) {
    return String(output || '').match(/S-\d-(?:\d+-)*\d+/i)?.[0] || null;
}

export function isDedicatedStageDataPath(candidate, workspaceRoot, tempRoot) {
    const name = path.basename(candidate || '');
    if (!name.startsWith('mflx-playwright-stage-')) {
        return false;
    }

    const isContained = root => {
        const relative = path.relative(path.resolve(root), path.resolve(candidate));
        return Boolean(relative)
            && relative !== '..'
            && !relative.startsWith(`..${path.sep}`)
            && !path.isAbsolute(relative);
    };

    const workspaceRelative = path.relative(path.resolve(workspaceRoot), path.resolve(candidate));
    const isDirectWorkspaceChild = Boolean(workspaceRelative)
        && workspaceRelative !== '..'
        && !workspaceRelative.startsWith(`..${path.sep}`)
        && !path.isAbsolute(workspaceRelative)
        && !workspaceRelative.includes(path.sep);

    return isDirectWorkspaceChild || isContained(tempRoot);
}

export function buildRestrictedDirectoryAclArgs(directoryPath, userSid) {
    if (!directoryPath || !userSid || !/^S-\d-(?:\d+-)*\d+$/i.test(userSid)) {
        throw new Error('A stage directory and a valid Windows user SID are required to restrict its ACL.');
    }

    return [
        directoryPath,
        '/inheritance:r',
        '/grant:r',
        `*${userSid}:(OI)(CI)F`,
        '*S-1-5-18:(OI)(CI)F',
        '*S-1-5-32-544:(OI)(CI)F'
    ];
}

export function resolveDotnetExecutable(platform, programFiles) {
    return platform === 'win32' ?
        path.join(programFiles || 'C:\\Program Files', 'dotnet', 'dotnet.exe') :
        'dotnet';
}
