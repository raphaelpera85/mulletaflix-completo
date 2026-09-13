/**
 * Module alphaPicker.
 * @module components/alphaPicker/alphaPicker
 */

import escapeHtml from 'escape-html';

import focusManager from '../focusManager';
import layoutManager from '../layoutManager';
import dom from '../../utils/dom';
import globalize from '../../lib/globalize';
import './style.scss';
import '../../elements/emby-button/paper-icon-button-light';
import 'material-design-icons-iconfont';

const selectedButtonClass = 'alphaPickerButton-selected';

interface AlphaPickerOptions {
    element: HTMLElement;
    itemsContainer?: HTMLElement;
    itemClass?: string;
    mode?: string;
    valueChangeEvent?: string;
    [key: string]: unknown;
}

interface Query {
    NameLessThan?: string;
    NameStartsWith?: string;
    SortBy: string;
}

function focus(this: HTMLElement): void {
    const selected = this.querySelector(`.${selectedButtonClass}`) as HTMLElement | null;

    if (selected) {
        focusManager.focus(selected);
    } else {
        focusManager.autoFocus(this, true);
    }
}

function getAlphaPickerButtonClassName(vertical: boolean): string {
    let alphaPickerButtonClassName = 'alphaPickerButton';

    if (layoutManager.tv) {
        alphaPickerButtonClassName += ' alphaPickerButton-tv';
    }

    if (vertical) {
        alphaPickerButtonClassName += ' alphaPickerButton-vertical';
    }

    return alphaPickerButtonClassName;
}

function getLetterButton(l: string, vertical: boolean): string {
    return `<button data-value="${escapeHtml(l)}" class="${getAlphaPickerButtonClassName(vertical)}">${escapeHtml(l)}</button>`;
}

function mapLetters(letters: string[], vertical: boolean): string[] {
    return letters.map(l => {
        return getLetterButton(l, vertical);
    });
}

function render(element: HTMLElement, options: AlphaPickerOptions): void {
    element.classList.add('alphaPicker');

    if (layoutManager.tv) {
        element.classList.add('alphaPicker-tv');
    }

    const vertical = element.classList.contains('alphaPicker-vertical');

    if (!vertical) {
        element.classList.add('focuscontainer-x');
    }

    let html = '';
    let letters: string[];

    const alphaPickerButtonClassName = getAlphaPickerButtonClassName(vertical);

    let rowClassName = 'alphaPickerRow';

    if (vertical) {
        rowClassName += ' alphaPickerRow-vertical';
    }

    html += `<div class="${rowClassName}">`;
    if (options.mode === 'keyboard') {
        html += `<button data-value=" " is="paper-icon-button-light" class="${alphaPickerButtonClassName}" aria-label="${globalize.translate('ButtonSpace')}"><span class="material-icons alphaPickerButtonIcon space_bar" aria-hidden="true"></span></button>`;
    } else {
        letters = ['#'];
        html += mapLetters(letters, vertical).join('');
    }

    letters = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z'];
    html += mapLetters(letters, vertical).join('');

    if (options.mode === 'keyboard') {
        html += `<button data-value="backspace" is="paper-icon-button-light" class="${alphaPickerButtonClassName}" aria-label="${globalize.translate('ButtonBackspace')}"><span class="material-icons alphaPickerButtonIcon backspace" aria-hidden="true"></span></button>`;
        html += '</div>';

        letters = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9'];
        html += `<div class="${rowClassName}">`;
        html += '<br/>';
        html += mapLetters(letters, vertical).join('');
        html += '</div>';
    } else {
        html += '</div>';
    }

    element.innerHTML = html;

    element.classList.add('focusable');
    element.focus = focus as HTMLElement['focus'];
}

function updateSelectedButton(element: HTMLElement, value: string | null): void {
    const selected = element.querySelector(`.${selectedButtonClass}`) as HTMLElement | null;
    if (!value) {
        selected?.classList.remove(selectedButtonClass);
        return;
    }

    let button: HTMLElement | null = null;
    try {
        button = element.querySelector(`.alphaPickerButton[data-value='${value}']`) as HTMLElement | null;
    } catch (error) {
        console.error('error in querySelector:', error);
    }

    if (button && button !== selected) button.classList.add(selectedButtonClass);
    if (selected && selected !== button) selected.classList.remove(selectedButtonClass);
}

export class AlphaPicker {
    options: AlphaPickerOptions;
    _currentValue: string | null = null;
    enabled: (enabled: boolean) => void = () => undefined;
    visible: (visible: boolean) => void = () => undefined;

    constructor(options: AlphaPickerOptions) {
        this.options = options;

        const element = options.element;
        const itemsContainer = options.itemsContainer;
        const itemClass = options.itemClass;

        let itemFocusValue: string | null;
        let itemFocusTimeout: ReturnType<typeof setTimeout> | null;

        const onItemFocusTimeout = (): void => {
            itemFocusTimeout = null;
            this.value(itemFocusValue, true);
        };

        let alphaFocusedElement: HTMLElement | null;
        let alphaFocusTimeout: ReturnType<typeof setTimeout> | null;

        const onAlphaFocusTimeout = (): void => {
            alphaFocusTimeout = null;

            if (document.activeElement === alphaFocusedElement) {
                const value = alphaFocusedElement!.getAttribute('data-value');
                this.value(value, true);
            }
        };

        function onAlphaPickerInKeyboardModeClick(e: MouseEvent): void {
            const alphaPickerButton = dom.parentWithClass(e.target as HTMLElement, 'alphaPickerButton') as HTMLElement | null;

            if (alphaPickerButton) {
                const value = alphaPickerButton.getAttribute('data-value');

                element.dispatchEvent(new CustomEvent('alphavalueclicked', {
                    cancelable: false,
                    detail: {
                        value
                    }
                }));
            }
        }

        const onAlphaPickerClick = (e: MouseEvent): void => {
            const alphaPickerButton = dom.parentWithClass(e.target as HTMLElement, 'alphaPickerButton') as HTMLElement | null;

            if (alphaPickerButton) {
                const value = alphaPickerButton.getAttribute('data-value');
                if ((this._currentValue || '').toUpperCase() === (value || '').toUpperCase()) {
                    this.value(null, true);
                } else {
                    this.value(value, true);
                }
            }
        };

        function onAlphaPickerFocusIn(e: FocusEvent): void {
            if (alphaFocusTimeout) {
                clearTimeout(alphaFocusTimeout);
                alphaFocusTimeout = null;
            }

            const alphaPickerButton = dom.parentWithClass(e.target as HTMLElement, 'alphaPickerButton') as HTMLElement | null;

            if (alphaPickerButton) {
                alphaFocusedElement = alphaPickerButton;
                alphaFocusTimeout = setTimeout(onAlphaFocusTimeout, 600);
            }
        }

        function onItemsFocusIn(e: FocusEvent): void {
            const item = dom.parentWithClass(e.target as HTMLElement, itemClass!) as HTMLElement | null;

            if (item) {
                const prefix = item.getAttribute('data-prefix');
                if (prefix?.length) {
                    itemFocusValue = prefix[0];
                    if (itemFocusTimeout) {
                        clearTimeout(itemFocusTimeout);
                    }
                    itemFocusTimeout = setTimeout(onItemFocusTimeout, 100);
                }
            }
        }

        this.enabled = function (enabled: boolean): void {
            if (enabled) {
                if (itemsContainer) {
                    itemsContainer.addEventListener('focus', onItemsFocusIn as EventListener, true);
                }

                if (options.mode === 'keyboard') {
                    element.addEventListener('click', onAlphaPickerInKeyboardModeClick);
                }

                if (options.valueChangeEvent !== 'click') {
                    element.addEventListener('focus', onAlphaPickerFocusIn as EventListener, true);
                } else {
                    element.addEventListener('click', onAlphaPickerClick);
                }
            } else {
                if (itemsContainer) {
                    itemsContainer.removeEventListener('focus', onItemsFocusIn as EventListener, true);
                }

                element.removeEventListener('click', onAlphaPickerInKeyboardModeClick);
                element.removeEventListener('focus', onAlphaPickerFocusIn as EventListener, true);
                element.removeEventListener('click', onAlphaPickerClick);
            }
        };

        render(element, options);

        this.enabled(true);
        this.visible(true);
    }

    value(value?: string | null, applyValue?: boolean): string | null {
        const element = this.options.element;

        if (value !== undefined) {
            if (value !== null) {
                value = value.toUpperCase();
                this._currentValue = value;

                if (this.options.mode !== 'keyboard') {
                    updateSelectedButton(element, value);
                }
            } else {
                this._currentValue = value;
                updateSelectedButton(element, null);
            }
        }

        if (applyValue) {
            element.dispatchEvent(new CustomEvent('alphavaluechanged', {
                cancelable: false,
                detail: {
                    value
                }
            }));
        }

        return this._currentValue;
    }

    on(name: string, fn: EventListenerOrEventListenerObject): void {
        const element = this.options.element;
        element.addEventListener(name, fn);
    }

    off(name: string, fn: EventListenerOrEventListenerObject): void {
        const element = this.options.element;
        element.removeEventListener(name, fn);
    }

    updateControls(query: Query): void {
        if (query.NameLessThan) {
            this.value('#');
        } else {
            this.value(query.NameStartsWith);
        }

        this.visible(query.SortBy.indexOf('SortName') !== -1);
    }

    focus(): void {
        const element = this.options.element;
        focusManager.autoFocus(element, true);
    }

    destroy(): void {
        const element = this.options.element;
        this.enabled(false);
        element.classList.remove('focuscontainer-x');
        this.options = null as unknown as AlphaPickerOptions;
    }
}

export default AlphaPicker;
