import assert from 'node:assert/strict';
import test from 'node:test';
import { resolveNebulaEnabledForSave } from '../../Jellyfin.Api/Web/NebulaFTP/configPage.js';

test('autosave preserves a persisted disabled state even if checkbox is checked', () => {
    assert.equal(resolveNebulaEnabledForSave(true, { checked: true }, { Enabled: false }), false);
});

test('autosave preserves a persisted enabled state even if checkbox is unchecked', () => {
    assert.equal(resolveNebulaEnabledForSave(true, { checked: false }, { Enabled: true }), true);
});

test('explicit save applies the checkbox state', () => {
    assert.equal(resolveNebulaEnabledForSave(false, { checked: false }, { Enabled: true }), false);
    assert.equal(resolveNebulaEnabledForSave(false, { checked: true }, { Enabled: false }), true);
});

test('missing checkbox preserves persisted state for both save paths', () => {
    assert.equal(resolveNebulaEnabledForSave(false, null, { Enabled: false }), false);
    assert.equal(resolveNebulaEnabledForSave(true, null, { Enabled: true }), true);
});
