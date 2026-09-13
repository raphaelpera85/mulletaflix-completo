import './viewManager/viewContainer.scss';
import Dashboard from '../utils/dashboard';

export type ControllerFactory =
    new (view: HTMLElement, params: Record<string, string>) => void | { default: new (view: HTMLElement, params: Record<string, string>) => void };

export interface ViewOptions {
    cancel?: boolean;
    controllerFactory?: ControllerFactory;
    fullscreen?: boolean;
    type?: string;
    url?: string;
    view?: string;
    [key: string]: unknown;
}

interface NormalizedView {
    elem: HTMLElement | string;
    hasScript: boolean;
    hasjQuery: boolean;
    hasjQueryChecked: boolean;
    hasjQuerySelect: boolean;
}

interface JQueryMobileLike {
    activePage?: HTMLElement;
}

interface JQueryWrapper {
    [index: number]: HTMLElement;
    appendTo(target: HTMLElement): JQueryWrapper;
}

interface JQueryLike {
    (element: HTMLElement): JQueryWrapper;
    mobile?: JQueryMobileLike;
}

type BeforeChangeHandler = (view: HTMLElement, restored: boolean, options: ViewOptions) => void;

const getMainAnimatedPages = (): HTMLElement | null => {
    return document.querySelector('.mainAnimatedPages');
};

function getJQuery(): JQueryLike | undefined {
    return (window as Window & { $?: JQueryLike }).$;
}

function setControllerClass(view: HTMLElement, options: ViewOptions): Promise<void> {
    if (options.controllerFactory) {
        return Promise.resolve();
    }

    let controllerUrl = view.getAttribute('data-controller');

    if (controllerUrl) {
        if (controllerUrl.startsWith('__plugin/')) {
            controllerUrl = controllerUrl.substring('__plugin/'.length);
        }

        controllerUrl = Dashboard.getPluginUrl(controllerUrl);
        const apiUrl = ApiClient.getUrl('/web/' + controllerUrl);
        return import(/* @vite-ignore */ apiUrl).then((controllerFactory: unknown) => {
            options.controllerFactory = controllerFactory as ControllerFactory;
        });
    }

    return Promise.resolve();
}

export function loadView(options: ViewOptions): Promise<HTMLElement> | void {
    if (options.cancel) {
        return;
    }

    const selected = selectedPageIndex;
    const previousAnimatable = selected === -1 ? null : allPages[selected] || null;
    let pageIndex = selected + 1;

    if (pageIndex >= pageContainerCount) {
        pageIndex = 0;
    }

    const isPluginpage = (options.url || '').includes('configurationpage');
    const newViewInfo = normalizeNewView(options, isPluginpage);
    const newView = newViewInfo.elem;
    const currentPage = allPages[pageIndex] || null;

    if (currentPage) {
        triggerDestroy(currentPage);
    }

    let view = createViewElement(newView);

    view.classList.add('mainAnimatedPage');

    const mainAnimatedPages = getMainAnimatedPages();
    if (!mainAnimatedPages) {
        console.warn('[viewContainer] main animated pages element is not present');
        return;
    }

    const jq = getJQuery();

    view = attachView(view, currentPage, mainAnimatedPages, newViewInfo.hasScript, jq);

    if (options.type) {
        view.setAttribute('data-type', options.type);
    }

    const properties: string[] = [];
    if (options.fullscreen) {
        properties.push('fullscreen');
    }

    if (properties.length) {
        view.setAttribute('data-properties', properties.join(','));
    }

    allPages[pageIndex] = view;

    return setControllerClass(view, options)
        .then(() => new Promise((resolve) => setTimeout(resolve, 0)))
        .then(() => {
            if (onBeforeChange) {
                onBeforeChange(view, false, options);
            }

            beforeAnimate(allPages, pageIndex, selected);
            selectedPageIndex = pageIndex;
            currentUrls[pageIndex] = options.url || '';

            if (!options.cancel && previousAnimatable) {
                afterAnimate(allPages, pageIndex);
            }

            if (jq) {
                jq.mobile = jq.mobile || {};
                jq.mobile.activePage = view;
            }

            return view;
        });
}

function createViewElement(newView: HTMLElement | string): HTMLElement {
    if (typeof newView !== 'string') {
        return newView;
    }

    const view = document.createElement('div');
    view.innerHTML = newView;
    return view;
}

function attachView(
    view: HTMLElement,
    currentPage: HTMLElement | null,
    mainAnimatedPages: HTMLElement,
    hasScript: boolean,
    jq: JQueryLike | undefined
): HTMLElement {
    if (currentPage && hasScript && jq) {
        mainAnimatedPages.removeChild(currentPage);
        return jq(view).appendTo(mainAnimatedPages)[0];
    }

    if (currentPage) {
        mainAnimatedPages.replaceChild(view, currentPage);
        return view;
    }

    if (hasScript && jq) {
        return jq(view).appendTo(mainAnimatedPages)[0];
    }

    mainAnimatedPages.appendChild(view);
    return view;
}

function parseHtml(html: string, hasScript: boolean): HTMLElement {
    if (hasScript) {
        html = html
            .split('\x3c!--<script').join('<script')
            .split('</script>--\x3e').join('</script>');
    }

    const wrapper = document.createElement('div');
    wrapper.innerHTML = html;
    return wrapper.querySelector<HTMLElement>('div[data-role="page"]') || wrapper;
}

function normalizeNewView(options: ViewOptions, isPluginpage: boolean): NormalizedView {
    const viewHtml = options.view || '';

    if (viewHtml.indexOf('data-role="page"') === -1) {
        return {
            elem: viewHtml,
            hasScript: false,
            hasjQuery: false,
            hasjQueryChecked: false,
            hasjQuerySelect: false
        };
    }

    let hasScript = viewHtml.indexOf('<script') !== -1;
    const elem = parseHtml(viewHtml, hasScript);

    if (hasScript) {
        hasScript = elem.querySelector('script') != null;
    }

    let hasjQuery = false;
    let hasjQuerySelect = false;
    let hasjQueryChecked = false;

    if (isPluginpage) {
        hasjQuery = viewHtml.indexOf('jQuery') !== -1 || viewHtml.indexOf('$(') !== -1 || viewHtml.indexOf('$.') !== -1;
        hasjQueryChecked = viewHtml.indexOf('.checked(') !== -1;
        hasjQuerySelect = viewHtml.indexOf('.selectmenu(') !== -1;
    }

    return {
        elem,
        hasScript,
        hasjQuerySelect,
        hasjQueryChecked,
        hasjQuery
    };
}

function beforeAnimate(allPages: Array<HTMLElement | undefined>, newPageIndex: number, oldPageIndex: number): void {
    for (let index = 0, length = allPages.length; index < length; index++) {
        if (newPageIndex !== index && oldPageIndex !== index) {
            allPages[index]?.classList.add('hide');
        }
    }
}

function afterAnimate(allPages: Array<HTMLElement | undefined>, newPageIndex: number): void {
    for (let index = 0, length = allPages.length; index < length; index++) {
        if (newPageIndex !== index) {
            allPages[index]?.classList.add('hide');
        }
    }
}

export function setOnBeforeChange(fn: BeforeChangeHandler): void {
    onBeforeChange = fn;
}

export function tryRestoreView(options: ViewOptions): Promise<HTMLElement> | void {
    console.debug('[viewContainer] tryRestoreView', options);
    const url = options.url || '';
    const index = currentUrls.indexOf(url);
    const jq = getJQuery();

    if (index !== -1) {
        const animatable = allPages[index];
        const view = animatable;

        if (view) {
            if (options.cancel) {
                return;
            }

            const selected = selectedPageIndex;
            const previousAnimatable = selected === -1 ? null : allPages[selected] || null;
            return setControllerClass(view, options).then(() => {
                if (onBeforeChange) {
                    onBeforeChange(view, true, options);
                }

                beforeAnimate(allPages, index, selected);
                animatable.classList.remove('hide');
                selectedPageIndex = index;

                if (!options.cancel && previousAnimatable) {
                    afterAnimate(allPages, index);
                }

                if (jq) {
                    jq.mobile = jq.mobile || {};
                    jq.mobile.activePage = view;
                }

                return view;
            });
        }
    }

    return Promise.reject();
}

function triggerDestroy(view: HTMLElement): void {
    view.dispatchEvent(new CustomEvent('viewdestroy', {}));
}

export function reset(): void {
    console.debug('[viewContainer] resetting view cache');
    allPages = [];
    currentUrls = [];
    const mainAnimatedPages = getMainAnimatedPages();
    if (mainAnimatedPages) mainAnimatedPages.innerHTML = '';
    selectedPageIndex = -1;
}

let onBeforeChange: BeforeChangeHandler | undefined;
let allPages: Array<HTMLElement | undefined> = [];
let currentUrls: string[] = [];
const pageContainerCount = 3;
let selectedPageIndex = -1;
reset();
getMainAnimatedPages()?.classList.remove('hide');

export default {
    loadView,
    tryRestoreView,
    reset,
    setOnBeforeChange
};
