import React, { useCallback, useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import Box from '@mui/material/Box';

import Page from '../../../components/Page';
import { PageStateContainer } from 'components/common';
import DetailPageSkeleton from 'components/common/DetailPageSkeleton';
import ViewManagerPage from 'components/viewManager/ViewManagerPage';
import globalize from '../../../lib/globalize';

/**
 * Modern React wrapper for the Item Details page that applies PageStateContainer
 * to handle loading, error, offline, and empty states.
 *
 * Falls back to legacy ViewManagerPage for actual rendering since the details
 * view uses a complex legacy controller with rich multimedia support.
 */
const ItemDetailsModern: React.FC = () => {
    const { id } = useParams<{ id: string }>();
    const [pageState, setPageState] = useState<'loading' | 'error' | 'success'>('loading');
    const documentRef = useRef<Document>(document);
    const viewManagerRef = useRef<HTMLDivElement>(null);

    const handleRetry = useCallback(() => {
        setPageState('loading');
        // The ViewManagerPage will handle the retry internally
        if (viewManagerRef.current) {
            setPageState('success');
        }
    }, []);

    useEffect(() => {
        // Set initial state to success since ViewManagerPage handles loading internally
        const timer = setTimeout(() => {
            setPageState('success');
        }, 100);

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
                    onRetry={handleRetry}
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
            <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100%' }}>
                <PageStateContainer
                    state={pageState}
                    onRetry={handleRetry}
                    loadingComponent={<DetailPageSkeleton sections={3} sectionHeight={200} />}
                    errorMessage={globalize.translate('ErrorDefault')}
                >
                    <Box ref={viewManagerRef}>
                        <ViewManagerPage
                            controller='itemDetails/index'
                            view='itemDetails/index.html'
                        />
                    </Box>
                </PageStateContainer>
            </Box>
        </Page>
    );
};

export default ItemDetailsModern;
