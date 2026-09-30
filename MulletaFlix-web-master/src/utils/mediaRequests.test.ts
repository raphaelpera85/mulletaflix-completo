import { describe, expect, it } from 'vitest';
import { classifyMediaRequests, isMediaRequestIncluded, type MediaCatalogTitle } from './mediaRequests';

const catalog: MediaCatalogTitle[] = [
    { Title: 'Atomic (2024)', MediaType: 'Series', Year: 2024 },
    { Title: 'Atomic (2020)', MediaType: 'Series', Year: 2020 },
    { Title: 'Atomic', MediaType: 'Animation' },
    { Title: 'Welcome to Demon School! Iruma-kun (2019)', MediaType: 'Animation', Year: 2019 }
];

describe('media request catalog matching', () => {
    it('matches title by category so a same-name item in another library does not count', () => {
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Atomic', Overview: 'Series · 2024' }, catalog)).toBe(true);
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Atomic', Overview: 'Dorama' }, catalog)).toBe(false);
    });

    it('keeps same-category remakes pending unless the request identifies a year', () => {
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Atomic', Overview: 'Series' }, catalog)).toBe(false);
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Atomic', Overview: 'Series · 2020' }, catalog)).toBe(true);
    });

    it('matches normalized titles and the year supplied with the request', () => {
        expect(isMediaRequestIncluded({
            Name: 'Solicitação de mídia: Welcome to Demon School! Iruma-kun',
            Overview: 'Animation · 2019'
        }, catalog)).toBe(true);
        expect(isMediaRequestIncluded({
            Name: 'Solicitação de mídia: Welcome to Demon School! Iruma-kun',
            Overview: 'Animation · 2020'
        }, catalog)).toBe(false);
    });

    it('keeps legacy requests ambiguous when same title exists in multiple categories', () => {
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Atomic' }, catalog)).toBe(false);
    });

    it('places requests in separate pending and included grids', () => {
        const requests = [
            { Name: 'Solicitação de mídia: Atomic', Overview: 'Series · 2024' },
            { Name: 'Solicitação de mídia: New Show', Overview: 'Series · 2026' }
        ];

        expect(classifyMediaRequests(requests, catalog)).toEqual({
            pending: [ requests[1] ],
            included: [ requests[0] ]
        });
    });

    it('uses an explicit release year instead of numbers inside the title', () => {
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: 2001 A Space Odyssey (1968)', Overview: 'Movie' }, [
            { Title: '2001 A Space Odyssey (1968)', MediaType: 'Movie', Year: 1968 }
        ])).toBe(true);
    });

    it('does not collapse a numeric sequel title into the original work', () => {
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Blade Runner 2049', Overview: 'Movie' }, [
            { Title: 'Blade Runner (1982)', MediaType: 'Movie', Year: 1982 }
        ])).toBe(false);
        expect(isMediaRequestIncluded({ Name: 'Solicitação de mídia: Blade Runner 2049', Overview: 'Movie' }, [
            { Title: 'Blade Runner 2049 (2017)', MediaType: 'Movie', Year: 2017 }
        ])).toBe(true);
    });
});
