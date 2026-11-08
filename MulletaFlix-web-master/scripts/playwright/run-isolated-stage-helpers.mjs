import { randomUUID } from 'node:crypto';

export function createStageDirName(timestamp = Date.now(), randomId = randomUUID()) {
    return `mflx-playwright-stage-${timestamp}-${randomId.slice(0, 8)}`;
}

export function buildStageEnvironment(parentEnvironment, tempBase) {
    return {
        ...parentEnvironment,
        TEMP: tempBase,
        TMP: tempBase
    };
}
