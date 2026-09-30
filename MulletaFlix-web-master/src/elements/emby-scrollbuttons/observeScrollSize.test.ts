import { afterEach, describe, expect, it, vi } from 'vitest';

import { observeScrollSize } from './observeScrollSize';
import { updateScrollButtonElements } from './updateScrollButtonElements';

describe('observeScrollSize', () => {
    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('recalculates controls when the viewport or slider content changes', () => {
        const observed: Element[] = [];
        const disconnect = vi.fn();
        let resizeCallback: ResizeObserverCallback | undefined;

        class ResizeObserverMock {
            constructor(callback: ResizeObserverCallback) {
                resizeCallback = callback;
            }

            observe(element: Element) {
                observed.push(element);
            }

            disconnect = disconnect;

            unobserve() {
                // Not used by this helper.
            }
        }

        vi.stubGlobal('ResizeObserver', ResizeObserverMock);

        const viewport = document.createElement('div');
        const slider = document.createElement('div');
        const events: string[] = [];
        const previousButton = document.createElement('button');
        const nextButton = document.createElement('button');
        let cachedScrollWidth = 0;
        let scrollPosition = 0;
        const reloadScroller = vi.fn(() => {
            events.push('reload');
            cachedScrollWidth = 800;
        });
        const onResize = vi.fn(() => {
            events.push('measure');
            updateScrollButtonElements(previousButton, nextButton, 200, scrollPosition, cachedScrollWidth, false);
        });
        const observer = observeScrollSize(viewport, slider, reloadScroller, onResize);

        expect(observed).toEqual([viewport, slider]);
        resizeCallback?.([], observer);
        expect(reloadScroller).toHaveBeenCalledOnce();
        expect(onResize).toHaveBeenCalledOnce();
        expect(events).toEqual(['reload', 'measure']);
        expect(previousButton.classList.contains('hide')).toBe(false);
        expect(nextButton.disabled).toBe(false);

        scrollPosition = 600;
        resizeCallback?.([], observer);
        expect(previousButton.disabled).toBe(false);
        expect(nextButton.disabled).toBe(true);

        observer.disconnect();
        expect(disconnect).toHaveBeenCalledOnce();
    });
});
