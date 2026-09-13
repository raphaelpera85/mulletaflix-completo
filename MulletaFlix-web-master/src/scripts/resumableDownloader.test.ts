import { describe, expect, it } from 'vitest';

import { parseContentRange } from './resumableDownloader';

describe('parseContentRange', () => {
    it('parses a complete byte range', () => {
        expect(parseContentRange('bytes 4194304-8388607/12000000')).toEqual({
            start: 4194304,
            total: 12000000
        });
    });

    it('allows an unknown total', () => {
        expect(parseContentRange('bytes 0-10/*')).toEqual({ start: 0 });
    });

    it('rejects malformed ranges', () => {
        expect(parseContentRange('items 0-10/20')).toBeNull();
        expect(parseContentRange('bytes invalid')).toBeNull();
        expect(parseContentRange(null)).toBeNull();
    });
});
