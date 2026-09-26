import { describe, expect, it, vi } from 'vitest';
import { playIntroThenMain } from './playIntroThenMain';

describe('playIntroThenMain', () => {
    it('starts the main item when intro playback rejects', async () => {
        const introError = new Error('intro source is unavailable');
        const playIntro = vi.fn().mockRejectedValue(introError);
        const onIntroError = vi.fn();
        const playMain = vi.fn().mockResolvedValue('main playback started');

        await expect(playIntroThenMain(playIntro, onIntroError, playMain)).resolves.toBe('main playback started');
        expect(onIntroError).toHaveBeenCalledWith(introError);
        expect(playMain).toHaveBeenCalledOnce();
    });

    it('does not start the main item when the intro starts successfully', async () => {
        const playIntro = vi.fn().mockResolvedValue(undefined);
        const onIntroError = vi.fn();
        const playMain = vi.fn();

        await expect(playIntroThenMain(playIntro, onIntroError, playMain)).resolves.toBeUndefined();
        expect(onIntroError).not.toHaveBeenCalled();
        expect(playMain).not.toHaveBeenCalled();
    });

    it('propagates a main item playback failure', async () => {
        const mainError = new Error('main source is unavailable');
        const playMain = vi.fn().mockRejectedValue(mainError);

        await expect(playIntroThenMain(
            () => Promise.reject(new Error('intro failed')),
            vi.fn(),
            playMain
        )).rejects.toBe(mainError);
    });
});
