import { defineConfig, devices } from '@playwright/test';

const baseURL = 'http://127.0.0.1:8098';

export default defineConfig({
    testDir: './tests/playwright/specs',
    testMatch: [ '28-backup-coverage.spec.ts', '29-accessibility.spec.ts' ],
    fullyParallel: false,
    workers: 1,
    retries: 0,
    timeout: 30_000,
    expect: { timeout: 10_000 },
    use: {
        baseURL,
        ...devices['Desktop Chrome'],
        headless: true,
        trace: 'retain-on-failure',
        screenshot: 'only-on-failure'
    },
    reporter: 'list',
    webServer: {
        command: 'npm run serve -- --host 127.0.0.1 --port 8098 --strictPort',
        url: baseURL,
        reuseExistingServer: !process.env.CI,
        timeout: 60_000
    }
});
