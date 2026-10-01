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
    const isPrevDisabled = localeAwarePos <= 0;
    const isNextDisabled = scrollWidth > 0 && localeAwarePos + scrollSize >= scrollWidth;
    previousButton.disabled = isPrevDisabled;
    nextButton.disabled = isNextDisabled;
    previousButton.setAttribute('aria-disabled', String(isPrevDisabled));
    nextButton.setAttribute('aria-disabled', String(isNextDisabled));
}
