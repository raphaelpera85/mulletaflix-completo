import assert from 'node:assert/strict';
import test from 'node:test';
import { createStageDirName } from './run-isolated-stage-helpers.mjs';

test('createStageDirName uses a timestamp and UUID-shaped entropy', () => {
    const name = createStageDirName(1700000000000, '12345678-1234-4abc-8def-123456789abc');

    assert.match(name, /^mflx-playwright-stage-1700000000000-12345678$/);
});
