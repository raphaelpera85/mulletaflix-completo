import type { BaseItemDto } from '@jellyfin/sdk/lib/generated-client';
import React, { type FC, memo, useEffect, useRef } from 'react';

import cardBuilder from 'components/cardbuilder/cardBuilder';
import type { CardOptions } from 'types/cardOptions';
import 'elements/emby-scroller/emby-scroller';
import 'elements/emby-itemscontainer/emby-itemscontainer';
import 'lib/legacy/patchCreateElement';

interface SearchResultsRowProps {
    title?: string;
    items?: BaseItemDto[];
    cardOptions?: CardOptions;
}

const SearchResultsRow: FC<SearchResultsRowProps> = ({ title, items = [], cardOptions = {} }) => {
    const element = useRef<HTMLDivElement>(null);
    const scrollerAttributes = { class: 'padded-top-focusscale padded-bottom-focusscale' } as React.HTMLAttributes<HTMLDivElement>;
    const itemsContainerAttributes = { class: 'focuscontainer-x itemsContainer scrollSlider' } as React.HTMLAttributes<HTMLDivElement>;

    useEffect(() => {
        cardBuilder.buildCards(items, {
            itemsContainer: element.current?.querySelector('.itemsContainer'),
            ...cardOptions
        });

        const scrollerEl = element.current?.querySelector('[is="emby-scroller"]') as (HTMLDivElement & { scroller?: { reload: () => void } }) | null;
        scrollerEl?.scroller?.reload();
    }, [cardOptions, items]);

    return (
        <div
            ref={element}
            className='verticalSection'
        >
            <h2 className='sectionTitle sectionTitle-cards focuscontainer-x padded-left padded-right'>
                {title}
            </h2>
            <div
                is='emby-scroller'
                data-horizontal='true'
                data-centerfocus='card'
                data-scrollbuttons='true'
                {...scrollerAttributes}
            >
                <div
                    is='emby-itemscontainer'
                    {...itemsContainerAttributes}
                />
            </div>
        </div>
    );
};

export default memo(SearchResultsRow);
