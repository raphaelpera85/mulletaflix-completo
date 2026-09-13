import test from 'node:test';
import assert from 'node:assert/strict';
import crypto from 'node:crypto';

import { getAdminCredentials } from './admin-user.mjs';

const originalUser = process.env.MFLX_ADMIN_USER;
const originalPassword = process.env.MFLX_ADMIN_PASSWORD;

test.after(() => {
    if (originalUser === undefined) delete process.env.MFLX_ADMIN_USER;
    else process.env.MFLX_ADMIN_USER = originalUser;

    if (originalPassword === undefined) delete process.env.MFLX_ADMIN_PASSWORD;
    else process.env.MFLX_ADMIN_PASSWORD = originalPassword;
});

test('getAdminCredentials requires credentials from the environment', () => {
    delete process.env.MFLX_ADMIN_USER;
    delete process.env.MFLX_ADMIN_PASSWORD;

    assert.throws(
        () => getAdminCredentials(),
        /MFLX_ADMIN_USER and MFLX_ADMIN_PASSWORD/
    );
});

test('getAdminCredentials returns configured environment credentials', () => {
    const configuredPassword = `test-${crypto.randomBytes(16).toString('hex')}`;
    process.env.MFLX_ADMIN_USER = 'test-admin';
    process.env.MFLX_ADMIN_PASSWORD = configuredPassword;

    assert.deepEqual(getAdminCredentials(), {
        username: 'test-admin',
        password: configuredPassword
    });
});
