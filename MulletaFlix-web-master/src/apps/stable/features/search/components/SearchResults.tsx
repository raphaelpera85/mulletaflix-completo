import React, { type FC, useCallback } from 'react';
import { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import { CardShape } from 'components/cardbuilder/utils/shape';
import SearchResultsRow from './SearchResultsRow';
import globalize from 'lib/globalize';
import { Link } from 'react-router-dom';
import { useSearchItems } from '../api/useSearchItems';
import { Section } from '../types';
import { getSearchScopeLabel, buildSearchGlobalHref } from '../utils/search';
import { PageStateContainer } from 'components/common';

interface SearchResultsProps {
    parentId?: string;
    collectionType?: CollectionType;
    query?: string;
}

/*
 * React component to display search result rows for global search and library view search
 */
const SearchResults: FC<SearchResultsProps> = ({
    parentId,
    collectionType,
    query
}) => {
    const { data, isPending, isError, refetch } = useSearchItems(parentId, collectionType, query?.trim());
    const scopeLabel = getSearchScopeLabel(parentId, collectionType);
    const handleRetry = useCallback(() => {
        refetch().catch(() => undefined);
    }, [refetch]);

    const renderSection = (section: Section, index: number) => {
        return (
            <SearchResultsRow
                key={`${section.title}-${index}`}
                title={globalize.translate(section.title)}
                items={section.items}
                cardOptions={{
                    shape: CardShape.AutoOverflow,
                    scalable: true,
                    showTitle: true,
                    overlayText: false,
                    centerText: true,
                    allowBottomPadding: false,
                    ...section.cardOptions
                }}
            />
        );
    };

    const successContent = (
        <div className={'searchResults padded-top padded-bottom-page'}>
            {scopeLabel && (
                <div className='secondary padded-left padded-right' style={{ marginBottom: '0.75rem' }}>
                    {`Scoped to ${scopeLabel}`}
                </div>
            )}
            {data?.map((section, index) => renderSection(section, index))}
        </div>
    );

    return (
        <PageStateContainer
            state={isError ? 'error' : isPending ? 'loading' : !data?.length ? 'empty' : 'success'}
            onRetry={handleRetry}
            emptyState={{
                title: globalize.translate('SearchResultsEmpty', query ?? ''),
                description: scopeLabel ? `Scoped to ${scopeLabel}` : 'Global search',
                action: collectionType ? (
                    <Link
                        className='emby-button'
                        to={buildSearchGlobalHref(query)}
                    >{globalize.translate('RetryWithGlobalSearch')}</Link>
                ) : undefined
            }}
        >
            {successContent}
        </PageStateContainer>
    );
};

export default SearchResults;
