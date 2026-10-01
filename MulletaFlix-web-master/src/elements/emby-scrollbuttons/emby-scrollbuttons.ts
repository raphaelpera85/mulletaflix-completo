import './emby-scrollbuttons.scss';
import 'webcomponents.js/webcomponents-lite';
import '../emby-button/paper-icon-button-light';
import globalize from 'lib/globalize';
import ScrollerFactory from 'lib/scroller';
import { ScrollDirection, scrollerItemSlideIntoView } from './utils';
import { observeScrollSize } from './observeScrollSize';
import { updateScrollButtonElements } from './updateScrollButtonElements';

interface ScrollButtonsElement extends HTMLDivElement {
    createdCallback(): void;
    attachedCallback(): void;
    detachedCallback(): void;
    scroller: ScrollButtonsScrollerElement | null;
    scrollHandler: (() => void) | null;
    scrollButtonsLeft: HTMLButtonElement | null;
    scrollButtonsRight: HTMLButtonElement | null;
    resizeObserver: ResizeObserver | null;
}

interface ScrollButtonsScrollerElement extends HTMLDivElement {
    getScrollPosition(): number;
    getScrollSize(): number;
    getScrollSlider(): HTMLElement;
    addScrollEventListener(fn: () => void, options?: AddEventListenerOptions): void;
    removeScrollEventListener(fn: () => void, options?: EventListenerOptions): void;
    scroller?: InstanceType<typeof ScrollerFactory> | null;
    slideTo?(position: number, immediate?: boolean): void;
    scrollToPosition?(position: number): void;
}

const EmbyScrollButtonsPrototype = Object.create(HTMLDivElement.prototype) as ScrollButtonsElement;

EmbyScrollButtonsPrototype.createdCallback = function (): void {
    // no-op
};

function getScrollButtonHtml(direction: 'left' | 'right'): string {
    let html = '';
    const icon: string = direction === 'left' ? 'chevron_left' : 'chevron_right';
    const title: string = direction === 'left' ? globalize.translate('Previous') : globalize.translate('Next') ;

    html += `<button type="button" is="paper-icon-button-light" data-ripple="false" data-direction="${direction}" title="${title}" aria-label="${title}" class="emby-scrollbuttons-button">`;
    html += '<span class="material-icons ' + icon + '" aria-hidden="true"></span>';
    html += '</button>';

    return html;
}

function getScrollPosition(parent: ScrollButtonsScrollerElement): number {
    if (parent.getScrollPosition) {
        return parent.getScrollPosition();
    }

    return 0;
}

function getScrollWidth(parent: ScrollButtonsScrollerElement): number {
    if (parent.getScrollSize) {
        return parent.getScrollSize();
    }

    return 0;
}

function updateScrollButtons(scrollButtons: ScrollButtonsElement, scrollSize: number, scrollPos: number, scrollWidth: number): void {
    // Leave a small tolerance for subpixel rounding at the end of the row.
    updateScrollButtonElements(
        scrollButtons.scrollButtonsLeft!,
        scrollButtons.scrollButtonsRight!,
        scrollSize,
        scrollPos,
        scrollWidth,
        globalize.getIsElementRTL(scrollButtons)
    );
}

function onScroll(this: ScrollButtonsElement): void {
    const scroller = this.scroller!;

    const scrollSize: number = getScrollSize(scroller);
    const scrollPos: number = getScrollPosition(scroller);
    const scrollWidth: number = getScrollWidth(scroller);

    updateScrollButtons(this, scrollSize, scrollPos, scrollWidth);
}

function getScrollSize(elem: ScrollButtonsScrollerElement): number {
    return elem.getAttribute('data-horizontal') === 'false' ? elem.clientHeight : elem.clientWidth;
}

function onScrollButtonClick(this: HTMLElement): void {
    const direction = this.getAttribute('data-direction') as 'left' | 'right';
    const scrollElement = this.parentNode!.nextSibling as unknown as ScrollButtonsScrollerElement;
    const scrollPosition: number = getScrollPosition(scrollElement);
    scrollerItemSlideIntoView({
        direction: direction === 'left' ? ScrollDirection.LEFT : ScrollDirection.RIGHT,
        scroller: scrollElement.scroller ?? null,
        scrollState: {
            scrollPos: scrollPosition
        }
    });
}

EmbyScrollButtonsPrototype.attachedCallback = function (this: ScrollButtonsElement): void {
    const scroller = this.nextSibling as unknown as ScrollButtonsScrollerElement;
    this.scroller = scroller;

    const parent = this.parentNode as HTMLElement;
    parent.classList.add('emby-scroller-container');

    this.innerHTML = getScrollButtonHtml('left') + getScrollButtonHtml('right');

    const buttons = this.querySelectorAll('.emby-scrollbuttons-button');
    (buttons[0] as HTMLElement).addEventListener('click', onScrollButtonClick);
    (buttons[1] as HTMLElement).addEventListener('click', onScrollButtonClick);
    this.scrollButtonsLeft = buttons[0] as HTMLButtonElement;
    this.scrollButtonsRight = buttons[1] as HTMLButtonElement;

    const scrollHandler = onScroll.bind(this) as () => void;
    this.scrollHandler = scrollHandler;
    scroller.addScrollEventListener(scrollHandler, {
        capture: false,
        passive: true
    });

    // Result cards can be mounted after the scroller and its buttons. A single
    // initial measurement then sees an empty slider and leaves the controls
    // hidden until the user scrolls (which they cannot do if the controls are
    // the only visible navigation affordance). Re-measure when either the
    // viewport or its content changes size.
    this.resizeObserver = observeScrollSize(
        scroller as unknown as Element,
        scroller.getScrollSlider(),
        () => scroller.scroller?.reload(),
        scrollHandler
    );

    requestAnimationFrame(() => {
        this.scrollHandler!();
    });
};

EmbyScrollButtonsPrototype.detachedCallback = function (this: ScrollButtonsElement): void {
    const parent = this.scroller;
    this.scroller = null;

    const scrollHandler = this.scrollHandler;
    if (parent && scrollHandler) {
        parent.removeScrollEventListener(scrollHandler, {
            capture: false
        } as EventListenerOptions);
    }

    this.resizeObserver?.disconnect();
    this.resizeObserver = null;

    this.scrollHandler = null;
    this.scrollButtonsLeft = null;
    this.scrollButtonsRight = null;
};

document.registerElement('emby-scrollbuttons', {
    prototype: EmbyScrollButtonsPrototype,
    extends: 'div'
});
