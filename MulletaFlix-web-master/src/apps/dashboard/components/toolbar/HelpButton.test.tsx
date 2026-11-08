import React from 'react';
import { MemoryRouter } from 'react-router-dom';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import HelpButton from './HelpButton';

const render = (path: string) => renderToStaticMarkup(
    <MemoryRouter initialEntries={[path]}>
        <HelpButton />
    </MemoryRouter>
);

describe('HelpButton', () => {
    it('shows the XMLTV guide link when query parameters are present', () => {
        const markup = render('/dashboard/livetv/guide?type=xmltv');

        expect(markup).toContain('href="https://MulletaFlix.org/docs/general/server/live-tv/setup-guide#adding-guide-data"');
    });

    it('renders nothing when the current route has no help link', () => {
        expect(render('/dashboard/media-requests')).toBe('');
    });
});
