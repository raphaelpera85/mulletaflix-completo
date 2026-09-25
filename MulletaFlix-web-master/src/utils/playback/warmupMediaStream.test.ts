import { describe, expect, it, vi } from 'vitest';
import { warmupMediaStream } from './warmupMediaStream';

describe('warmupMediaStream', () => {
    it('requests a small range and consumes only the first chunk on success', async () => {
        const cancel = vi.fn().mockResolvedValue(undefined);
        const read = vi.fn().mockResolvedValue({ done: false, value: new Uint8Array([1]) });
        const fetcher = vi.fn().mockResolvedValue({
            ok: true,
            status: 206,
            body: { getReader: () => ({ read, cancel }) }
        } as unknown as Response);

        const response = await warmupMediaStream('/stream', fetcher);

        expect(response.status).toBe(206);
        expect(fetcher).toHaveBeenCalledWith('/stream', {
            method: 'GET',
            headers: { Range: 'bytes=0-65535' }
        });
        expect(read).toHaveBeenCalledOnce();
        expect(cancel).toHaveBeenCalledOnce();
    });

    it('rejects HTTP errors so callers do not cache an unusable media source', async () => {
        const cancelBody = vi.fn().mockResolvedValue(undefined);
        const fetcher = vi.fn().mockResolvedValue({
            ok: false,
            status: 401,
            body: { cancel: cancelBody }
        } as unknown as Response);

        await expect(warmupMediaStream('/stream', fetcher)).rejects.toThrow('HTTP 401');
        expect(cancelBody).toHaveBeenCalledOnce();
    });
});
