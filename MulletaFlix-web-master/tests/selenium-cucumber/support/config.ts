// Cucumber loads this support module through CommonJS require().
// eslint-disable-next-line @typescript-eslint/no-require-imports
const path = require('node:path');

function trimTrailingSlashes(value) {
    let end = value.length;
    while (end > 0 && value[end - 1] === '/') {
        end -= 1;
    }

    return value.slice(0, end);
}

const ROOT_URL = trimTrailingSlashes(process.env.STAGE_URL || process.env.PW_BASE_URL || 'http://127.0.0.1:8096');

function getRequiredSecret(name) {
    const value = process.env[name]?.trim();
    if (!value) {
        throw new Error(`Set ${name} before running Selenium/Cucumber tests.`);
    }

    return value;
}

module.exports = {
    ROOT_URL,
    ADMIN_USER: process.env.MFLX_ADMIN_USER || 'Raphael',
    get ADMIN_PASSWORD() {
        return getRequiredSecret('MFLX_ADMIN_PASSWORD');
    },
    COMMON_USER: process.env.MFLX_COMMON_USER || 'mflx-user',
    get COMMON_PASSWORD() {
        return getRequiredSecret('MFLX_COMMON_PASSWORD');
    },
    MOVIES_PATH: process.env.MFLX_MOVIES_PATH || 'D:\\Users\\Raphael\\Videos\\Filmes',
    SERIES_PATH: process.env.MFLX_SERIES_PATH || 'D:\\Users\\Raphael\\Videos\\Series',
    IPTV_PATH: process.env.MFLX_IPTV_PATH || 'D:\\Users\\Raphael\\Documents\\Projetos\\m3u\\canais.m3u8',
    REPORT_DIR: process.env.SELENIUM_CUCUMBER_REPORT_DIR
        || path.join(process.cwd(), 'tests', 'selenium-cucumber', 'reports'),
    RAW_REPORT_FILE: process.env.SELENIUM_CUCUMBER_RAW_REPORT
        || path.join(process.cwd(), 'tests', 'selenium-cucumber', 'reports', 'raw-results.json'),
    HEADLESS: process.env.SELENIUM_HEADLESS !== 'false'
};
