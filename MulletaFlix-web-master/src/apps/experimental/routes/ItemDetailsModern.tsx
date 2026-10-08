import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';

import Page from '../../../components/Page';
import { getRemotePageState, PageStateContainer, type PageState } from 'components/common';
import DetailPageSkeleton from 'components/common/DetailPageSkeleton';
import ViewManagerPage from 'components/viewManager/ViewManagerPage';
import {
    ITEM_DETAILS_PAGE_STATE_EVENT,
    requestItemDetailsPageRetry,
    type ItemDetailsPageStateDetail
} from 'components/itemDetails/itemDetailsPageEvents';
import globalize from '../../../lib/globalize';

/**
 * Modern React wrapper for item details, driven by request states from the
 * legacy controller while keeping that controller mounted.
 *
 * Falls back to legacy ViewManagerPage for actual rendering since the details
 * view uses a complex legacy controller with rich multimedia support.
 *
 * IMPORTANT: ViewManagerPage manages its own loading/mount lifecycle internally
 * (same as every other legacy-controller page in this app). It must stay mounted
 * unconditionally -- PageStateContainer's 'loading' state REPLACES children
 * instead of overlaying them, so wrapping it as a PageStateContainer child
 * The state panel is a sibling of ViewManagerPage; loading and error states
 * never unmount the legacy view, and degraded refreshes retain existing content.
 */
const ItemDetailsModern: React.FC = () => {
    // NOTE: the 'details' route has no ':id' path segment (see asyncRoutes/user.ts) --
    // the item id always arrives as a query string param (?id=...&context=...&serverId=...),
    // same as every other modern page in this app. useParams() here always returned
    // undefined, so this page showed the error state on literally every single item.
    const [searchParams] = useSearchParams();
    const id = searchParams.get('id') || undefined;
    const [loadState, setLoadState] = useState<ItemDetailsPageStateDetail['state']>('loading');
    const [loadedItemId, setLoadedItemId] = useState<string>();
    const loadedItemIdRef = useRef<string>();
    const [isOnline, setIsOnline] = useState(() => typeof navigator === 'undefined' || navigator.onLine);

    useEffect(() => {
        loadedItemIdRef.current = undefined;
        setLoadedItemId(undefined);
        setLoadState('loading');

        const handlePageState = (event: Event): void => {
            const detail = (event as CustomEvent<ItemDetailsPageStateDetail>).detail;
            if (!detail || detail.itemId !== id) {
                return;
            }

            setLoadState(detail.state);
            if (detail.state === 'success') {
                loadedItemIdRef.current = id;
                setLoadedItemId(id);
            }
        };

        const handleOnline = (): void => setIsOnline(true);
        const handleOffline = (): void => setIsOnline(false);

        document.addEventListener(ITEM_DETAILS_PAGE_STATE_EVENT, handlePageState);
        window.addEventListener('online', handleOnline);
        window.addEventListener('offline', handleOffline);

        return () => {
            document.removeEventListener(ITEM_DETAILS_PAGE_STATE_EVENT, handlePageState);
            window.removeEventListener('online', handleOnline);
            window.removeEventListener('offline', handleOffline);
        };
    }, [id]);

    const pageState: PageState = useMemo(() => id ? getRemotePageState({
        isPending: loadState === 'loading',
        isError: loadState === 'error' || loadState === 'degraded',
        hasData: loadedItemId === id,
        isOnline
    }) : 'error', [ id, isOnline, loadedItemId, loadState ]);

    const handleRetry = useCallback((): void => {
        if (id) {
            requestItemDetailsPageRetry(id);
        }
    }, [ id ]);

    // If no item ID provided, show error
    if (!id) {
        return (
            <Page
                id='itemDetailsPage'
                className='mainAnimatedPage itemDetailsPage'
                isBackButtonEnabled={true}
            >
                <PageStateContainer
                    state='error'
                    errorMessage={globalize.translate('ErrorDefault')}
                />
            </Page>
        );
    }

    return (
        <Page
            id='itemDetailsPage'
            className='mainAnimatedPage itemDetailsPage'
            isBackButtonEnabled={true}
        >
            <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100%', position: 'relative' }}>
                {pageState !== 'success' && (
                    <Box
                        aria-live={pageState === 'loading' ? 'polite' : 'assertive'}
                        sx={{
                            ...(pageState === 'loading' ? { position: 'absolute', inset: 0, zIndex: 1 } : {}),
                            ...(pageState === 'offline' || pageState === 'error' ? { width: '100%' } : {})
                        }}
                    >
                        <PageStateContainer
                            state={pageState}
                            onRetry={handleRetry}
                            errorMessage={globalize.translate('ErrorDefault')}
                            loadingComponent={<DetailPageSkeleton sections={3} sectionHeight={200} />}
                        />
                    </Box>
                )}
                <Box
                    aria-hidden={pageState === 'loading' || pageState === 'offline' || (pageState === 'error' && loadedItemIdRef.current !== id)}
                    sx={{
                        visibility: pageState === 'loading' || pageState === 'offline' || (pageState === 'error' && loadedItemIdRef.current !== id) ? 'hidden' : 'visible'
                    }}
                >
                    <ViewManagerPage
                        controller='itemDetails/index'
                        view='itemDetails/index.html'
                    />
                </Box>
            </Box>
        </Page>
    );
};

export default ItemDetailsModern;
