import React, { FunctionComponent } from 'react';
import DOMPurify from 'dompurify';
import escapeHtml from 'escape-html';

import globalize from 'lib/globalize';

type IProps = {
    is?: string;
    id?: string;
    title?: string;
    className?: string;
    icon?: string,
    dataIndex?: string | number;
    dataTag?: string | number;
    dataProfileid?: string | number;
    onClick?: () => void;
};

const createIconButtonElement = ({ is, id, className, title, icon, dataIndex, dataTag, dataProfileid }: IProps) => ({
    __html: DOMPurify.sanitize(`<button
        is="${escapeHtml(is || '')}"
        type="button"
        ${id}
        class="${escapeHtml(className || '')}"
        ${title}
        ${dataIndex}
        ${dataTag}
        ${dataProfileid}
    >
        <span class="material-icons ${escapeHtml(icon || '')}" aria-hidden="true"></span>
    </button>`)
});

const IconButtonElement: FunctionComponent<IProps> = ({ is, id, className, title, icon, dataIndex, dataTag, dataProfileid, onClick }: IProps) => {
    const button = createIconButtonElement({
        is: is,
        id: id ? `id="${escapeHtml(id)}"` : '',
        className: className,
        title: title ? `title="${escapeHtml(globalize.translate(title))}"` : '',
        icon: icon,
        dataIndex: (dataIndex || dataIndex === 0) ? `data-index="${escapeHtml(String(dataIndex))}"` : '',
        dataTag: dataTag ? `data-tag="${escapeHtml(String(dataTag))}"` : '',
        dataProfileid: dataProfileid ? `data-profileid="${escapeHtml(String(dataProfileid))}"` : ''
    });

    if (onClick !== undefined) {
        return (
            <button
                style={{ all: 'unset' }}
                dangerouslySetInnerHTML={button}
                onClick={onClick}
            />
        );
    }

    return (
        <div
            dangerouslySetInnerHTML={button}
        />
    );
};

export default IconButtonElement;
