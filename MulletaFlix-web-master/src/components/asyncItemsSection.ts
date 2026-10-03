import globalize from 'lib/globalize';

interface SectionLoaderOptions<TResult, TItem> {
    section: HTMLElement;
    loadItems: () => Promise<TResult>;
    selectItems: (result: TResult) => TItem[];
    renderItems: (items: TItem[]) => void;
    label: string;
}

export function loadSectionItems<TResult, TItem>({
    section,
    loadItems,
    selectItems,
    renderItems,
    label
}: SectionLoaderOptions<TResult, TItem>): void {
    let isLoading = false;

    const retry = (): void => {
        if (isLoading) {
            return;
        }

        isLoading = true;
        section.setAttribute('aria-busy', 'true');
        section.querySelector('.asyncItemsSectionError')?.remove();
        section.querySelector('.asyncItemsSectionLoading')?.remove();
        section.classList.remove('hide');

        const loadingRegion = document.createElement('div');
        loadingRegion.className = 'asyncItemsSectionLoading padded-top secondaryText';
        loadingRegion.setAttribute('role', 'status');
        loadingRegion.setAttribute('aria-live', 'polite');
        loadingRegion.textContent = globalize.translate('AccessibilityLoading');

        const sectionHeading = section.querySelector<HTMLElement>(':scope > h2, :scope > .sectionTitle');
        section.insertBefore(loadingRegion, sectionHeading?.nextSibling || section.firstChild);

        void Promise.resolve()
            .then(loadItems)
            .then(result => {
                loadingRegion.remove();
                const items = selectItems(result);
                if (!items.length) {
                    section.classList.add('hide');
                    return;
                }

                renderItems(items);
                section.classList.remove('hide');
            })
            .catch((error: unknown) => {
                loadingRegion.remove();
                console.error(`[asyncItemsSection] failed to load ${label}`, error);
                const errorRegion = document.createElement('div');
                errorRegion.className = 'asyncItemsSectionError padded-top';
                errorRegion.setAttribute('role', 'alert');
                errorRegion.append(document.createTextNode(`${globalize.translate('ErrorDefault')} `));

                const retryButton = document.createElement('button');
                retryButton.type = 'button';
                retryButton.className = 'emby-button raised button-submit';
                retryButton.textContent = globalize.translate('Retry');
                retryButton.addEventListener('click', retry, { once: true });
                errorRegion.append(retryButton);

                const errorHeading = section.querySelector<HTMLElement>(':scope > h2, :scope > .sectionTitle');
                section.insertBefore(errorRegion, errorHeading?.nextSibling || section.firstChild);
                section.classList.remove('hide');
            })
            .finally(() => {
                isLoading = false;
                section.removeAttribute('aria-busy');
            });
    };

    retry();
}
