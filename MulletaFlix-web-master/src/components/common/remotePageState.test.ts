import { describe, expect, it } from 'vitest';

import { getRemotePageState } from './remotePageState';

describe('getRemotePageState', () => {
    it.each([
        [{ isPending: true, isError: false, hasData: false, isOnline: true }, 'loading'],
        [{ isPending: true, isError: false, hasData: false, isOnline: false }, 'offline'],
        [{ isPending: false, isError: true, hasData: false, isOnline: true }, 'error'],
        [{ isPending: false, isError: true, hasData: false, isOnline: false }, 'offline'],
        [{ isPending: false, isError: true, hasData: true, isOnline: true }, 'degraded'],
        [{ isPending: false, isError: false, hasData: true, isOnline: false }, 'degraded'],
        [{ isPending: false, isError: false, hasData: true, isOnline: true }, 'success']
    ] as const)('maps remote state %#', (input, expected) => {
        expect(getRemotePageState(input)).toBe(expected);
    });
});
