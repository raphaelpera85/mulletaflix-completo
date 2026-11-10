import { describe, expect, it } from 'vitest';
import { shouldAdvanceAfterPlayback } from './shouldAdvanceAfterPlayback';

describe('shouldAdvanceAfterPlayback', () => {
    it('continues an intro into its next video even when video autoplay is disabled', () => {
        expect(shouldAdvanceAfterPlayback({
            hasNextItem: true,
            isIntro: true,
            nextMediaType: 'Video',
            enableNextEpisodeAutoPlay: false
        })).toBe(true);
    });

    it('does not autoplay a regular next video when video autoplay is disabled', () => {
        expect(shouldAdvanceAfterPlayback({
            hasNextItem: true,
            isIntro: false,
            nextMediaType: 'Video',
            enableNextEpisodeAutoPlay: false
        })).toBe(false);
    });

    it('continues non-video queue items without video autoplay', () => {
        expect(shouldAdvanceAfterPlayback({
            hasNextItem: true,
            isIntro: false,
            nextMediaType: 'Audio',
            enableNextEpisodeAutoPlay: false
        })).toBe(true);
    });

    it('never advances when there is no next item', () => {
        expect(shouldAdvanceAfterPlayback({
            hasNextItem: false,
            isIntro: true,
            nextMediaType: 'Video',
            enableNextEpisodeAutoPlay: true
        })).toBe(false);
    });
});
