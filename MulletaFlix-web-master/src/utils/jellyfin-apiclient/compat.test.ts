import { describe, expect, it } from 'vitest';
import type { ApiClient } from 'jellyfin-apiclient';
import { toApi } from './compat';

describe('toApi', () => {
    it('creates an Api instance and configures an authorization request interceptor', async () => {
        const mockApiClient = {
            serverAddress: () => 'http://localhost:8096',
            appName: () => 'MulletaFlix Web',
            appVersion: () => '12.0.0',
            deviceName: () => 'Chrome',
            deviceId: () => 'mock-device-id',
            accessToken: () => 'mock-token-xyz'
        } as unknown as ApiClient;

        const api = toApi(mockApiClient);

        expect(api).toBeDefined();
        expect(api.basePath).toBe('http://localhost:8096');
        expect(api.authorizationHeader).toContain('Token="mock-token-xyz"');

        // Test the interceptor on axiosInstance
        const requestConfig = {
            headers: {} as Record<string, string>
        };

        // Execute handlers registered in axiosInstance interceptors
        const handlers = (api.axiosInstance.interceptors.request as any).handlers || [];
        for (const handler of handlers) {
            if (handler && typeof handler.fulfilled === 'function') {
                await handler.fulfilled(requestConfig);
            }
        }

        expect(requestConfig.headers['Authorization']).toContain('Token="mock-token-xyz"');
    });
});
