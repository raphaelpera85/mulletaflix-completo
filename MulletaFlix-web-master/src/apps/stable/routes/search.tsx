import type { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import React, { type FC } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useDebounceValue } from 'usehooks-ts';

import SearchFields from 'apps/stable/features/search/components/SearchFields';
import SearchResults from 'apps/stable/features/search/components/SearchResults';
import SearchSuggestions from 'apps/stable/features/search/components/SearchSuggestions';
import Page from 'components/Page';
import useSearchParam from 'hooks/useSearchParam';
import globalize from 'lib/globalize';
import { getSearchScopeLabel, buildSearchScopeHref } from '../features/search/utils/search';

const COLLECTION_TYPE_PARAM = 'collectionType';
const PARENT_ID_PARAM = 'parentId';
const QUERY_PARAM = 'query';

const MIN_QUERY_LENGTH = 2;

const Search: FC = () => {
    const [searchParams] = useSearchParams();
    const parentIdQuery = searchParams.get(PARENT_ID_PARAM) || undefined;
    const collectionTypeQuery = (searchParams.get(COLLECTION_TYPE_PARAM) || undefined) as CollectionType | undefined;
    const scopeLabel = getSearchScopeLabel(parentIdQuery, collectionTypeQuery);
    const clearScopeHref = buildSearchScopeHref(searchParams);
    const [ query, setQuery ] = useSearchParam(QUERY_PARAM);
    const [debouncedQuery] = useDebounceValue(query, 350);

    const pageTitle = scopeLabel ? `${globalize.translate('Search')} - ${scopeLabel}` : globalize.translate('Search');
    const shouldSearch = debouncedQuery && debouncedQuery.length >= MIN_QUERY_LENGTH;

    return (
        <Page
            id='searchPage'
            title={pageTitle}
            className='mainAnimatedPage libraryPage allLibraryPage noSecondaryNavPage'
        >
            <SearchFields
                query={query}
                onSearch={setQuery}
                scopeLabel={scopeLabel}
                scopeHref={scopeLabel ? clearScopeHref : undefined}
            />
            {!shouldSearch ? (
                <SearchSuggestions
                    parentId={parentIdQuery}
                    query={debouncedQuery}
                    collectionType={collectionTypeQuery}
                />
            ) : (
                <SearchResults
                    parentId={parentIdQuery}
                    collectionType={collectionTypeQuery}
                    query={debouncedQuery}
                />
            )}
        </Page>
    );
};

export default Search;

