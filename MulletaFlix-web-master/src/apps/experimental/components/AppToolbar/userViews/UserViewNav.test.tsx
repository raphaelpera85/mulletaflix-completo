import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import { createTheme, ThemeProvider } from '@mui/material/styles';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    useApi: vi.fn(),
    useUserViews: vi.fn(),
    useAncestors: vi.fn(),
    showMediaRequestDialog: vi.fn(),
    currentApiClient: vi.fn()
}));

vi.mock('hooks/useApi', () => ({ useApi: mocks.useApi }));
vi.mock('hooks/api/useUserViews', () => ({ useUserViews: mocks.useUserViews }));
vi.mock('apps/experimental/features/libraries/hooks/api/useAncestors', () => ({ useAncestors: mocks.useAncestors }));
vi.mock('components/userFeedback/userFeedback', () => ({ showMediaRequestDialog: mocks.showMediaRequestDialog }));
vi.mock('lib/jellyfin-apiclient', () => ({
    ServerConnections: { currentApiClient: mocks.currentApiClient }
}));
vi.mock('hooks/useWebConfig', () => ({ useWebConfig: () => ({ menuLinks: [] }) }));
vi.mock('hooks/useCurrentTab', () => ({ default: () => ({ activeTab: 0 }) }));
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            Favorites: 'Favoritos',
            MediaRequestsMenuTitle: 'Solicitações',
            MediaRequestTitle: 'Solicitar inclusão de mídia',
            MyMediaRequestsTitle: 'Minhas solicitações',
            ButtonMore: 'Mais'
        }[key] || key)
    }
}));

import UserViewNav from './UserViewNav';

const apiClient = { getUrl: vi.fn() };

const renderNav = async (user: { Id: string } | undefined) => {
    const container = document.createElement('div');
    const root: Root = createRoot(container);
    mocks.useApi.mockReturnValue({ user });
    mocks.useUserViews.mockReturnValue({ data: { Items: [] }, isPending: !user });
    mocks.useAncestors.mockReturnValue({ data: undefined });
    mocks.currentApiClient.mockReturnValue(apiClient);

    await act(async () => {
        root.render(
            <ThemeProvider theme={createTheme()}>
                <MemoryRouter initialEntries={['/home']}>
                    <UserViewNav />
                </MemoryRouter>
            </ThemeProvider>
        );
    });

    return { container, root };
};

const findNavItem = (container: HTMLElement, accessibleName: string) => [
    ...Array.from(container.getElementsByTagName('a')),
    ...Array.from(container.getElementsByTagName('button'))
].find(item => item.textContent?.trim() === accessibleName);

describe('UserViewNav media request entry point', () => {
    beforeEach(() => {
        mocks.useApi.mockReset();
        mocks.useUserViews.mockReset();
        mocks.useAncestors.mockReset();
        mocks.showMediaRequestDialog.mockReset();
        mocks.currentApiClient.mockReset();
    });

    it('groups both media request actions in one popup next to Favorites', async () => {
        const { container, root } = await renderNav({ Id: 'user-1' });

        const favoritesButton = findNavItem(container, 'Favoritos');
        const requestsButton = findNavItem(container, 'Solicitações');

        expect(favoritesButton).toBeDefined();
        expect(requestsButton).toBeDefined();
        expect(favoritesButton?.nextElementSibling).toBe(requestsButton);
        expect(requestsButton?.getAttribute('aria-haspopup')).toBe('menu');
        expect(findNavItem(container, 'Solicitar inclusão de mídia')).toBeUndefined();
        expect(findNavItem(container, 'Minhas solicitações')).toBeUndefined();

        await act(async () => requestsButton?.click());
        expect(requestsButton?.getAttribute('aria-expanded')).toBe('true');
        const requestAction = [...document.body.querySelectorAll<HTMLElement>('[role="menuitem"]')]
            .find(item => item.textContent?.trim() === 'Solicitar inclusão de mídia');
        const myRequestsAction = [...document.body.querySelectorAll<HTMLElement>('[role="menuitem"]')]
            .find(item => item.textContent?.includes('Minhas solicitações'));

        expect(requestAction).toBeDefined();
        expect(myRequestsAction).toBeDefined();
        expect(myRequestsAction?.getAttribute('href')).toBe('/myrequests');

        await act(async () => requestAction?.click());
        expect(mocks.showMediaRequestDialog).toHaveBeenCalledOnce();
        expect(mocks.showMediaRequestDialog).toHaveBeenCalledWith(apiClient);
        expect(requestsButton?.getAttribute('aria-expanded')).toBeNull();

        await act(async () => root.unmount());
    });

    it('does not expose user request actions before authentication is available', async () => {
        const { container, root } = await renderNav(undefined);

        expect(findNavItem(container, 'Favoritos')).toBeUndefined();
        expect(findNavItem(container, 'Solicitações')).toBeUndefined();
        expect(findNavItem(container, 'Solicitar inclusão de mídia')).toBeUndefined();
        expect(findNavItem(container, 'Minhas solicitações')).toBeUndefined();

        await act(async () => root.unmount());
    });
});
