import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const { getCurrentApiMock, fetchMock } = vi.hoisted(() => ({
    getCurrentApiMock: vi.fn(),
    fetchMock: vi.fn()
}));

vi.mock('lib/jellyfin-apiclient', () => ({
    ServerConnections: { getCurrentApi: getCurrentApiMock }
}));
vi.mock('components/Page', () => ({
    default: ({ children }: React.PropsWithChildren) => <main>{children}</main>
}));
vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading'>Loading</div>
}));
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string, label?: string) => ({
            UnidentifiedMedia: 'Mídias não identificadas',
            UnidentifiedMediaDescription: 'Revise itens sem identificação.',
            Movies: 'Filmes',
            Series: 'Séries',
            Refresh: 'Atualizar',
            Retry: 'Tentar novamente',
            NoUnidentifiedItems: `Nenhum item não identificado: ${label}`,
            ItemsFound: `Itens encontrados: ${label}`,
            Name: 'Nome',
            LabelPath: 'Caminho',
            DateAdded: 'Adicionado',
            Identify: 'Identificar'
        }[key] ?? key)
    }
}));

import { Component as UnidentifiedPage } from './unidentified';

const item = (name: string, id: string) => ({
    Id: id,
    Name: name,
    Path: `N:/Filmes/${name}.mkv`,
    Type: 'Movie',
    DateCreated: '2026-10-07T00:00:00.000Z'
});

const response = (items: ReturnType<typeof item>[]) => ({
    ok: true,
    json: vi.fn().mockResolvedValue(items)
}) as unknown as Response;

const deferred = <T,>() => {
    let resolve!: (value: T) => void;
    const promise = new Promise<T>(resolvePromise => {
        resolve = resolvePromise;
    });
    return { promise, resolve };
};

describe('UnidentifiedPage query states with real TanStack Query', () => {
    let container: HTMLDivElement;
    let root: Root;
    let client: QueryClient;

    beforeEach(() => {
        getCurrentApiMock.mockReturnValue({ basePath: 'http://server.test', accessToken: 'test-token' });
        fetchMock.mockReset();
        vi.stubGlobal('fetch', fetchMock);
        vi.stubGlobal('IS_REACT_ACT_ENVIRONMENT', true);
        container = document.createElement('div');
        document.body.append(container);
        root = createRoot(container);
        client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 60_000 } } });
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        client.clear();
        container.remove();
        vi.unstubAllGlobals();
    });

    const mount = async () => {
        await act(async () => root.render(
            <QueryClientProvider client={client}>
                <UnidentifiedPage />
            </QueryClientProvider>
        ));
    };

    it('does not report an empty library until the initial query resolves', async () => {
        const pending = deferred<Response>();
        fetchMock.mockReturnValueOnce(pending.promise);

        await mount();
        await vi.waitFor(() => expect(container.querySelector('[data-testid="loading"]')).not.toBeNull());
        expect(container.textContent).not.toContain('Nenhum item não identificado');
        expect(container.querySelector<HTMLButtonElement>('button[aria-label="Atualizar"]')?.disabled).toBe(true);

        await act(async () => pending.resolve(response([])));
        await vi.waitFor(() => expect(container.textContent).toContain('Nenhum item não identificado: Filmes'));
        expect(container.querySelector<HTMLButtonElement>('button[aria-label="Atualizar"]')?.disabled).toBe(false);
    });

    it('keeps the last table visible after polling fails and replaces it after retry succeeds', async () => {
        fetchMock.mockResolvedValueOnce(response([item('Filme conhecido', 'movie-1')]))
            .mockRejectedValueOnce(new Error('transient network failure'))
            .mockResolvedValueOnce(response([item('Outro filme', 'movie-2')]));

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('Filme conhecido'));

        await act(async () => {
            await client.invalidateQueries({ queryKey: ['UnidentifiedItems', 'Movies'] });
        });

        await vi.waitFor(() => expect(container.textContent).toContain('transient network failure'));
        expect(container.textContent).toContain('Filme conhecido');
        expect(container.querySelector('table')).not.toBeNull();

        const retry = container.querySelector<HTMLButtonElement>('button[aria-label="Tentar novamente"]');
        expect(retry).not.toBeNull();
        await act(async () => retry?.click());

        await vi.waitFor(() => expect(container.textContent).toContain('Outro filme'));
        expect(container.textContent).not.toContain('transient network failure');
        expect(container.textContent).not.toContain('Filme conhecido');
        expect(fetchMock).toHaveBeenCalledTimes(3);
    });

    it('does not show movies as series while the series tab loads', async () => {
        const seriesPending = deferred<Response>();
        fetchMock.mockResolvedValueOnce(response([item('Somente filme', 'movie-2')]))
            .mockReturnValueOnce(seriesPending.promise);

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('Somente filme'));

        const seriesTab = Array.from(container.querySelectorAll<HTMLElement>('[role="tab"]'))
            .find(tab => tab.textContent === 'Séries');
        expect(seriesTab).not.toBeNull();
        await act(async () => seriesTab?.click());

        await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
        expect(container.querySelector('[data-testid="loading"]')).not.toBeNull();
        expect(container.textContent).not.toContain('Somente filme');
        expect(container.textContent).not.toContain('Nenhum item não identificado: Séries');

        await act(async () => seriesPending.resolve(response([])));
        await vi.waitFor(() => expect(container.textContent).toContain('Nenhum item não identificado: Séries'));
    });

    it('labels cached series as updating and replaces them after refresh', async () => {
        const seriesPending = deferred<Response>();
        fetchMock.mockResolvedValueOnce(response([item('Somente filme', 'movie-3')]))
            .mockReturnValueOnce(seriesPending.promise);
        client.setQueryData(['UnidentifiedItems', 'Series'], [item('Série em cache', 'series-1')]);

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('Somente filme'));

        const seriesTab = Array.from(container.querySelectorAll<HTMLElement>('[role="tab"]'))
            .find(tab => tab.textContent === 'Séries');
        await act(async () => seriesTab?.click());

        await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));
        expect(container.textContent).toContain('Série em cache');
        expect(container.textContent).toContain('Atualizando resultados…');
        expect(container.textContent).not.toContain('Somente filme');
        expect(container.querySelector<HTMLButtonElement>('button[aria-label="Atualizar"]')?.disabled).toBe(true);

        await act(async () => seriesPending.resolve(response([item('Série atualizada', 'series-2')])));
        await vi.waitFor(() => expect(container.textContent).toContain('Série atualizada'));
        expect(container.textContent).not.toContain('Série em cache');
        expect(container.textContent).not.toContain('Atualizando resultados…');
        expect(container.querySelector<HTMLButtonElement>('button[aria-label="Atualizar"]')?.disabled).toBe(false);
    });

    it('shows initial query errors instead of empty results and recovers by retry', async () => {
        fetchMock.mockRejectedValueOnce(new Error('initial request failed'))
            .mockResolvedValueOnce(response([item('Após retry', 'movie-4')]));

        await mount();
        await vi.waitFor(() => expect(container.textContent).toContain('initial request failed'));
        expect(container.textContent).not.toContain('Nenhum item não identificado');
        expect(container.querySelector('table')).toBeNull();

        const retry = container.querySelector<HTMLButtonElement>('button[aria-label="Tentar novamente"]');
        expect(retry).not.toBeNull();
        await act(async () => retry?.click());

        await vi.waitFor(() => expect(container.textContent).toContain('Após retry'));
        expect(container.textContent).not.toContain('initial request failed');
        expect(fetchMock).toHaveBeenCalledTimes(2);
    });
});
