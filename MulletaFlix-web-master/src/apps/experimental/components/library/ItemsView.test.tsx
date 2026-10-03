import React, { act } from 'react';
import { createRoot, type Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
    useGetItemsViewByType: vi.fn(),
    useLocalStorage: vi.fn(),
    refetch: vi.fn()
}));

vi.mock('@mui/material/Box', () => ({ default: ({ children }: React.PropsWithChildren) => <div>{children}</div> }));
vi.mock('@mui/material/ButtonGroup', () => ({ default: ({ children }: React.PropsWithChildren) => <div>{children}</div> }));
vi.mock('@mui/material/Chip', () => ({ default: ({ label }: { label: string }) => <span>{label}</span> }));
vi.mock('@mui/material/Stack', () => ({ default: ({ children }: React.PropsWithChildren) => <div>{children}</div> }));
vi.mock('@mui/material/Toolbar', () => ({ default: ({ children }: React.PropsWithChildren) => <div>{children}</div> }));
vi.mock('@mui/material/useMediaQuery', () => ({ default: () => true }));
vi.mock('components/OffsetAppBar', () => ({ default: ({ children }: React.PropsWithChildren) => <div>{children}</div> }));
vi.mock('hooks/useApi', () => ({ useApi: () => ({}) }));
vi.mock('hooks/useLocalStorage', () => ({ useLocalStorage: mocks.useLocalStorage }));
vi.mock('hooks/useFetchItems', () => ({ useGetItemsViewByType: mocks.useGetItemsViewByType }));
vi.mock('hooks/useItem', () => ({ useItem: () => ({ data: undefined }) }));
vi.mock('hooks/useUserSettings', () => ({ useUserSettings: () => ({ libraryPageSize: 20 }) }));
vi.mock('components/common/LoadErrorMessage', () => ({
    default: ({ onRetry }: { onRetry: () => void }) => (
        <div role='alert'>
            <p>Falha ao carregar títulos</p>
            <button type='button' onClick={onRetry}>Retry</button>
        </div>
    )
}));
vi.mock('components/common/NoItemsMessage', () => ({ default: ({ message }: { message: string }) => <p>{message}</p> }));
vi.mock('components/loading/LoadingComponent', () => ({ default: () => <div>Loading</div> }));
vi.mock('elements/emby-itemscontainer/ItemsContainer', () => ({ default: ({ children }: React.PropsWithChildren) => <main>{children}</main> }));
vi.mock('components/listview/List/Lists', () => ({ default: () => null }));
vi.mock('components/cardbuilder/Card/Cards', () => ({
    default: ({ items }: { items: { Name?: string }[] }) => <div>{items.map((item) => item.Name).join(', ')}</div>
}));
vi.mock('components/common/SectionContainer', () => ({ default: () => null }));
vi.mock('components/cardbuilder/utils/shape', () => ({ CardShape: {} }));
vi.mock('components/playback/playbackmanager', () => ({ playbackManager: { canQueue: () => false } }));
vi.mock('lib/globalize', () => ({ default: { translate: (key: string) => key } }));

vi.mock('./AlphabetPicker', () => ({ default: () => null }));
vi.mock('./filter/FilterButton', () => ({ default: () => null }));
vi.mock('./NewCollectionButton', () => ({ default: () => null }));
vi.mock('./NewPlaylistButton', () => ({ default: () => null }));
vi.mock('./Pagination', () => ({ default: () => null }));
vi.mock('./PlayAllButton', () => ({ default: () => null }));
vi.mock('./QueueButton', () => ({ default: () => null }));
vi.mock('./ShuffleButton', () => ({ default: () => null }));
vi.mock('./SortButton', () => ({ default: () => null }));
vi.mock('./LibraryViewMenu', () => ({ default: () => null }));
vi.mock('./ViewSettingsButton', () => ({ default: () => null }));

import { ImageType } from '@jellyfin/sdk/lib/generated-client/models/image-type';
import { ItemSortBy } from '@jellyfin/sdk/lib/generated-client/models/item-sort-by';
import { SortOrder } from '@jellyfin/sdk/lib/generated-client/models/sort-order';
import ItemsView from './ItemsView';
import { LibraryTab } from 'types/libraryTab';
import { ViewMode, type LibraryViewSettings } from 'types/library';

describe('ItemsView load failures', () => {
    let host: HTMLDivElement;
    let root: Root;

    beforeEach(() => {
        host = document.createElement('div');
        root = createRoot(host);
        mocks.refetch.mockReset();
        mocks.refetch.mockResolvedValue({});
        mocks.useLocalStorage.mockReturnValue([{
            SortBy: ItemSortBy.SortName,
            SortOrder: SortOrder.Ascending,
            StartIndex: 0,
            CardLayout: false,
            ImageType: ImageType.Primary,
            ViewMode: ViewMode.GridView,
            ShowTitle: true,
            ShowYear: true
        } satisfies LibraryViewSettings, vi.fn()]);
        mocks.useGetItemsViewByType.mockReturnValue({
            isPending: false,
            isError: true,
            data: undefined,
            isPlaceholderData: false,
            refetch: mocks.refetch
        });
    });

    afterEach(async () => {
        await act(async () => root.unmount());
    });

    it('shows a retryable error instead of an empty library after a failed request', async () => {
        await act(async () => root.render(
            <ItemsView
                viewType={LibraryTab.Series}
                parentId='library-id'
                itemType={[]}
                noItemsMessage='Nenhum título'
                isBtnFilterEnabled={false}
                isBtnSortEnabled={false}
                isBtnGridListEnabled={false}
                isAlphabetPickerEnabled={false}
                isPaginationEnabled={false}
            />
        ));

        expect(host.querySelector('[role="alert"]')?.textContent).toContain('Falha ao carregar títulos');
        expect(host.textContent).not.toContain('Nenhum título');
        expect(host.textContent).toContain('\u2014');
    });

    it('retries the failed library request', async () => {
        await act(async () => root.render(
            <ItemsView
                viewType={LibraryTab.Series}
                parentId='library-id'
                itemType={[]}
                noItemsMessage='Nenhum título'
                isBtnFilterEnabled={false}
                isBtnSortEnabled={false}
                isBtnGridListEnabled={false}
                isAlphabetPickerEnabled={false}
                isPaginationEnabled={false}
            />
        ));

        const retry = host.querySelector('button');
        expect(retry?.textContent).toBe('Retry');
        await act(async () => retry?.click());
        expect(mocks.refetch).toHaveBeenCalledOnce();
    });

    it('keeps previously loaded titles visible when a background refresh fails', async () => {
        mocks.useGetItemsViewByType.mockReturnValue({
            isPending: false,
            isError: true,
            data: { Items: [{ Name: 'Atomic' }], TotalRecordCount: 1 },
            isPlaceholderData: false,
            refetch: mocks.refetch
        });

        await act(async () => root.render(
            <ItemsView
                viewType={LibraryTab.Series}
                parentId='library-id'
                itemType={[]}
                noItemsMessage='Nenhum título'
                isBtnFilterEnabled={false}
                isBtnSortEnabled={false}
                isBtnGridListEnabled={false}
                isAlphabetPickerEnabled={false}
                isPaginationEnabled={false}
            />
        ));

        expect(host.querySelector('[role="alert"]')).not.toBeNull();
        expect(host.textContent).toContain('Atomic');
        expect(host.textContent).not.toContain('Nenhum título');
    });

    it('does not render a false empty state or numeric zero after an empty cached refresh fails', async () => {
        mocks.useGetItemsViewByType.mockReturnValue({
            isPending: false,
            isError: true,
            data: { Items: [], TotalRecordCount: 0 },
            isPlaceholderData: false,
            refetch: mocks.refetch
        });

        await act(async () => root.render(
            <ItemsView
                viewType={LibraryTab.Series}
                parentId='library-id'
                itemType={[]}
                noItemsMessage='Nenhum título'
                isBtnFilterEnabled={false}
                isBtnSortEnabled={false}
                isBtnGridListEnabled={false}
                isAlphabetPickerEnabled={false}
                isPaginationEnabled={false}
            />
        ));

        const content = host.querySelector('main')?.textContent;
        expect(content).toContain('Falha ao carregar títulos');
        expect(content).not.toContain('Nenhum título');
        expect(content).not.toContain('0');
    });

    it('still shows the empty state after a successful empty response', async () => {
        mocks.useGetItemsViewByType.mockReturnValue({
            isPending: false,
            isError: false,
            data: { Items: [], TotalRecordCount: 0 },
            isPlaceholderData: false,
            refetch: mocks.refetch
        });

        await act(async () => root.render(
            <ItemsView
                viewType={LibraryTab.Series}
                parentId='library-id'
                itemType={[]}
                noItemsMessage='Nenhum título'
                isBtnFilterEnabled={false}
                isBtnSortEnabled={false}
                isBtnGridListEnabled={false}
                isAlphabetPickerEnabled={false}
                isPaginationEnabled={false}
            />
        ));

        expect(host.textContent).toContain('Nenhum título');
        expect(host.querySelector('[role="alert"]')).toBeNull();
    });
});
