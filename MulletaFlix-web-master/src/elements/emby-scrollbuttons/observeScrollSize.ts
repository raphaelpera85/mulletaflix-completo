/** Reload cached scroller measurements and refresh its controls after layout changes. */
export function observeScrollSize(
    viewport: Element,
    slider: Element,
    reloadScroller: () => void,
    onResize: () => void
): ResizeObserver {
    const observer = new ResizeObserver(() => {
        reloadScroller();
        onResize();
    });
    observer.observe(viewport);
    observer.observe(slider);
    return observer;
}
