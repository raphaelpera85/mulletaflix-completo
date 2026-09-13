import { getPlaylistsApi } from '@jellyfin/sdk/lib/utils/api/playlists-api';

import listView from 'components/listview/listview';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { toApi } from 'utils/jellyfin-apiclient/compat';
import type { ItemDto } from 'types/base/models/item-dto';

interface PlaylistItem {
    Id: string;
    ServerId: string;
}

interface PlaylistData {
    CanEdit?: boolean;
}

interface PlaylistItemsResponse {
    Items?: ItemDto[];
    TotalRecordCount?: number;
}

interface PlaylistApiClient {
    getCurrentUserId(): string;
    getUrl(path: string, query: Record<string, string>): string;
    getJSON(url: string): Promise<PlaylistItemsResponse>;
}

interface PlaylistContainer extends HTMLElement {
    enableDragReordering(editable: boolean): void;
    fetchData: () => Promise<PlaylistItemsResponse>;
    getItemsHtml: (items: ItemDto[]) => string;
    refreshItems(): Promise<void>;
}

interface PlaylistPage extends HTMLElement {
    playlistInit?: boolean;
}

function getFetchPlaylistItemsFn(apiClient: PlaylistApiClient, itemId: string): () => Promise<PlaylistItemsResponse> {
    return function () {
        const query = {
            Fields: 'PrimaryImageAspectRatio,MediaSourceCount,Chapters,Trickplay',
            EnableImageTypes: 'Primary,Backdrop,Banner,Thumb',
            UserId: apiClient.getCurrentUserId()
        };
        return apiClient.getJSON(apiClient.getUrl(`Playlists/${itemId}/Items`, query));
    };
}

function getItemsHtmlFn(playlistId: string, isEditable: boolean = false): (items: ItemDto[]) => string {
    return function (items: ItemDto[]) {
        return listView.getListViewHtml({
            items,
            showIndex: false,
            playFromHere: true,
            action: 'playallfromhere',
            smallIcon: true,
            dragHandle: isEditable,
            playlistId,
            showParentTitle: true
        });
    };
}

async function init(page: HTMLElement, item: PlaylistItem): Promise<void> {
    const apiClient = ServerConnections.getApiClient(item.ServerId) as unknown as PlaylistApiClient;
    const api = toApi(apiClient as never);

    let isEditable = false;
    const { data } = await getPlaylistsApi(api)
        .getPlaylistUser({
            playlistId: item.Id,
            userId: apiClient.getCurrentUserId()
        })
        .catch((err: unknown) => {
            // If a user doesn't have access, then the request will 404 and throw
            console.info('[PlaylistViewer] Failed to fetch playlist permissions', err);
            return { data: {} as PlaylistData };
        });
    isEditable = !!data.CanEdit;

    const elem = page.querySelector('#childrenContent .itemsContainer') as PlaylistContainer | null;
    if (!elem) return;

    elem.classList.add('vertical-list');
    elem.classList.remove('vertical-wrap');
    elem.enableDragReordering(isEditable);
    elem.fetchData = getFetchPlaylistItemsFn(apiClient, item.Id);
    elem.getItemsHtml = getItemsHtmlFn(item.Id, isEditable);
}

function refresh(page: HTMLElement): void {
    page.querySelector('#childrenContent')!.classList.add('verticalSection-extrabottompadding');
    const container = page.querySelector('#childrenContent .itemsContainer') as PlaylistContainer | null;
    void container?.refreshItems().catch(() => undefined);
}

function render(page: PlaylistPage, item: PlaylistItem): void {
    if (!page.playlistInit) {
        page.playlistInit = true;
        init(page, item)
            .finally(() => {
                refresh(page);
            }).catch(() => undefined);
    } else {
        refresh(page);
    }
}

const PlaylistViewer = {
    render
};

export default PlaylistViewer;
