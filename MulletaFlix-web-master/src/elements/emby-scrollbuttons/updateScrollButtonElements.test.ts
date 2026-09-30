import { describe, expect, it } from 'vitest';

import { updateScrollButtonElements } from './updateScrollButtonElements';

describe('updateScrollButtonElements', () => {
    it('shows both controls at the start and disables only the previous control', () => {
        const previousButton = document.createElement('button');
        const nextButton = document.createElement('button');

        updateScrollButtonElements(previousButton, nextButton, 200, 0, 800, false);

        expect(previousButton.classList.contains('hide')).toBe(false);
        expect(nextButton.classList.contains('hide')).toBe(false);
        expect(previousButton.disabled).toBe(true);
        expect(nextButton.disabled).toBe(false);
    });

    it('disables the next control at the end of the row', () => {
        const previousButton = document.createElement('button');
        const nextButton = document.createElement('button');

        updateScrollButtonElements(previousButton, nextButton, 200, 600, 800, false);

        expect(previousButton.disabled).toBe(false);
        expect(nextButton.disabled).toBe(true);
    });

    it('hides both controls when content fits and respects RTL positions', () => {
        const previousButton = document.createElement('button');
        const nextButton = document.createElement('button');

        updateScrollButtonElements(previousButton, nextButton, 200, 0, 210, false);
        expect(previousButton.classList.contains('hide')).toBe(true);
        expect(nextButton.classList.contains('hide')).toBe(true);

        updateScrollButtonElements(previousButton, nextButton, 200, -100, 800, true);
        expect(previousButton.disabled).toBe(false);
        expect(nextButton.disabled).toBe(false);
    });
});
