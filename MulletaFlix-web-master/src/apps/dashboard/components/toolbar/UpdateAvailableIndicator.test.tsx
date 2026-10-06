import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import UpdateAvailableIndicator from './UpdateAvailableIndicator';

const render = (updateAvailable: boolean, availableVersion?: string) => renderToStaticMarkup(
    <MemoryRouter>
        <UpdateAvailableIndicator updateAvailable={updateAvailable} availableVersion={availableVersion} />
    </MemoryRouter>
);

describe('UpdateAvailableIndicator', () => {
    it('renders nothing when there is no update available', () => {
        const markup = render(false);
        expect(markup).toBe('');
    });

    it('stays discreet (renders nothing) even if a stale version string is present without UpdateAvailable', () => {
        const markup = render(false, '1.2.3');
        expect(markup).toBe('');
    });

    it('shows a badge linking to the update center when an update is available', () => {
        const markup = render(true, '2.0.0');
        expect(markup).toContain('href="/dashboard/updates"');
        expect(markup).toContain('Atualização disponível: 2.0.0');
        // MUI Badge renders the dot as a span with the MuiBadge-dot class
        expect(markup).toContain('MuiBadge-dot');
    });

    it('falls back to a generic label when the available version is unknown', () => {
        const markup = render(true);
        expect(markup).toContain('Atualização disponível');
        expect(markup).not.toContain('Atualização disponível: ');
    });
});
