import { describe, expect, it, vi } from 'vitest';
import { bindEventsToHlsPlayer } from './htmlMediaHelper';

type HlsEventHandler = (...args: unknown[]) => void;

describe('bindEventsToHlsPlayer', () => {
    it('starts the media element after HLS reports a parsed manifest', async () => {
        const handlers = new Map<string, HlsEventHandler>();
        const hls = {
            on: vi.fn((event: string, handler: HlsEventHandler) => handlers.set(event, handler))
        };
        const mediaElement = {
            play: vi.fn().mockResolvedValue(undefined),
            addEventListener: vi.fn()
        } as unknown as HTMLMediaElement;
        const resolve = vi.fn();

        bindEventsToHlsPlayer({}, hls, mediaElement, vi.fn(), resolve);
        handlers.get('hlsManifestParsed')?.();
        await vi.waitFor(() => expect(resolve).toHaveBeenCalledOnce());

        expect(mediaElement.play).toHaveBeenCalledOnce();
    });

    it('rejects startup on an unrecoverable HLS error', () => {
        const handlers = new Map<string, HlsEventHandler>();
        const hls = {
            on: vi.fn((event: string, handler: HlsEventHandler) => handlers.set(event, handler)),
            destroy: vi.fn()
        };
        const reject = vi.fn();

        bindEventsToHlsPlayer({}, hls, {} as HTMLMediaElement, vi.fn(), vi.fn(), reject);
        handlers.get('hlsError')?.({}, { type: 'other', fatal: true });

        expect(hls.destroy).toHaveBeenCalledOnce();
        expect(reject).toHaveBeenCalledOnce();
    });
});
