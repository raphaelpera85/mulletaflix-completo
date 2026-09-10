export const STAGE_URL = process.env.STAGE_URL || 'http://127.0.0.1:8096';

function requiredEnv(name: string): string {
    const value = process.env[name]?.trim();
    if (!value) {
        throw new Error(`Set ${name} before running the end-to-end tests.`);
    }

    return value;
}

export const TEST_USERS = {
    admin: {
        name: process.env.MFLX_ADMIN_USER || 'Raphael',
        password: requiredEnv('MFLX_ADMIN_PASSWORD')
    },
    common: {
        name: process.env.MFLX_COMMON_USER || 'mflx-user',
        password: requiredEnv('MFLX_COMMON_PASSWORD')
    }
} as const;

export const TEST_LIBRARY = {
    get movies() {
        return requiredEnv('MFLX_MOVIES_PATH');
    },
    get series() {
        return requiredEnv('MFLX_SERIES_PATH');
    },
    get iptv() {
        return requiredEnv('MFLX_IPTV_PATH');
    }
} as const;
