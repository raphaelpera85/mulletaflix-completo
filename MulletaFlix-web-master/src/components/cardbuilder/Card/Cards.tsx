import React, { type FC, useMemo } from 'react';
import { setCardData } from '../cardBuilder';
import Card from './Card';
import type { ItemDto } from 'types/base/models/item-dto';
import type { CardOptions } from 'types/cardOptions';
import '../card.scss';

interface CardsProps {
    items: ItemDto[];
    cardOptions: CardOptions;
}

const Cards: FC<CardsProps> = ({ items, cardOptions }) => {
    // F-7: setCardData() computes options.shape/width and, via
    // getPrimaryImageAspectRatio(), sorts the aspect ratios of every item in
    // the grid (values.sort()) to find the median. Grids can hold up to ~100
    // cards, so re-running that sort on every render/commit (e.g. from
    // unrelated parent re-renders) is wasted work. Gate it behind useMemo so
    // it only reruns when the actual items or cardOptions reference changes;
    // setCardData still mutates cardOptions in place (existing contract with
    // Card/useCard), useMemo just controls when that mutation happens.
    useMemo(() => {
        setCardData(items, cardOptions);
        return cardOptions;
    }, [items, cardOptions]);

    const renderCards = () =>
        items.map((item) => (
            <Card key={item.Id} item={item} cardOptions={cardOptions} />
        ));

    return <>{renderCards()}</>;
};

export default Cards;
