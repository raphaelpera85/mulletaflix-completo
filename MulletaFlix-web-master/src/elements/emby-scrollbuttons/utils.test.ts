import { describe, expect, it, vi } from 'vitest';
import type ScrollerFactory from 'lib/scroller';

import { ScrollDirection, scrollerItemSlideIntoView } from './utils';

describe('scrollerItemSlideIntoView', () => {
    it('moves the scroller instance to the next visible window', () => {
        const frame = document.createElement('div');
        Object.defineProperty(frame, 'offsetWidth', { value: 600 });

        const slider = document.createElement('div');
        for (let index = 0; index < 12; index++) {
            const item = document.createElement('div');
            Object.defineProperty(item, 'offsetWidth', { value: 100 });
            slider.append(item);
        }

        const slideTo = vi.fn();
        const scroller = {
            getScrollFrame: () => frame,
            getScrollSlider: () => slider,
            slideTo
        } as unknown as InstanceType<typeof ScrollerFactory>;

        scrollerItemSlideIntoView({
            direction: ScrollDirection.RIGHT,
            scroller,
            scrollState: { scrollPos: 0 }
        });

        expect(slideTo).toHaveBeenCalledWith(600, false, undefined);
    });
});
