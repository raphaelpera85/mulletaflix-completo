export function updateScrollButtonElements(
    previousButton: HTMLButtonElement,
    nextButton: HTMLButtonElement,
    scrollSize: number,
    scrollPos: number,
    scrollWidth: number,
    isRtl: boolean
): void {
    const localeAwarePos = isRtl ? scrollPos * -1 : scrollPos;
    const hasOverflow = scrollWidth > scrollSize + 20;

    previousButton.classList.toggle('hide', !hasOverflow);
    nextButton.classList.toggle('hide', !hasOverflow);
    previousButton.disabled = localeAwarePos <= 0;
    nextButton.disabled = scrollWidth > 0 && localeAwarePos + scrollSize >= scrollWidth;
}
