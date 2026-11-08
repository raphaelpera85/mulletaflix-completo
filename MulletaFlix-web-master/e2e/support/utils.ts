import { expect, Page, APIRequestContext, Locator } from '@playwright/test';
import { STAGE_URL } from './constants';

export async function apiJson(request: APIRequestContext, path: string, options?: Parameters<APIRequestContext['get']>[1]) {
    const response = await request.get(new URL(path, STAGE_URL).toString(), options);
    expect(response.ok()).toBeTruthy();
    return response.json();
}

export async function apiPostJson(request: APIRequestContext, path: string, body: unknown) {
    const response = await request.post(new URL(path, STAGE_URL).toString(), {
        data: body,
        headers: {
            'Content-Type': 'application/json'
        }
    });
    expect(response.ok()).toBeTruthy();
    return response;
}

export async function openRoot(page: Page) {
    await page.goto('/');
}

export async function openLogin(page: Page) {
    await page.goto('/#/login');
    await expect(page.locator('#loginPage')).toBeVisible({ timeout: 30_000 });
}

export async function loginWithManualCredentials(page: Page, username: string, password: string) {
    const manualForm = page.locator('.manualLoginForm');

    if (!await manualForm.isVisible().catch(() => false)) {
        const manualButton = page.locator('.btnManual:visible');
        if (await manualButton.count()) {
            await manualButton.click();
        } else {
            const userCard = page.locator('.cardContent[data-username]').filter({ hasText: username }).first();
            await expect(userCard).toBeVisible({ timeout: 30_000 });
            await userCard.click();
        }
    }

    await expect(manualForm).toBeVisible({ timeout: 30_000 });
    await page.locator('#txtManualName').fill(username);
    await page.locator('#txtManualPassword').fill(password);
    await manualForm.locator('button[type="submit"]').click();
}

export async function openDashboard(page: Page) {
    await page.goto('/#/dashboard/users');
}

export async function fillVisibleSelect(locator: Locator, preferredValues: string[] = []) {
    const options = await locator.locator('option').evaluateAll(nodes => nodes.map(node => ({
        value: (node as HTMLOptionElement).value,
        text: (node as HTMLOptionElement).textContent?.trim() || ''
    })).filter(opt => opt.value !== ''));

    for (const preferred of preferredValues) {
        const match = options.find(option => option.value === preferred || option.text.toLowerCase().includes(preferred.toLowerCase()));
        if (match) {
            await locator.selectOption(match.value);
            return match.value;
        }
    }

    if (options[0]?.value) {
        await locator.selectOption(options[0].value);
        return options[0].value;
    }

    throw new Error('No selectable option found');
}

export async function setCheckbox(page: Page, selector: string, checked: boolean) {
    const checkbox = page.locator(selector);
    if (await checkbox.isVisible().catch(() => false)) {
        if (checked) {
            await checkbox.check();
        } else {
            await checkbox.uncheck();
        }
    }
}
