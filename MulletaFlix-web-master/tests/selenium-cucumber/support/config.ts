// Cucumber loads this support module through CommonJS require().
const path = require('node:path');

function trimTrailingSlashes(value) {
    let end = value.length;
    while (end > 0 && value[end - 1] === '/') {
        end -= 1;
    }

    return value.slice(0, end);
}

const ROOT_URL = trimTrailingSlashes(process.env.STAGE_URL || process.env.PW_BASE_URL || 'http://127.0.0.1:8096');

function getRequiredEnv(name) {
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
        return getRequiredEnv('MFLX_ADMIN_PASSWORD');
    },
    COMMON_USER: process.env.MFLX_COMMON_USER || 'mflx-user',
    get COMMON_PASSWORD() {
        return getRequiredEnv('MFLX_COMMON_PASSWORD');
    },
    get MOVIES_PATH() {
        return getRequiredEnv('MFLX_MOVIES_PATH');
    },
    get SERIES_PATH() {
        return getRequiredEnv('MFLX_SERIES_PATH');
    },
    get IPTV_PATH() {
        return getRequiredEnv('MFLX_IPTV_PATH');
    },
    REPORT_DIR: process.env.SELENIUM_CUCUMBER_REPORT_DIR
        || path.join(process.cwd(), 'tests', 'selenium-cucumber', 'reports'),
    RAW_REPORT_FILE: process.env.SELENIUM_CUCUMBER_RAW_REPORT
        || path.join(process.cwd(), 'tests', 'selenium-cucumber', 'reports', 'raw-results.json'),
    HEADLESS: process.env.SELENIUM_HEADLESS !== 'false'
};
