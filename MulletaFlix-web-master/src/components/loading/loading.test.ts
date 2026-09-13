import { describe, expect, it } from 'vitest';

import loading, { withLoading } from './loading';

describe('loading.withLoading', () => {
    it('hides the indicator after a successful operation', async () => {
        await withLoading(async () => 'done');

        const loader = document.querySelector('.docspinner');
        expect(loader).not.toBeNull();
        expect(loader?.classList.contains('mdlSpinnerActive')).toBe(false);
    });

    it('hides the indicator when the operation fails and preserves the error', async () => {
        const error = new Error('request failed');

        await expect(withLoading(async () => {
            throw error;
        })).rejects.toBe(error);

        expect(document.querySelector('.docspinner')?.classList.contains('mdlSpinnerActive')).toBe(false);
    });

    it('keeps the indicator active until all concurrent operations finish', async () => {
        let resolveFirst!: () => void;
        let resolveSecond!: () => void;
        const first = withLoading(() => new Promise<void>(resolve => {
            resolveFirst = resolve;
        }));
        const second = withLoading(() => new Promise<void>(resolve => {
            resolveSecond = resolve;
        }));

        await Promise.resolve();
        resolveFirst();
        await first;

        expect(document.querySelector('.docspinner')?.classList.contains('mdlSpinnerActive')).toBe(true);

        resolveSecond();
        await second;
        expect(document.querySelector('.docspinner')?.classList.contains('mdlSpinnerActive')).toBe(false);
    });

    it('ignores a legacy hide while a managed operation is still active', async () => {
        let resolveOperation!: () => void;
        const operation = withLoading(() => new Promise<void>(resolve => {
            resolveOperation = resolve;
        }));

        await Promise.resolve();
        const loader = document.querySelector('.docspinner');
        expect(loader?.classList.contains('mdlSpinnerActive')).toBe(true);

        // Compatibility code may still call loading.hide() during the request.
        loading.hide();
        expect(loader?.classList.contains('mdlSpinnerActive')).toBe(true);

        resolveOperation();
        await operation;
        expect(loader?.classList.contains('mdlSpinnerActive')).toBe(false);
    });
});
