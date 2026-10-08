export type ItemDetailsPageLoadState = 'loading' | 'success' | 'error' | 'degraded';

export const ITEM_DETAILS_PAGE_STATE_EVENT = 'mulletaflix:item-details:page-state';
export const ITEM_DETAILS_PAGE_RETRY_EVENT = 'mulletaflix:item-details:retry';

export interface ItemDetailsPageStateDetail {
    itemId: string;
    state: ItemDetailsPageLoadState;
}

export interface ItemDetailsPageRetryDetail {
    itemId: string;
}

export const getItemDetailsFailureState = (renderedItemId: string | undefined, requestedItemId: string): ItemDetailsPageLoadState => (
    renderedItemId === requestedItemId ? 'degraded' : 'error'
);

export const emitItemDetailsPageState = (itemId: string, state: ItemDetailsPageLoadState): void => {
    document.dispatchEvent(new CustomEvent<ItemDetailsPageStateDetail>(ITEM_DETAILS_PAGE_STATE_EVENT, {
        detail: { itemId, state }
    }));
};

export const requestItemDetailsPageRetry = (itemId: string): void => {
    document.dispatchEvent(new CustomEvent<ItemDetailsPageRetryDetail>(ITEM_DETAILS_PAGE_RETRY_EVENT, {
        detail: { itemId }
    }));
};
