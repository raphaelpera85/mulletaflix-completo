import React from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import SearchResults from '../../../src/apps/stable/features/search/components/SearchResults';
import keyboardNavigation from '../../../src/scripts/keyboardNavigation';
import layoutManager from '../../../src/components/layoutManager';

if (new URLSearchParams(window.location.search).get('keyboard') === 'tv') {
    layoutManager.tv = true;
    keyboardNavigation.enable();
}

createRoot(document.getElementById('root')!).render(
    <MemoryRouter>
        <SearchResults query='carousel-test' />
    </MemoryRouter>
);
