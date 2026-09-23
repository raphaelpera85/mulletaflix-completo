import type { Locale } from 'date-fns';

const LOCALE_MAP: Record<string, string> = {
    'af': 'af',
    'ar': 'ar-DZ',
    'be-by': 'be',
    'bg-bg': 'bg',
    'bn': 'bn',
    'ca': 'ca',
    'cs': 'cs',
    'cy': 'cy',
    'da': 'da',
    'de': 'de',
    'el': 'el',
    'en-gb': 'en-GB',
    'en-us': 'en-US',
    'eo': 'eo',
    'es': 'es',
    'es-ar': 'es',
    'es-do': 'es',
    'es-mx': 'es',
    'et': 'et',
    'eu': 'eu',
    'fa': 'fa-IR',
    'fi': 'fi',
    'fr': 'fr',
    'fr-ca': 'fr-CA',
    'gl': 'gl',
    'gsw': 'de',
    'he': 'he',
    'hi-in': 'hi',
    'hr': 'hr',
    'hu': 'hu',
    'id': 'id',
    'is': 'is',
    'it': 'it',
    'ja': 'ja',
    'kk': 'kk',
    'ko': 'ko',
    'lt-lt': 'lt',
    'lv': 'lv',
    'ms': 'ms',
    'nb': 'nb',
    'nl': 'nl',
    'nn': 'nn',
    'pl': 'pl',
    'pt': 'pt',
    'pt-br': 'pt-BR',
    'pt-pt': 'pt',
    'ro': 'ro',
    'ru': 'ru',
    'sk': 'sk',
    'sl-si': 'sl',
    'sv': 'sv',
    'ta': 'ta',
    'th': 'th',
    'tr': 'tr',
    'uk': 'uk',
    'vi': 'vi',
    'zh-cn': 'zh-CN',
    'zh-hk': 'zh-HK',
    'zh-tw': 'zh-TW'
};

const DEFAULT_LOCALE = 'en-US';

let localeString = DEFAULT_LOCALE;

/**
 * The active date-fns locale, or undefined until one has loaded.
 *
 * Every consumer in this app hands the value straight to date-fns as the `locale` option, and
 * date-fns falls back to en-US whenever that option is undefined, so `undefined` behaves exactly
 * like the en-US default used to.
 */
let locale: Locale | undefined;

const localeModules = import.meta.glob('../../node_modules/date-fns/locale/*/index.js');

/**
 * Loads a single locale through the dynamic glob.
 * @param localeName The date-fns locale directory name, for example `pt-BR`.
 * @returns The locale, or undefined when it is not available.
 */
async function loadLocaleModule(localeName: string): Promise<Locale | undefined> {
    const globPath = `../../node_modules/date-fns/locale/${localeName}/index.js`;
    const loadFn = localeModules[globPath];
    if (!loadFn) {
        return undefined;
    }

    try {
        const mod = await loadFn() as { default?: Locale };
        return mod.default ?? (mod as unknown as Locale);
    } catch {
        return undefined;
    }
}

let defaultLocalePromise: Promise<Locale | undefined> | undefined;

/**
 * Loads en-US once, on demand.
 *
 * This deliberately goes through the dynamic glob instead of the previous static
 * `import { enUS } from 'date-fns/locale'`. That import looked harmless and was not: the locale's
 * helper modules (`buildFormatLongFn`, `buildLocalizeFn`, `buildMatchFn`, `buildMatchPatternFn`)
 * live under `date-fns/esm/_lib/`, outside the `/date-fns/locale/` path that `getVendorChunk` in
 * vite.config.ts keeps out of the shared vendor chunk. They therefore landed in `vendor-date-fns`,
 * which turned that entire 285 KB chunk into a static dependency of the entry module: the browser
 * preloaded and parsed all of it before the first render, to obtain one locale object.
 * @returns The default locale, or undefined when it could not be loaded.
 */
function getDefaultLocale(): Promise<Locale | undefined> {
    defaultLocalePromise ??= loadLocaleModule(DEFAULT_LOCALE);
    return defaultLocalePromise;
}

/**
 * Resolves once the default locale has loaded, so callers that need the object itself can await it
 * instead of assuming it exists synchronously.
 */
export const defaultLocaleReady: Promise<Locale | undefined> = getDefaultLocale().then((loaded) => {
    locale ??= loaded;
    return locale;
});

/**
 * Fetches a date-fns locale by name.
 * @param localeName The normalized date-fns locale name.
 * @returns The locale, or undefined when it is not available.
 */
export async function fetchLocale(localeName: string): Promise<Locale | undefined> {
    return (await loadLocaleModule(localeName)) ?? getDefaultLocale();
}

export function normalizeLocale(localeName: string) {
    return LOCALE_MAP[localeName]
        || LOCALE_MAP[localeName.replace(/-.*/, '')]
        || DEFAULT_LOCALE;
}

export async function updateLocale(newLocale: string) {
    console.debug('[dateFnsLocale] updating date-fns locale', newLocale);
    localeString = normalizeLocale(newLocale);
    console.debug('[dateFnsLocale] mapped to date-fns locale', localeString);
    locale = await fetchLocale(localeString);
}

export function getLocale(): Locale | undefined {
    return locale;
}

export function getLocaleWithSuffix() {
    return {
        addSuffix: true,
        locale
    };
}
