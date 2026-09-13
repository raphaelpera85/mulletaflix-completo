import { withLoading } from '../../components/loading/loading';
import cardBuilder from '../../components/cardbuilder/cardBuilder';
import type { ItemDtoQueryResult } from 'types/base/models/item-dto-query-result';

interface ViewParams {
    topParentId: string;
}

interface PageData {
    query: Record<string, unknown>;
}

const data: Record<string, PageData> = {};

function getQuery(params: ViewParams): Record<string, unknown> {
    const key = getSavedQueryKey(params);
    let pageData = data[key];

    if (!pageData) {
        pageData = data[key] = {
            query: {
                SortBy: 'SortName',
                SortOrder: 'Ascending',
                IncludeItemTypes: 'Series',
                Recursive: true,
                Fields: 'DateCreated,PrimaryImageAspectRatio',
                StartIndex: 0
            }
        };
        pageData.query.ParentId = params.topParentId;
    }

    return pageData.query;
}

function getSavedQueryKey(params: ViewParams): string {
    return `${params.topParentId}-studios`;
}

function getPromise(params: ViewParams): Promise<ItemDtoQueryResult> {
    const query = getQuery(params);
    return ApiClient.getStudios(ApiClient.getCurrentUserId(), query);
}

function reloadItems(context: HTMLElement, params: ViewParams, promise: Promise<ItemDtoQueryResult>): void {
    void withLoading(async () => {
        const result = await promise;
        const elem = context.querySelector('#items');
        if (!elem) {
            return;
        }
        cardBuilder.buildCards(result.Items ?? [], {
            itemsContainer: elem,
            shape: 'backdrop',
            preferThumb: true,
            showTitle: true,
            scalable: true,
            centerText: true,
            overlayMoreButton: true,
            context: 'tvshows'
        });

        void import('../../components/autoFocuser').then(({ default: autoFocuser }) => {
            autoFocuser.autoFocus(context);
        }).catch((error: unknown) => console.error('[TvStudios] failed to focus page', error));
    }).catch((error: unknown) => {
        console.error('[TvStudios] failed to load studios', error);
    });
}

interface TvStudiosController {
    preRender: () => void;
    renderTab: () => void;
}

export default function (this: TvStudiosController, view: HTMLElement, params: ViewParams, tabContent: HTMLElement): void {
    let promise: Promise<ItemDtoQueryResult> = Promise.resolve({ Items: [] });

    this.preRender = function (): void {
        promise = getPromise(params);
    };

    this.renderTab = function (): void {
        reloadItems(tabContent, params, promise);
    };
}
