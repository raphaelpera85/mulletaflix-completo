import React from 'react';
import { createRoot } from 'react-dom/client';
import { MemoryRouter } from 'react-router-dom';
import SearchResults from '../../../src/apps/stable/features/search/components/SearchResults';

createRoot(document.getElementById('root')!).render(
    <MemoryRouter>
        <SearchResults query='carousel-test' />
    </MemoryRouter>
);
