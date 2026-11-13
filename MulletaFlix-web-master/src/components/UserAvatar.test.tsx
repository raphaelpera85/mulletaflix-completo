import React from 'react';
import { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { useApiMock } = vi.hoisted(() => ({ useApiMock: vi.fn() }));

vi.mock('hooks/useApi', () => ({ useApi: useApiMock }));
vi.mock('@mui/material/Avatar', () => ({
    default: ({ src, alt }: { src?: string; alt?: string }) => <img data-testid='user-avatar' src={src} alt={alt} />
}));

import UserAvatar from './UserAvatar';

describe('UserAvatar', () => {
    let container: HTMLDivElement;
    let root: Root;

    beforeEach(() => {
        container = document.createElement('div');
        document.body.appendChild(container);
        root = createRoot(container);
    });

    afterEach(() => {
        act(() => root.unmount());
        container.remove();
        vi.clearAllMocks();
    });

    it('uses the authenticated image URL for a user primary image', async () => {
        const getUserImageUrl = vi.fn(() => 'https://server.test/Users/user-id/Images/Primary?tag=image-tag&api_key=session-token');
        useApiMock.mockReturnValue({
            api: { basePath: 'https://server.test' },
            __legacyApiClient__: { getUserImageUrl }
        });

        await act(async () => root.render(
            <UserAvatar user={{ Id: 'user-id', Name: 'Raphael', PrimaryImageTag: 'image-tag' }} />
        ));

        expect(getUserImageUrl).toHaveBeenCalledWith('user-id', { tag: 'image-tag', type: 'Primary' });
        expect(container.querySelector('img')?.getAttribute('src')).toBe(
            'https://server.test/Users/user-id/Images/Primary?tag=image-tag&api_key=session-token'
        );
    });

    it('does not request an image when the user has no primary image tag', async () => {
        const getUserImageUrl = vi.fn();
        useApiMock.mockReturnValue({ __legacyApiClient__: { getUserImageUrl } });

        await act(async () => root.render(<UserAvatar user={{ Id: 'user-id', Name: 'Raphael' }} />));

        expect(getUserImageUrl).not.toHaveBeenCalled();
        expect(container.querySelector('img')?.getAttribute('src')).toBeNull();
    });
});
