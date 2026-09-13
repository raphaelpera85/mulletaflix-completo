import './loading.scss';

let loader: HTMLDivElement | undefined;
let activeOperations = 0;

function createLoader(): HTMLDivElement {
    const elem = document.createElement('div');
    elem.setAttribute('dir', 'ltr');
    elem.classList.add('docspinner');
    elem.classList.add('mdl-spinner');

    elem.innerHTML = '<div class="mdl-spinner__layer mdl-spinner__layer-1"><div class="mdl-spinner__circle-clipper mdl-spinner__left"><div class="mdl-spinner__circle mdl-spinner__circleLeft"></div></div><div class="mdl-spinner__circle-clipper mdl-spinner__right"><div class="mdl-spinner__circle mdl-spinner__circleRight"></div></div></div><div class="mdl-spinner__layer mdl-spinner__layer-2"><div class="mdl-spinner__circle-clipper mdl-spinner__left"><div class="mdl-spinner__circle mdl-spinner__circleLeft"></div></div><div class="mdl-spinner__circle-clipper mdl-spinner__right"><div class="mdl-spinner__circle mdl-spinner__circleRight"></div></div></div><div class="mdl-spinner__layer mdl-spinner__layer-3"><div class="mdl-spinner__circle-clipper mdl-spinner__left"><div class="mdl-spinner__circle mdl-spinner__circleLeft"></div></div><div class="mdl-spinner__circle-clipper mdl-spinner__right"><div class="mdl-spinner__circle mdl-spinner__circleRight"></div></div></div><div class="mdl-spinner__layer mdl-spinner__layer-4"><div class="mdl-spinner__circle-clipper mdl-spinner__left"><div class="mdl-spinner__circle mdl-spinner__circleLeft"></div></div><div class="mdl-spinner__circle-clipper mdl-spinner__right"><div class="mdl-spinner__circle mdl-spinner__circleRight"></div></div></div>';

    document.body.appendChild(elem);
    return elem;
}

export function show() {
    if (!loader) {
        loader = createLoader();
    }
    loader.classList.add('mdlSpinnerActive');
}

export function hide() {
    // Legacy callers still invoke hide() directly. Never let one of them
    // interrupt an operation that is being balanced by withLoading().
    if (activeOperations === 0 && loader) {
        loader.classList.remove('mdlSpinnerActive');
    }
}

/**
 * Runs an asynchronous operation with the global loading indicator balanced on
 * both success and failure paths. Keeping this invariant here prevents new
 * callers from accidentally leaving the application in a permanently busy
 * state when a request rejects.
 */
export async function withLoading<T>(operation: () => Promise<T>): Promise<T> {
    activeOperations += 1;
    show();

    try {
        return await operation();
    } finally {
        activeOperations = Math.max(0, activeOperations - 1);
        if (activeOperations === 0) {
            hide();
        }
    }
}

const loading = {
    show,
    hide,
    withLoading
};

window.Loading = loading;

export default loading;
