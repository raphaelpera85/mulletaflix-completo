import React, { type FC } from 'react';
import { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import Loading from 'components/loading/LoadingComponent';
import { CardShape } from 'components/cardbuilder/utils/shape';
import SearchResultsRow from './SearchResultsRow';
import globalize from 'lib/globalize';
import { Link } from 'react-router-dom';
import { useSearchItems } from '../api/useSearchItems';
import { Section } from '../types';

function getScopeLabel(parentId?: string, collectionType?: CollectionType) {
    if (collectionType === CollectionType.Movies) return 'Movies';
    if (collectionType === CollectionType.Tvshows) return 'TV Shows';
    if (collectionType === CollectionType.Music) return 'Music';
    if (collectionType === CollectionType.Livetv) return 'Live TV';
    if (parentId) return 'this library';
    return undefined;
}

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
    const { data, isPending } = useSearchItems(parentId, collectionType, query?.trim());
    const scopeLabel = getScopeLabel(parentId, collectionType);

    if (isPending) return <Loading />;

    if (!data?.length) {
        return (
            <div className='noItemsMessage centerMessage'>
                <div className='secondary padded-left padded-right' style={{ marginBottom: '0.75rem' }}>
                    {scopeLabel ? `Scoped to ${scopeLabel}` : 'Global search'}
                </div>
                {globalize.translate('SearchResultsEmpty', query ?? '')}
                {collectionType && (
                    <div>
                        <Link
                            className='emby-button'
                            to={`/search?query=${encodeURIComponent(query || '')}`}
                        >{globalize.translate('RetryWithGlobalSearch')}</Link>
                    </div>
                )}
            </div>
        );
    }

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

    return (
        <div className={'searchResults padded-top padded-bottom-page'}>
            {scopeLabel && (
                <div className='secondary padded-left padded-right' style={{ marginBottom: '0.75rem' }}>
                    {`Scoped to ${scopeLabel}`}
                </div>
            )}
            {data.map((section, index) => renderSection(section, index))}
        </div>
    );
};

export default SearchResults;

