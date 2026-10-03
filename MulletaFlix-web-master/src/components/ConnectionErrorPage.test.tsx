import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ConnectionState } from 'lib/jellyfin-apiclient';

vi.mock('@mui/material/Button', () => ({
    default: ({ children, onClick }: React.PropsWithChildren<{ onClick?: () => void }>) => (
        <button type='button' onClick={onClick}>{children}</button>
    )
}));
vi.mock('components/Page', () => ({
    default: ({ children, id }: React.PropsWithChildren<{ id: string }>) => <main id={id}>{children}</main>
}));
vi.mock('components/apphost', () => ({ appHost: { supports: () => false } }));
vi.mock('components/toast/toast', () => ({ default: vi.fn() }));
vi.mock('elements/emby-button/LinkButton', () => ({
    default: ({ children }: React.PropsWithChildren) => <button type='button'>{children}</button>
}));
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => ({
            HeaderServerUnavailable: 'Servidor indisponível',
            MessageUnableToConnectToServer: 'Não foi possível conectar ao servidor.',
            HeaderServerMismatch: 'Servidor incompatível',
            MessageServerMismatch: 'A versão do servidor não corresponde.',
            Retry: 'Tentar novamente'
        }[key] || key)
    }
}));
vi.mock('lib/jellyfin-apiclient', () => ({
    ConnectionState: {
        ServerMismatch: 'ServerMismatch',
        ServerUpdateNeeded: 'ServerUpdateNeeded',
        Unavailable: 'Unavailable'
    },
    ServerConnections: {
        getLastUsedServer: vi.fn(),
        updateSavedServerId: vi.fn()
    }
}));

import ConnectionErrorPage from './ConnectionErrorPage';

describe('ConnectionErrorPage', () => {
    let host: HTMLDivElement;
    let root: Root;

    beforeEach(() => {
        host = document.createElement('div');
        document.body.appendChild(host);
        root = createRoot(host);
    });

    afterEach(async () => {
        await act(async () => root.unmount());
        host.remove();
    });

    it('offers an accessible retry when the server is unavailable', async () => {
        const onRetry = vi.fn();

        await act(async () => root.render(<ConnectionErrorPage state={ConnectionState.Unavailable} onRetry={onRetry} />));

        const retryButton = host.querySelector<HTMLButtonElement>('button');
        expect(retryButton?.textContent).toBe('Tentar novamente');
        expect(host.querySelector('[role="alert"]')?.textContent).toContain('Não foi possível conectar ao servidor.');

        await act(async () => retryButton?.click());

        expect(onRetry).toHaveBeenCalledTimes(1);
    });

    it('retries once when browser connectivity returns while the server is unavailable', async () => {
        const onRetry = vi.fn();

        await act(async () => root.render(<ConnectionErrorPage state={ConnectionState.Unavailable} onRetry={onRetry} />));

        await act(async () => {
            window.dispatchEvent(new Event('online'));
            window.dispatchEvent(new Event('online'));
        });

        expect(onRetry).toHaveBeenCalledTimes(1);
    });

    it('removes the connectivity retry listener when the error page unmounts', async () => {
        const onRetry = vi.fn();

        await act(async () => root.render(<ConnectionErrorPage state={ConnectionState.Unavailable} onRetry={onRetry} />));
        await act(async () => root.unmount());

        await act(async () => window.dispatchEvent(new Event('online')));

        expect(onRetry).not.toHaveBeenCalled();
        root = createRoot(host);
    });

    it('does not show retry for server mismatch errors', async () => {
        const onRetry = vi.fn();

        await act(async () => root.render(<ConnectionErrorPage state={ConnectionState.ServerMismatch} onRetry={onRetry} />));

        expect(host.querySelector('h1')?.textContent).toBe('Servidor incompatível');
        const hasRetryButton = Array.from(host.querySelectorAll('button'))
            .some(button => button.textContent === 'Tentar novamente');
        expect(hasRetryButton).toBe(false);

        await act(async () => window.dispatchEvent(new Event('online')));

        expect(onRetry).not.toHaveBeenCalled();
    });
});
