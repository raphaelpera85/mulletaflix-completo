import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    useQuery: vi.fn(),
    useMutation: vi.fn(),
    currentApiClient: vi.fn()
}));

vi.mock('@tanstack/react-query', async (importOriginal) => ({
    ...await importOriginal<typeof import('@tanstack/react-query')>(),
    useQuery: mocks.useQuery,
    useMutation: mocks.useMutation
}));
vi.mock('lib/jellyfin-apiclient', () => ({
    ServerConnections: { currentApiClient: mocks.currentApiClient }
}));
vi.mock('components/Page', () => ({
    default: ({ children }: { children: React.ReactNode }) => <div data-testid='page'>{children}</div>
}));
vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div data-testid='loading' />
}));
vi.mock('components/toast/toast', () => ({ default: vi.fn() }));
vi.mock('utils/query/queryClient', () => ({
    queryClient: { invalidateQueries: vi.fn().mockResolvedValue(undefined) }
}));

import HttpsDomainPage from './index';

const NOT_CONFIGURED_STATUS = {
    Enabled: false,
    Configured: false,
    DuckDnsSubdomain: '',
    FullDomain: '',
    AcmeEmail: '',
    TokenConfigured: false,
    MaskedToken: '••••',
    DnsStatus: 'NotConfigured',
    CertificateStatus: 'NotConfigured',
    LastUpdatedUtc: null
};

describe('HttpsDomainPage', () => {
    beforeEach(() => {
        mocks.useQuery.mockReset();
        mocks.useMutation.mockReset();
        mocks.currentApiClient.mockReset();
        mocks.currentApiClient.mockReturnValue({
            getUrl: (path: string) => `http://server/${path}`,
            getJSON: vi.fn().mockResolvedValue(NOT_CONFIGURED_STATUS),
            ajax: vi.fn().mockResolvedValue(NOT_CONFIGURED_STATUS)
        });
        mocks.useMutation.mockReturnValue({ mutate: vi.fn(), isPending: false });
    });

    it('shows the loading indicator while the status query is pending', () => {
        mocks.useQuery.mockReturnValue({ isPending: true, isError: false, data: undefined, refetch: vi.fn() });

        const markup = renderToStaticMarkup(<HttpsDomainPage />);

        expect(markup).toContain('data-testid="loading"');
    });

    it('shows an error state with retry when the status query fails', () => {
        const refetch = vi.fn();
        mocks.useQuery.mockReturnValue({ isPending: false, isError: true, error: new Error('boom'), data: undefined, refetch });

        const markup = renderToStaticMarkup(<HttpsDomainPage />);

        expect(markup).toContain('Não foi possível carregar o status');
        expect(markup).toContain('boom');
    });

    it('renders the configuration form with the DuckDNS/token fields when not configured', () => {
        mocks.useQuery.mockReturnValue({ isPending: false, isError: false, data: NOT_CONFIGURED_STATUS, refetch: vi.fn() });

        const markup = renderToStaticMarkup(<HttpsDomainPage />);

        expect(markup).toContain('Subdomínio DuckDNS');
        expect(markup).toContain('Token DuckDNS');
        // Regression guard: all three inputs (subdomain, e-mail, token) must be
        // required -- the backend's Configure endpoint rejects an empty token
        // unconditionally, so a "leave blank to keep current" affordance here
        // would be a lie that fails on submit every time.
        expect(markup.match(/<input[^>]*\brequired\b/g) || []).toHaveLength(3);
    });

    it('renders the masked token and certificate/DNS status chips when configured', () => {
        const configuredStatus = {
            ...NOT_CONFIGURED_STATUS,
            Configured: true,
            DuckDnsSubdomain: 'myserver',
            FullDomain: 'myserver.duckdns.org',
            AcmeEmail: 'admin@example.com',
            TokenConfigured: true,
            MaskedToken: '••••ab12',
            CertificateStatus: 'Active'
        };
        mocks.useQuery.mockReturnValue({ isPending: false, isError: false, data: configuredStatus, refetch: vi.fn() });

        const markup = renderToStaticMarkup(<HttpsDomainPage />);

        expect(markup).toContain('myserver.duckdns.org');
        expect(markup).toContain('••••ab12');
        expect(markup).toContain('Ativo');
    });
});
