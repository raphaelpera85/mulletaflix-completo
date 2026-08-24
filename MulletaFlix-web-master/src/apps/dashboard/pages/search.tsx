import React, { Suspense, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import globalize from 'lib/globalize';

import { useUnifiedSearch } from 'apps/dashboard/features/search/api/useUnifiedSearch';
import Loading from 'components/loading/LoadingComponent';
import Page from 'components/Page';
import SectionContainer from 'components/common/SectionContainer';
import ItemsContainer from 'elements/emby-itemscontainer/ItemsContainer';
import SearchInput from 'apps/dashboard/components/SearchInput';
import type { BaseItemDto } from '@jellyfin/sdk/lib/generated-client';

const SearchPage = () => {
    const [searchParams, setSearchParams] = useSearchParams();
    const searchTerm = searchParams.get('q') ?? '';
    const userId = searchParams.get('userId');

    const { data: result, isLoading, isError, error, refetch } = useUnifiedSearch({
        userId: userId ?? undefined,
        searchTerm: searchTerm || undefined,
        limit: 100
    });

    useEffect(() => {
        if (searchTerm) {
            refetch();
        }
    }, [searchTerm, refetch]);

    if (isLoading) {
        return <Loading />;
    }

    if (isError) {
        return (
            <div className="p-4 text-center text-error">
                {globalize.translate('SearchFailed')}: {String(error)}
            </div>
        );
    }

    const sections = result?.sections ?? [];

    return (
        <Page
            id='searchPage'
            title={globalize.translate('Search')}
            className='mainAnimatedPage type-interior'
        >
            <div className="p-4">
                <div className="mb-4">
                    <SearchInput
                        type="search"
                        className="w-full max-w-md"
                        placeholder={globalize.translate('SearchAllContent')}
                        value={searchTerm}
                        onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchParams({ q: e.target.value })}
                        autoFocus
                    />
                </div>

                {searchTerm && sections.length === 0 && (
                    <div className="text-center py-8 text-base-content/60">
                        {globalize.translate('NoResultsFoundFor')}: "{searchTerm}"
                    </div>
                )}

                {sections.map((section: any) => (
                    <SectionContainer
                        key={section.name}
                        sectionHeaderProps={{ title: section.name }}
                        items={section.items as BaseItemDto[]}
                        cardOptions={section.cardOptions}
                        className="my-4"
                    >
                        {section.items.length === 0 && (
                            <div className="p-4 text-center text-base-content/60">
                                {globalize.translate('NoResults')}
                            </div>
                        )}
                    </SectionContainer>
                ))}

                {!searchTerm && (
                    <div className="text-center py-8 text-base-content/60">
                        {globalize.translate('EnterSearchTerm')}
                    </div>
                )}
            </div>
        </Page>
    );
};

export default SearchPage;