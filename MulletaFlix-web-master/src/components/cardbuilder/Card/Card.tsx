import React, { type FC, memo } from 'react';
import useCard from './useCard';
import CardWrapper from './CardWrapper';
import CardBox from './CardBox';

import type { CardOptions } from 'types/cardOptions';
import type { ItemDto } from 'types/base/models/item-dto';

interface CardProps {
    item: ItemDto;
    cardOptions: CardOptions;
    /**
     * Position of this card within its grid/row. Used to mark the first
     * above-the-fold cards as high fetch-priority so their images aren't
     * gated behind the IntersectionObserver-based lazy loader.
     */
    index?: number;
}

// Rough number of cards that are visible without scrolling on a typical
// first load (desktop row + a bit of the next one). Cards beyond this stay
// on the regular lazy-loading path.
const PRIORITY_CARD_COUNT = 8;

const Card: FC<CardProps> = ({ item, cardOptions, index }) => {
    const { getCardWrapperProps, getCardBoxProps } = useCard({ item, cardOptions } );
    const cardWrapperProps = getCardWrapperProps();
    const cardBoxProps = getCardBoxProps();
    const priority = index !== undefined && index < PRIORITY_CARD_COUNT;
    return (
        <CardWrapper {...cardWrapperProps}>
            <CardBox {...cardBoxProps} priority={priority} />
        </CardWrapper>
    );
};

export default memo(Card);
