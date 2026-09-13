import Worker from './blurhash.worker.ts';
import * as lazyLoader from '../lazyLoader/lazyLoaderIntersectionObserver';
import * as userSettings from '../../scripts/settings/userSettings';
import './style.scss';

const worker = new Worker();
interface BlurhashWorkerData {
    pixels: Uint8ClampedArray;
    hsh: string;
    width: number;
    height: number;
}

const targetDic: Record<string, HTMLElement[]> = {};
const INITIAL_PRIORITY_IMAGE_LIMIT = 48;
const INITIAL_PRIORITY_VIEWPORT_MARGIN = 2400;

worker.addEventListener(
    'message',
    ({ data: { pixels, hsh, width, height } }: MessageEvent<BlurhashWorkerData>) => {
        const elems = targetDic[hsh];
        if (elems?.length) {
            for (const elem of elems) {
                drawBlurhash(elem, pixels, width, height);
            }
            delete targetDic[hsh];
        }
    }
);

export function lazyImage(elem: HTMLElement, source = elem.getAttribute('data-src') ?? undefined): void {
    if (!source) {
        return;
    }

    fillImageElement(elem, source);
}

function drawBlurhash(target: HTMLElement, pixels: Uint8ClampedArray, width: number, height: number): void {
    const canvas = document.createElement('canvas');
    canvas.setAttribute('aria-hidden', 'true');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    const imgData = ctx!.createImageData(width, height);

    imgData.data.set(pixels);
    ctx!.putImageData(imgData, 0, 0);

    requestAnimationFrame(() => {
        canvas.classList.add('blurhash-canvas');
        target.parentNode?.insertBefore(canvas, target);
        target.classList.add('blurhashed');
        target.removeAttribute('data-blurhash');
    });
}

function itemBlurhashing(target: HTMLElement, hash: string): void {
    try {
        const width = 20;
        const height = 20;
        targetDic[hash] = (targetDic[hash] || []).filter((item) => item !== target);
        targetDic[hash].push(target);

        worker.postMessage({
            hash,
            width,
            height
        });
    } catch (err) {
        console.error(err);
        target.classList.add('non-blurhashable');
        return;
    }
}

export function fillImage(entry: IntersectionObserverEntry): void {
    if (!entry) {
        throw new Error('entry cannot be null');
    }

    const target = entry.target as HTMLElement;
    const source = target.getAttribute('data-src');

    if (entry.isIntersecting) {
        if (source) {
            fillImageElement(target, source);
        }
    } else if (!source) {
        emptyImageElement(target);
    }
}

function onAnimationEnd(event: AnimationEvent): void {
    const elem = event.target as HTMLElement;
    requestAnimationFrame(() => {
        const canvas = elem.previousSibling;
        if (elem.classList.contains('blurhashed') && canvas instanceof HTMLCanvasElement) {
            canvas.classList.add('lazy-hidden');
        }

        elem.parentNode?.querySelector('.cardPadder')?.classList.add('lazy-hidden-children');
    });
    elem.removeEventListener('animationend', onAnimationEnd);
}

function fillImageElement(elem: HTMLElement, url: string | undefined): void {
    if (url === undefined) {
        throw new TypeError('url cannot be undefined');
    }

    if (elem.getAttribute('data-loading-src') === url || elem.style.backgroundImage.includes(url) || elem.getAttribute('src') === url) {
        return;
    }

    elem.setAttribute('data-loading-src', url);

    const preloaderImg = new Image();
    preloaderImg.decoding = 'async';
    preloaderImg.fetchPriority = elem.getAttribute('data-priority') === 'high' ? 'high' : 'auto';
    // ponytail: IntersectionObserver already gates when a card may load.
    preloaderImg.loading = 'eager';
    preloaderImg.src = url;

    elem.classList.add('lazy-hidden');
    elem.addEventListener('animationend', onAnimationEnd);

    preloaderImg.addEventListener('load', () => {
        requestAnimationFrame(() => {
            if (elem.tagName !== 'IMG') {
                elem.style.backgroundImage = "url('" + url + "')";
            } else {
                elem.setAttribute('src', url);
            }
            elem.removeAttribute('data-src');
            elem.removeAttribute('data-loading-src');
            elem.removeAttribute('data-priority');

            if (userSettings.enableFastFadein()) {
                elem.classList.add('lazy-image-fadein-fast');
            } else {
                elem.classList.add('lazy-image-fadein');
            }
            elem.classList.remove('lazy-hidden');
        });
    });

    preloaderImg.addEventListener('error', () => {
        elem.removeAttribute('data-loading-src');
        elem.removeAttribute('data-priority');
        elem.classList.remove('lazy-hidden');
    });
}

function queueBlurhash(target: HTMLElement, hash: string): void {
    if (window.requestIdleCallback) {
        window.requestIdleCallback(() => itemBlurhashing(target, hash), { timeout: 0 });
        return;
    }

    window.setTimeout(() => itemBlurhashing(target, hash), 0);
}

function emptyImageElement(elem: HTMLElement): void {
    elem.removeEventListener('animationend', onAnimationEnd);
    const canvas = elem.previousSibling;
    if (canvas instanceof HTMLCanvasElement) {
        canvas.classList.remove('lazy-hidden');
    }

    elem.parentNode?.querySelector('.cardPadder')?.classList.remove('lazy-hidden-children');

    let url: string;

    if (elem.tagName !== 'IMG') {
        url = elem.style.backgroundImage.slice(4, -1).replace(/"/g, '');
        elem.style.backgroundImage = 'none';
    } else {
        url = elem.getAttribute('src') ?? '';
        elem.setAttribute('src', '');
    }
    elem.setAttribute('data-src', url);

    elem.classList.remove('lazy-image-fadein-fast', 'lazy-image-fadein');
    elem.classList.add('lazy-hidden');
}

function isNearInitialViewport(elem: HTMLElement): boolean {
    const rect = elem.getBoundingClientRect();
    const viewportHeight = window.innerHeight || document.documentElement.clientHeight;
    const viewportWidth = window.innerWidth || document.documentElement.clientWidth;

    return rect.bottom >= -INITIAL_PRIORITY_VIEWPORT_MARGIN
        && rect.top <= viewportHeight + INITIAL_PRIORITY_VIEWPORT_MARGIN
        && rect.right >= 0
        && rect.left <= viewportWidth;
}

function fillInitialPriorityImages(lazyElems: HTMLElement[]): void {
    let loadedCount = 0;

    for (const lazyElem of lazyElems) {
        if (loadedCount >= INITIAL_PRIORITY_IMAGE_LIMIT) {
            return;
        }

        const source = lazyElem.getAttribute('data-src');
        if (!source || !isNearInitialViewport(lazyElem)) {
            continue;
        }

        lazyElem.setAttribute('data-priority', 'high');
        fillImageElement(lazyElem, source);
        loadedCount++;
    }
}

export function lazyChildren(elem: Element): void {
    const lazyElems = Array.from(elem.querySelectorAll<HTMLElement>('.lazy'));

    if (userSettings.enableBlurhash()) {
        for (const lazyElem of lazyElems) {
            const blurhashstr = lazyElem.getAttribute('data-blurhash');
            if (blurhashstr && !lazyElem.classList.contains('blurhashed') && isNearInitialViewport(lazyElem)) {
                queueBlurhash(lazyElem, blurhashstr);
            } else if (!blurhashstr && !lazyElem.classList.contains('blurhashed')) {
                lazyElem.classList.add('non-blurhashable');
            }
        }
    }

    fillInitialPriorityImages(lazyElems);
    lazyLoader.lazyChildren(elem, fillImage);
}

export function getPrimaryImageAspectRatio(items: Array<{ PrimaryImageAspectRatio?: number | null }>): number | null {
    const values: number[] = [];

    for (let i = 0, length = items.length; i < length; i++) {
        const ratio = items[i].PrimaryImageAspectRatio || 0;

        if (!ratio) {
            continue;
        }

        values[values.length] = ratio;
    }

    if (!values.length) {
        return null;
    }

    values.sort(function (a, b) {
        return a - b;
    });

    const half = Math.floor(values.length / 2);
    let result: number;

    if (values.length % 2) {
        result = values[half];
    } else {
        result = (values[half - 1] + values[half]) / 2.0;
    }

    const aspect2x3 = 2 / 3;
    if (Math.abs(aspect2x3 - result) <= 0.15) {
        return aspect2x3;
    }

    const aspect16x9 = 16 / 9;
    if (Math.abs(aspect16x9 - result) <= 0.2) {
        return aspect16x9;
    }

    if (Math.abs(1 - result) <= 0.15) {
        return 1;
    }

    const aspect4x3 = 4 / 3;
    if (Math.abs(aspect4x3 - result) <= 0.15) {
        return aspect4x3;
    }

    return result;
}

export function fillImages(elems: IntersectionObserverEntry[]): void {
    for (let i = 0, length = elems.length; i < length; i++) {
        const elem = elems[i];
        fillImage(elem);
    }
}

export function setLazyImage(element: HTMLElement, url: string): void {
    element.classList.add('lazy');
    element.setAttribute('data-src', url);
    lazyImage(element);
}

export default {
    setLazyImage,
    fillImages,
    fillImage,
    lazyImage,
    lazyChildren,
    getPrimaryImageAspectRatio
};
