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
        const user = JSON.parse(content);
        if (!user) return null;
        const info = await fetchStagePublicInfo().catch(() => null);
        if (info?.Id && user.serverId && user.serverId !== info.Id) return null;
        return user;
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
    const loginPage = page.locator('#loginPage:not(.hide)').first();
    await loginPage.waitFor({ state: 'visible', timeout: 10_000 });
    const visibleCandidate = async locator => {
        for (let index = 0; index < await locator.count(); index++) {
            const candidate = locator.nth(index);
            if (await candidate.isVisible().catch(() => false)) {
                return candidate;
            }
        }

        return null;
    };
    const usernameCandidates = loginPage.locator(
        '#txtManualName, #txtUsername, input[autocomplete="username"], input[type="email"], input:not([type="hidden"]):not([type="password"]):not([type="checkbox"])'
    );
    let usernameInput = await visibleCandidate(usernameCandidates);

    if (!usernameInput) {
        const manualLoginButton = loginPage.locator('.btnManual');
        const userCard = loginPage.locator('.cardContent[data-username]').filter({ hasText: username }).first();

        for (let attempt = 0; attempt < 20 && !usernameInput; attempt++) {
            usernameInput = await visibleCandidate(usernameCandidates);
            if (usernameInput) break;

            const visibleManualButton = await visibleCandidate(manualLoginButton);
            if (visibleManualButton) {
                await visibleManualButton.click().catch(() => undefined);
            } else {
                const visibleUserCard = await visibleCandidate(userCard);
                if (visibleUserCard) {
                    await visibleUserCard.click().catch(() => undefined);
                }
            }

            await page.waitForTimeout(250);
        }

        if (!usernameInput) {
            throw new Error('Login page did not expose a stable username/password form, manual-login button, or matching user card.');
        }
    }

    if (!usernameInput) {
        throw new Error('Login form did not expose a visible username field after selecting manual login.');
    }

    await usernameInput.fill(username);
    await expect(usernameInput).toHaveValue(username);
    const passwordCandidates = loginPage.locator(
        '#txtManualPassword, #txtPassword, input[type="password"], input[autocomplete="current-password"]'
    );
    const passwordInput = await visibleCandidate(passwordCandidates);
    const submitButton = await visibleCandidate(loginPage.locator('button[type="submit"]'))
        || await visibleCandidate(loginPage.getByRole('button', { name: /^(sign in|entrar)$/i }));
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
    await Promise.race([
        page.locator('#loginPage:not(.hide), #selectServerPage:not(.hide), #loginPage, #selectServerPage').first().waitFor({ state: 'visible', timeout: 30_000 }),
        page.waitForURL(/#?\/(login|selectserver)/i, { timeout: 30_000 })
    ]).catch(() => undefined);
}

export async function logoutViaDashboard(page) {
    await page.evaluate(async () => {
        if (window.Dashboard && typeof window.Dashboard.logout === 'function') {
            window.Dashboard.logout();
        } else if (window.ApiClient && typeof window.ApiClient.logout === 'function') {
            await window.ApiClient.logout();
        }
    });
    await Promise.race([
        page.locator('#loginPage:not(.hide), #selectServerPage:not(.hide), #loginPage, #selectServerPage').first().waitFor({ state: 'visible', timeout: 30_000 }),
        page.waitForURL(/#?\/(login|selectserver)/i, { timeout: 30_000 })
    ]).catch(() => undefined);
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
        await tabLocator.waitFor({ state: 'visible', timeout: 30_000 });
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
