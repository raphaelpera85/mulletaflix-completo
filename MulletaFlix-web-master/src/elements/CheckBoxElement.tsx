import escapeHTML from 'escape-html';
import React, { type FC } from 'react';

import globalize from 'lib/globalize';

const createCheckBoxElement = ({
    labelClassName,
    className,
    id,
    dataFilter,
    dataItemType,
    dataId,
    checkedAttribute,
    renderContent
}: {
    labelClassName?: string;
    type?: string;
    className?: string;
    id?: string;
    dataFilter?: string;
    dataItemType?: string;
    dataId?: string;
    checkedAttribute?: string;
    renderContent?: string;
}) => ({
    __html: `<label ${labelClassName}>
        <input
            is="emby-checkbox"
            type="checkbox"
            class="${escapeHTML(className || '')}"
            ${id ? `id='${escapeHTML(id.replace(/^id=['"]|['"]$/g, ''))}'` : ''}
            ${dataFilter ? `data-filter='${escapeHTML(dataFilter.replace(/^data-filter=['"]|['"]$/g, ''))}'` : ''}
            ${dataItemType ? `data-itemtype='${escapeHTML(dataItemType.replace(/^data-itemtype=['"]|['"]$/g, ''))}'` : ''}
            ${dataId}
            ${checkedAttribute}
        />
        ${renderContent}
    </label>`
});

interface CheckBoxElementProps {
    labelClassName?: string;
    className?: string;
    elementId?: string;
    dataFilter?: string;
    itemType?: string;
    itemId?: string | null;
    itemCheckedAttribute?: string;
    itemName?: string | null;
    title?: string;
}

const CheckBoxElement: FC<CheckBoxElementProps> = ({
    labelClassName,
    className,
    elementId,
    dataFilter,
    itemType,
    itemId,
    itemCheckedAttribute,
    itemName,
    title
}) => {
    const renderContent = itemName ?
        `<span>${escapeHTML(itemName)}</span>` :
        `<span>${globalize.translate(title ?? '')}</span>`;

    return (
        <div
            className='sectioncheckbox'
            dangerouslySetInnerHTML={createCheckBoxElement({
                labelClassName: labelClassName ?
                    `class='${escapeHTML(labelClassName)}'` :
                    '',
                className,
                id: elementId,
                dataFilter,
                dataItemType: itemType,
                dataId: itemId ? `data-id='${escapeHTML(itemId)}'` : '',
                checkedAttribute: itemCheckedAttribute || '',
                renderContent
            })}
        />
    );
};

export default CheckBoxElement;
