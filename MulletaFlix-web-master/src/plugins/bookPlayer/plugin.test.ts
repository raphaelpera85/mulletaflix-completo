import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { resolveBookPlayerFontSize, resolveBookPlayerTheme } from './plugin';
import appSettings from '../../scripts/settings/appSettings';
import * as userSettings from '../../scripts/settings/userSettings';

describe('book reader preference persistence', () => {
    beforeEach(() => {
        localStorage.clear();
    });

    afterEach(() => {
        vi.restoreAllMocks();
        localStorage.clear();
    });

    describe('resolveBookPlayerTheme', () => {
        it('falls back to dark (default) when nothing is saved', () => {
            expect(resolveBookPlayerTheme(null, null)).toBe('dark');
            expect(resolveBookPlayerTheme(undefined, undefined)).toBe('dark');
        });

        it('falls back to the app theme when nothing is saved for the reader', () => {
            expect(resolveBookPlayerTheme(null, 'light')).toBe('light');
            expect(resolveBookPlayerTheme(null, 'dark')).toBe('dark');
        });

        it('uses the saved reader theme when it is a valid value', () => {
            expect(resolveBookPlayerTheme('sepia', 'dark')).toBe('sepia');
            expect(resolveBookPlayerTheme('light', 'dark')).toBe('light');
        });

        it('falls back safely when the saved value is invalid/corrupted', () => {
            expect(resolveBookPlayerTheme('neon-pink', 'light')).toBe('light');
            expect(resolveBookPlayerTheme('{"corrupted":true}', null)).toBe('dark');
            expect(resolveBookPlayerTheme('', 'light')).toBe('light');
        });
    });

    describe('resolveBookPlayerFontSize', () => {
        it('falls back to medium (default) when nothing is saved', () => {
            expect(resolveBookPlayerFontSize(null)).toBe('medium');
            expect(resolveBookPlayerFontSize(undefined)).toBe('medium');
        });

        it('uses the saved reader font size when it is a valid value', () => {
            expect(resolveBookPlayerFontSize('x-large')).toBe('x-large');
            expect(resolveBookPlayerFontSize('small')).toBe('small');
        });

        it('falls back safely when the saved value is invalid/corrupted', () => {
            expect(resolveBookPlayerFontSize('huge')).toBe('medium');
            expect(resolveBookPlayerFontSize('<<garbled>>')).toBe('medium');
        });
    });

    describe('end-to-end persistence via userSettings/appSettings', () => {
        it('reloads a previously saved theme and font size', () => {
            userSettings.bookPlayerTheme('sepia');
            userSettings.bookPlayerFontSize('large');

            // Simulate reopening the reader: read the persisted values back
            // exactly the way the BookPlayer constructor does.
            const reloadedTheme = resolveBookPlayerTheme(userSettings.bookPlayerTheme(), userSettings.theme());
            const reloadedFontSize = resolveBookPlayerFontSize(userSettings.bookPlayerFontSize());

            expect(reloadedTheme).toBe('sepia');
            expect(reloadedFontSize).toBe('large');
        });

        it('falls back to defaults when nothing was ever saved', () => {
            expect(userSettings.bookPlayerTheme()).toBeNull();
            expect(userSettings.bookPlayerFontSize()).toBeNull();

            const theme = resolveBookPlayerTheme(userSettings.bookPlayerTheme(), userSettings.theme());
            const fontSize = resolveBookPlayerFontSize(userSettings.bookPlayerFontSize());

            expect(theme).toBe('dark');
            expect(fontSize).toBe('medium');
        });

        it('falls back safely when the raw localStorage value is corrupted', () => {
            // Write a garbled value directly, bypassing the normal setter,
            // to simulate storage that got corrupted out-of-band.
            localStorage.setItem('bookPlayerTheme', '\u0000invalid-json{{{');
            localStorage.setItem('bookPlayerFontSize', 'not-a-size');

            const theme = resolveBookPlayerTheme(userSettings.bookPlayerTheme(), userSettings.theme());
            const fontSize = resolveBookPlayerFontSize(userSettings.bookPlayerFontSize());

            expect(theme).toBe('dark');
            expect(fontSize).toBe('medium');
        });

        it('never throws and returns null when localStorage itself throws (quota/corruption)', () => {
            const getSpy = vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
                throw new Error('simulated corrupted storage');
            });

            expect(() => appSettings.get('bookPlayerTheme')).not.toThrow();
            expect(appSettings.get('bookPlayerTheme')).toBeNull();

            getSpy.mockRestore();
        });

        it('never throws when persisting fails (quota exceeded/corrupted storage)', () => {
            const setSpy = vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
                throw new Error('simulated quota exceeded');
            });

            expect(() => userSettings.bookPlayerTheme('sepia')).not.toThrow();

            setSpy.mockRestore();
        });
    });
});
