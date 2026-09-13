import dom from '../utils/dom';
import Events from '../utils/events';

function getTouches(e: TouchEvent): TouchList {
    return e.changedTouches || e.targetTouches || e.touches;
}

function getSwipeDirection(
    deltaX: number,
    deltaY: number,
    swipeXThreshold: number,
    swipeYThreshold: number,
    swipeXMaxY: number,
    thresholdYMet: boolean
): string | null {
    if (deltaX > swipeXThreshold && Math.abs(deltaY) < swipeXMaxY) {
        return 'swiperight';
    }

    if (deltaX < (0 - swipeXThreshold) && Math.abs(deltaY) < swipeXMaxY) {
        return 'swipeleft';
    }

    if ((deltaY < (0 - swipeYThreshold) || thresholdYMet) && Math.abs(deltaX) < swipeXMaxY) {
        return 'swipeup';
    }

    if ((deltaY > swipeYThreshold || thresholdYMet) && Math.abs(deltaX) < swipeXMaxY) {
        return 'swipedown';
    }

    return null;
}

function triggerSwipe(
    helper: TouchHelper,
    direction: string,
    target: EventTarget,
    details: Record<string, number | undefined>
): boolean {
    const isVerticalSwipe = direction === 'swipeup' || direction === 'swipedown';
    const eventArgs: unknown[] = [target];

    if (isVerticalSwipe) {
        eventArgs.push(details);
    }

    Events.trigger(helper, direction, eventArgs);
    return isVerticalSwipe;
}

interface TouchHelperOptions {
    swipeXThreshold?: number;
    swipeYThreshold?: number;
    ignoreTagNames?: string[];
    preventDefaultOnMove?: boolean;
    triggerOnMove?: boolean;
}

class TouchHelper {
    touchStart: ((e: TouchEvent) => void) | null;
    touchEnd: ((e: TouchEvent) => void) | null;
    elem: HTMLElement | null;

    constructor(elem: HTMLElement, options?: TouchHelperOptions) {
        const opts = options || {};
        let touchTarget: EventTarget | null | undefined;
        let touchStartX: number | undefined;
        let touchStartY: number | undefined;
        let lastDeltaX: number | null | undefined;
        let lastDeltaY: number | null | undefined;
        let thresholdYMet: boolean;
        const swipeXThreshold = opts.swipeXThreshold || 50;
        const swipeYThreshold = opts.swipeYThreshold || 50;
        const swipeXMaxY = 30;

        const excludeTagNames = opts.ignoreTagNames || [];

        const touchStart = function (e: TouchEvent): void {
            const touch = getTouches(e)[0];
            touchTarget = null;
            touchStartX = 0;
            touchStartY = 0;
            lastDeltaX = null;
            lastDeltaY = null;
            thresholdYMet = false;

            if (touch) {
                const currentTouchTarget = touch.target;

                if (dom.parentWithTag(currentTouchTarget as HTMLElement, excludeTagNames)) {
                    return;
                }

                touchTarget = currentTouchTarget;
                touchStartX = touch.clientX;
                touchStartY = touch.clientY;
            }
        };

        const touchEnd = (e: TouchEvent): void => {
            const isTouchMove = e.type === 'touchmove';

            if (touchTarget) {
                const touch = getTouches(e)[0];

                let deltaX: number;
                let deltaY: number;

                let clientX: number | undefined;
                let clientY: number | undefined;

                if (touch) {
                    clientX = touch.clientX || 0;
                    clientY = touch.clientY || 0;
                    deltaX = clientX - (touchStartX || 0);
                    deltaY = clientY - (touchStartY || 0);
                } else {
                    deltaX = 0;
                    deltaY = 0;
                }

                const currentDeltaX = lastDeltaX == null ? deltaX : (deltaX - lastDeltaX);
                const currentDeltaY = lastDeltaY == null ? deltaY : (deltaY - lastDeltaY);

                lastDeltaX = deltaX;
                lastDeltaY = deltaY;

                const swipeDirection = getSwipeDirection(deltaX, deltaY, swipeXThreshold, swipeYThreshold, swipeXMaxY, thresholdYMet);
                if (swipeDirection) {
                    thresholdYMet = triggerSwipe(this, swipeDirection, touchTarget, {
                        deltaY: deltaY,
                        deltaX: deltaX,
                        clientX: clientX,
                        clientY: clientY,
                        currentDeltaX: currentDeltaX,
                        currentDeltaY: currentDeltaY
                    });
                }

                if (isTouchMove && opts.preventDefaultOnMove) {
                    e.preventDefault();
                }
            }

            if (!isTouchMove) {
                touchTarget = null;
                touchStartX = 0;
                touchStartY = 0;
                lastDeltaX = null;
                lastDeltaY = null;
                thresholdYMet = false;
            }
        };

        this.touchStart = touchStart;
        this.touchEnd = touchEnd;

        dom.addEventListener(elem, 'touchstart', touchStart as EventListenerOrEventListenerObject, {
            passive: true
        });
        if (opts.triggerOnMove) {
            dom.addEventListener(elem, 'touchmove', touchEnd as EventListenerOrEventListenerObject, {
                passive: !opts.preventDefaultOnMove
            });
        }
        dom.addEventListener(elem, 'touchend', touchEnd as EventListenerOrEventListenerObject, {
            passive: true
        });
        dom.addEventListener(elem, 'touchcancel', touchEnd as EventListenerOrEventListenerObject, {
            passive: true
        });

        this.elem = elem;
    }

    destroy(): void {
        const elem = this.elem;

        if (elem) {
            const touchStart = this.touchStart;
            const touchEnd = this.touchEnd;

            dom.removeEventListener(elem, 'touchstart', touchStart as EventListenerOrEventListenerObject, {
                passive: true
            });
            dom.removeEventListener(elem, 'touchmove', touchEnd as EventListenerOrEventListenerObject, {
                passive: true
            });
            dom.removeEventListener(elem, 'touchend', touchEnd as EventListenerOrEventListenerObject, {
                passive: true
            });
            dom.removeEventListener(elem, 'touchcancel', touchEnd as EventListenerOrEventListenerObject, {
                passive: true
            });
        }

        this.touchStart = null;
        this.touchEnd = null;

        this.elem = null;
    }
}

export default TouchHelper;
