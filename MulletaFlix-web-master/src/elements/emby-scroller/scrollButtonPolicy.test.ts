import { describe, expect, it } from 'vitest';
import { shouldEnableScrollButtons } from './scrollButtonPolicy';

describe('shouldEnableScrollButtons', () => {
    it('allows rows to force controls on touch-capable desktop browsers', () => {
        expect(shouldEnableScrollButtons({
            horizontal: true,
            setting: 'true',
            desktop: true,
            touch: true
        })).toBe(true);
    });

    it('keeps the default desktop behavior', () => {
        expect(shouldEnableScrollButtons({
            horizontal: true,
            setting: null,
            desktop: true,
            touch: false
        })).toBe(true);
    });

    it('does not create controls for vertical scrollers or an explicit opt-out', () => {
        expect(shouldEnableScrollButtons({
            horizontal: false,
            setting: 'true',
            desktop: true,
            touch: false
        })).toBe(false);
        expect(shouldEnableScrollButtons({
            horizontal: true,
            setting: 'false',
            desktop: true,
            touch: false
        })).toBe(false);
    });
});
