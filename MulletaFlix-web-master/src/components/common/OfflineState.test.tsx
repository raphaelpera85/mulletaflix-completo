import { describe, expect, it, vi } from 'vitest';
import { renderToStaticMarkup } from 'react-dom/server';
import React from 'react';
import OfflineState from './OfflineState';

// Mock globalize
vi.mock('lib/globalize', () => ({
    default: {
        translate: (key: string) => key
    }
}));

describe('OfflineState', () => {
    it('renders offline error state by default', () => {
        const markup = renderToStaticMarkup(<OfflineState />);
        expect(markup).toContain('alert');
    });

    it('renders degraded warning state when isDegraded is true', () => {
        const markup = renderToStaticMarkup(<OfflineState isDegraded />);
        expect(markup).toContain('MuiAlert-colorWarning');
    });

    it('renders custom message when provided', () => {
        const markup = renderToStaticMarkup(<OfflineState message='Custom offline message' />);
        expect(markup).toContain('Custom offline message');
    });

    it('renders retry button when onRetry is provided', () => {
        const markup = renderToStaticMarkup(<OfflineState onRetry={() => {}} />);
        expect(markup).toContain('Retry');
        expect(markup).toContain('button');
    });

    it('does not render retry button when onRetry is not provided', () => {
        const markup = renderToStaticMarkup(<OfflineState />);
        expect(markup).not.toContain('button');
    });
});
