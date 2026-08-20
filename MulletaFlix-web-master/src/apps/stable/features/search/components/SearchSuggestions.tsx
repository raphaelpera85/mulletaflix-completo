import React, { FunctionComponent } from 'react';

import type { BaseItemDto, SearchHint } from '@jellyfin/sdk/lib/generated-client';
import { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import Loading from 'components/loading/LoadingComponent';
import { appRouter } from 'components/router/appRouter';
import LinkButton from 'elements/emby-button/LinkButton';
import globalize from 'lib/globalize';
import { useSearchSuggestions } from '../api/useSearchSuggestions';

import 'elements/emby-button/emby-button';

type SearchSuggestionItem = BaseItemDto | SearchHint;

type SearchSuggestionsProps = {
    parentId?: string | null;
    query?: string;
    collectionType?: CollectionType;
};

const SearchSuggestions: FunctionComponent<SearchSuggestionsProps> = ({ parentId, query, collectionType }) => {
    const { data: suggestions, isPending } = useSearchSuggestions(parentId || undefined, query, collectionType);

    if (isPending) return <Loading />;

    const hasQuery = !!query?.trim();

    return (
        <div
            className='verticalSection searchSuggestions'
            style={{ textAlign: 'center' }}
        >
            <div>
                <h2 className='sectionTitle padded-left padded-right'>
                    {hasQuery ? globalize.translate('Search') : globalize.translate('Suggestions')}
                </h2>
            </div>

            <div className='searchSuggestionsList padded-left padded-right'>
                {suggestions?.map((item: SearchSuggestionItem) => (
                    <div key={item.Id}>
                        <LinkButton
                            className='button-link'
                            style={{ display: 'inline-block', padding: '0.5em 1em' }}
                            href={appRouter.getRouteUrl(item)}
                        >
                            {item.Name}
                        </LinkButton>
                    </div>
                ))}
            </div>
        </div>
    );
};

export default SearchSuggestions;
