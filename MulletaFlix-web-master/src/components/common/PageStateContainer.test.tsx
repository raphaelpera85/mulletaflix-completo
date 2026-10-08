import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';

vi.mock('lib/globalize', () => ({
    default: { translate: (key: string) => key }
}));

vi.mock('components/loading/LoadingComponent', () => ({
    default: () => <div>Loading</div>
}));

vi.mock('components/common/LoadErrorMessage', () => ({
    default: ({ message }: { message?: string }) => <div role='alert'>{message || 'Load failed'}</div>
}));

import PageStateContainer from './PageStateContainer';

describe('PageStateContainer', () => {
    it('keeps previously loaded content visible with a degraded warning and retry', () => {
        const onRetry = vi.fn();
        const markup = renderToStaticMarkup(
            <PageStateContainer state='degraded' onRetry={onRetry}>
                <p>Cached search results</p>
            </PageStateContainer>
        );

        expect(markup).toContain('Cached search results');
        expect(markup).toContain('OfflineModeWarning');
        expect(markup).toContain('Retry');
    });

    it('renders a terminal offline state with an actionable retry when no content exists', () => {
        const onRetry = vi.fn();
        const markup = renderToStaticMarkup(
            <PageStateContainer state='offline' onRetry={onRetry} />
        );

        expect(markup).toContain('OfflineModeError');
        expect(markup).toContain('Retry');
        expect(markup).not.toContain('Cached search results');
    });
});
