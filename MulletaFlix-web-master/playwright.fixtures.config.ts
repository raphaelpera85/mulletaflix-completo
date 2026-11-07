import { defineConfig, devices } from '@playwright/test';

const port = Number(process.env.PW_FIXTURE_PORT || 18197);
const baseURL = `http://127.0.0.1:${port}`;
const rawResultsFile = process.env.PW_JSON_REPORT || 'reports/playwright/raw-vite-fixture-results.json';

export default defineConfig({
    testDir: './tests/playwright/specs',
    testMatch: [
        '24-scrollbuttons.spec.ts',
        '25-myrequests.spec.ts',
        '26-request-autocomplete.spec.ts',
        '27-nebula-navigation.spec.ts',
        '28-backup-coverage.spec.ts',
        '29-accessibility.spec.ts'
    ],
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
    reporter: [ [ 'list' ], [ 'json', { outputFile: rawResultsFile } ] ],
    webServer: {
        command: `npm run serve -- --host 127.0.0.1 --port ${port} --strictPort`,
        url: baseURL,
        reuseExistingServer: false,
        timeout: 60_000
    }
});
