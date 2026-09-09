import React, { FC } from 'react';
import DOMPurify from 'dompurify';
import escapeHtml from 'escape-html';

const createScroller = ({ scrollerclassName, dataHorizontal, dataMousewheel, dataCenterfocus, dataId, className }: IProps) => ({
    __html: DOMPurify.sanitize(`<div is="emby-scroller"
    class="${escapeHtml(scrollerclassName || '')}"
    ${dataHorizontal}
    ${dataMousewheel}
    ${dataCenterfocus}
    >
        <div
            is="emby-itemscontainer"
            class="${escapeHtml(className || '')}"
            ${dataId}
        >
        </div>
    </div>`)
});

interface IProps {
    scrollerclassName?: string;
    dataHorizontal?: string;
    dataMousewheel?: string;
    dataCenterfocus?: string;
    dataId?: string;
    className?: string;
}

const ItemsScrollerContainerElement: FC<IProps> = ({ scrollerclassName, dataHorizontal, dataMousewheel, dataCenterfocus, dataId, className }) => {
    return (
        <div
            dangerouslySetInnerHTML={createScroller({
                scrollerclassName: scrollerclassName,
                dataHorizontal: dataHorizontal ? `data-horizontal="${escapeHtml(dataHorizontal)}"` : '',
                dataMousewheel: dataMousewheel ? `data-mousewheel="${escapeHtml(dataMousewheel)}"` : '',
                dataCenterfocus: dataCenterfocus ? `data-centerfocus="${escapeHtml(dataCenterfocus)}"` : '',
                dataId: dataId ? `data-id="${escapeHtml(dataId)}"` : '',
                className: className
            })}
        />
    );
};

export default ItemsScrollerContainerElement;
