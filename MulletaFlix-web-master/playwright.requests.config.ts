import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
    testDir: './tests/playwright/specs',
    testMatch: ['25-myrequests.spec.ts', '26-request-autocomplete.spec.ts', '27-nebula-navigation.spec.ts', '28-backup-coverage.spec.ts'],
    workers: 1,
    retries: 0,
    use: { baseURL: 'http://127.0.0.1:8097', ...devices['Desktop Chrome'], trace: 'retain-on-failure', screenshot: 'only-on-failure' },
    reporter: 'list',
    webServer: {
        command: 'npm run serve -- --host 127.0.0.1 --port 8097 --strictPort',
        url: 'http://127.0.0.1:8097',
        reuseExistingServer: !process.env.CI,
        timeout: 60_000
    }
});
