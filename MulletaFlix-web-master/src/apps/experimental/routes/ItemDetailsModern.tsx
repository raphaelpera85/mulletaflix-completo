import React, { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';

import Page from '../../../components/Page';
import { PageStateContainer } from 'components/common';
import DetailPageSkeleton from 'components/common/DetailPageSkeleton';
import ViewManagerPage from 'components/viewManager/ViewManagerPage';
import globalize from '../../../lib/globalize';

/**
 * Modern React wrapper for the Item Details page that applies PageStateContainer
 * to handle loading/error/empty states.
 *
 * Falls back to legacy ViewManagerPage for actual rendering since the details
 * view uses a complex legacy controller with rich multimedia support.
 *
 * IMPORTANT: ViewManagerPage manages its own loading/mount lifecycle internally
 * (same as every other legacy-controller page in this app). It must stay mounted
 * unconditionally -- PageStateContainer's 'loading' state REPLACES children
 * instead of overlaying them, so wrapping it as a PageStateContainer child
 * (as this file previously did, gated by an artificial setTimeout faking a
 * 'success' transition) is the exact same bug class that broke the Home page.
 * The loading skeleton below is a pure visual overlay above the always-mounted
 * ViewManagerPage, not a gate on it.
 */
const ItemDetailsModern: React.FC = () => {
    // NOTE: the 'details' route has no ':id' path segment (see asyncRoutes/user.ts) --
    // the item id always arrives as a query string param (?id=...&context=...&serverId=...),
    // same as every other modern page in this app. useParams() here always returned
    // undefined, so this page showed the error state on literally every single item.
    const [searchParams] = useSearchParams();
    const id = searchParams.get('id') || undefined;
    const [showSkeleton, setShowSkeleton] = useState(true);

    useEffect(() => {
        setShowSkeleton(true);
        const timer = setTimeout(() => setShowSkeleton(false), 100);
        return () => clearTimeout(timer);
    }, [id]);

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
                {showSkeleton && (
                    <Box sx={{ position: 'absolute', inset: 0, zIndex: 1 }}>
                        <DetailPageSkeleton sections={3} sectionHeight={200} />
                    </Box>
                )}
                <Box sx={{ visibility: showSkeleton ? 'hidden' : 'visible' }}>
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
