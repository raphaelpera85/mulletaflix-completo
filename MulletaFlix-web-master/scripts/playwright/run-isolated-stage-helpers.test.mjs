import assert from 'node:assert/strict';
import test from 'node:test';
import { buildStageEnvironment, createStageDirName } from './run-isolated-stage-helpers.mjs';

test('createStageDirName uses a timestamp and UUID-shaped entropy', () => {
    const name = createStageDirName(1700000000000, '12345678-1234-4abc-8def-123456789abc');

    assert.match(name, /^mflx-playwright-stage-1700000000000-12345678$/);
});

test('buildStageEnvironment isolates TEMP and TMP without mutating the parent environment', () => {
    const parent = { PATH: 'safe', TEMP: 'old-temp', TMP: 'old-tmp' };
    const child = buildStageEnvironment(parent, 'C:\\Users\\Raphael\\.mulletaflix-tmp');

    assert.deepEqual(parent, { PATH: 'safe', TEMP: 'old-temp', TMP: 'old-tmp' });
    assert.equal(child.PATH, 'safe');
    assert.equal(child.TEMP, 'C:\\Users\\Raphael\\.mulletaflix-tmp');
    assert.equal(child.TMP, 'C:\\Users\\Raphael\\.mulletaflix-tmp');
});
