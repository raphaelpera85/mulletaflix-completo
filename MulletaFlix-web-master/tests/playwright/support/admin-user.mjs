import { expect } from '@playwright/test';
import crypto from 'node:crypto';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

import { fetchStagePublicInfo, openLogin, resolveStageUrl, seedStageConnection, waitForDashboardBridge } from './stage.mjs';
import { ensureWizardCompleted } from './wizard.mjs';

const SHARED_USER_FILE = process.env.MFLX_SHARED_USER_FILE
    || path.join(os.tmpdir(), 'mulletaflix-playwright-common-user.json');

function createCredentials() {
    const suffix = crypto.randomBytes(12).toString('hex');
    const password = crypto.randomBytes(24).toString('base64url');

    return {
        username: `mflx-user-${suffix}`,
        // The value is generated from cryptographic randomness for this disposable test user.
        password
    };
}

export function getAdminCredentials() {
    const username = process.env.MFLX_ADMIN_USER?.trim();
    const password = process.env.MFLX_ADMIN_PASSWORD?.trim();

    if (!username || !password) {
        throw new Error('Set MFLX_ADMIN_USER and MFLX_ADMIN_PASSWORD before running Playwright.');
    }

    return { username, password };
}

export async function readSharedUser() {
    try {
        const content = await fs.readFile(SHARED_USER_FILE, 'utf8');
        return JSON.parse(content);
    } catch (error) {
        if (error?.code === 'ENOENT') {
            return null;
        }

        throw error;
    }
}

export async function saveSharedUser(user) {
    await fs.mkdir(path.dirname(SHARED_USER_FILE), { recursive: true });
    await fs.writeFile(SHARED_USER_FILE, JSON.stringify(user, null, 2), 'utf8');
}

export async function clearSharedUser() {
    try {
        await fs.unlink(SHARED_USER_FILE);
    } catch (error) {
        if (error?.code !== 'ENOENT') {
            throw error;
        }
    }
}

export async function loginWithManualForm(page, username, password) {
    await seedStageConnection(page);
    await openLogin(page);
    await waitForStageBridge(page);
    const visibleCandidate = async locator => {
        for (let index = 0; index < await locator.count(); index++) {
            const candidate = locator.nth(index);
            if (await candidate.isVisible().catch(() => false)) {
                return candidate;
            }
        }

        return null;
    };
    const usernameCandidates = page.locator(
        '#txtManualName, #txtUsername, #loginPage input[autocomplete="username"], #loginPage input[type="email"]'
    );
    let usernameInput = await visibleCandidate(usernameCandidates)
        || await visibleCandidate(page.getByRole('textbox').first());

    if (!usernameInput) {
        const manualLoginButton = page.locator('#loginPage .btnManual');
        if (!(await manualLoginButton.isVisible().catch(() => false))) {
            throw new Error('Login page exposes neither the username/password form nor a visible manual-login button.');
        }

        await manualLoginButton.click();
        usernameInput = page.locator('#txtManualName');
        await usernameInput.waitFor({ state: 'visible', timeout: 10_000 });
    }

    if (!usernameInput) {
        throw new Error('Login form did not expose a visible username field after selecting manual login.');
    }

    await usernameInput.fill(username);
    await expect(usernameInput).toHaveValue(username);
    const passwordCandidates = page.locator(
        '#txtManualPassword, #txtPassword, #loginPage input[type="password"], #loginPage input[autocomplete="current-password"]'
    );
    const passwordInput = await visibleCandidate(passwordCandidates)
        || await visibleCandidate(page.getByRole('textbox').nth(1));
    const submitButton = await visibleCandidate(page.locator('#loginPage button[type="submit"]'))
        || await visibleCandidate(page.getByRole('button', { name: /^(sign in|entrar)$/i }));
    if (!passwordInput || !submitButton) {
        throw new Error('Login form did not expose visible password and submit controls.');
    }

    await passwordInput.fill(password);
    await expect(passwordInput).toHaveValue(password);
    // Filling the password can trigger the legacy login form's autofill/update
    // handlers; assert both required fields immediately before submitting so
    // native browser validation cannot silently suppress the auth request.
    await expect(usernameInput).toHaveValue(username);
    const authenticationResponse = page.waitForResponse(response =>
        response.request().method() === 'POST'
        && new URL(response.url()).pathname.toLowerCase().endsWith('/users/authenticatebyname'),
        { timeout: 10_000 }
    );
    await submitButton.click();
    const response = await authenticationResponse;
    if (!response.ok()) {
        throw new Error(`Login request failed with HTTP ${response.status()}: ${await response.text()}`);
    }

    await page.locator('#indexPage').waitFor({ state: 'visible', timeout: 30_000 });
    await expect(page.locator('.headerUserButton')).toHaveAttribute('title', username, { timeout: 30_000 });
    return {
        User: {
            Name: username
        }
    };
}

export async function logoutViaDrawer(page) {
    const drawerButton = page.locator('.mainDrawerButton');
    await drawerButton.waitFor({ state: 'visible', timeout: 30_000 });
    await drawerButton.click();
    await page.locator('.btnLogout').waitFor({ state: 'visible', timeout: 10_000 });
    await page.locator('.btnLogout').click();
    await page.locator('#loginPage').waitFor({ state: 'visible', timeout: 30_000 });
}

export async function logoutViaDashboard(page) {
    await page.evaluate(() => {
        window.Dashboard.logout();
    });
    await page.locator('#loginPage:not(.hide)').first().waitFor({ state: 'visible', timeout: 30_000 });
}

export async function createCommonUser(page) {
    const info = await fetchStagePublicInfo();
    const credentials = createCredentials();

    const user = await page.evaluate(async ({ currentUsername, currentPassword }) => {
        const createdUser = await window.ApiClient.createUser({
            Name: currentUsername,
            Password: currentPassword
        });

        if (!createdUser?.Id) {
            throw new Error('User creation did not return an id.');
        }

        return {
            username: createdUser.Name || currentUsername,
            password: currentPassword,
            userId: createdUser.Id
        };
    }, {
        currentUsername: credentials.username,
        currentPassword: credentials.password
    });

    const stagedUser = {
        ...user,
        serverId: info.Id
    };

    await saveSharedUser(stagedUser);
    return stagedUser;
}

export async function loadOrCreateSharedUser(page) {
    const info = await fetchStagePublicInfo();
    const sharedUser = await readSharedUser();
    if (sharedUser && sharedUser.serverId === info.Id) {
        return sharedUser;
    }

    if (sharedUser) {
        await clearSharedUser();
    }

    return createCommonUser(page);
}

export async function openUserTab(page, userId, tab) {
    const tabRoutes = {
        profile: 'profile',
        access: 'access',
        parentalcontrol: 'parentalcontrol',
        password: 'password'
    };
    const tabLabels = {
        profile: /Perfil|Profile/i,
        access: /Acesso|Access/i,
        parentalcontrol: /Controle Parental|Parental Control/i,
        password: /Senha|Password/i
    };

    await page.goto(resolveStageUrl(`/dashboard/users/${userId}/${tabRoutes[tab] || 'profile'}`), {
        waitUntil: 'domcontentloaded'
    });
    await page.locator('#usersEditPage').waitFor({ state: 'visible', timeout: 30_000 });

    const tabSelectors = {
        profile: '.editUserProfileForm',
        access: '.userLibraryAccessForm',
        parentalcontrol: '.userParentalControlForm',
        // This is a UI tab identifier, not a credential or password value.
        // eslint-disable-next-line sonarjs/no-hardcoded-passwords
        password: '.updatePasswordForm'
    };

    const selector = tabSelectors[tab] || '.editUserProfileForm';
    const tabLocator = page.getByRole('tab', { name: tabLabels[tab] || /Perfil|Profile/i }).first();

    if (!(await page.locator(selector).isVisible().catch(() => false))) {
        await tabLocator.click({ force: true });
    }

    await page.locator(selector).waitFor({ state: 'visible', timeout: 30_000 });
}

export async function deleteUserById(page, userId) {
    await page.evaluate(async (id) => {
        await window.ApiClient.deleteUser(id);
    }, userId);
}

export async function waitForStageBridge(page) {
    await waitForDashboardBridge(page);
}

export { ensureWizardCompleted };
