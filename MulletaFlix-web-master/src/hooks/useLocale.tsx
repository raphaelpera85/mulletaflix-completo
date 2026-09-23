import type { Locale } from 'date-fns';
import { useEffect, useMemo, useState } from 'react';

import { getDefaultLanguage, normalizeLocaleName } from 'lib/globalize';
import { fetchLocale, normalizeLocale } from 'utils/dateFnsLocale';

import { useUserSettings } from './useUserSettings';

export function useLocale() {
    const { dateTimeLocale: dateTimeSetting, language } = useUserSettings();
    // Honest type instead of the previous `undefined as unknown as Locale` cast: the locale is
    // genuinely absent until fetchLocale resolves, and date-fns falls back to en-US for undefined.
    const [ dateFnsLocale, setDateFnsLocale ] = useState<Locale | undefined>();

    const locale: string = useMemo(() => (
        normalizeLocaleName(language || getDefaultLanguage())
    ), [ language ]);

    const dateTimeLocale: string = useMemo(() => (
        dateTimeSetting ? normalizeLocaleName(dateTimeSetting) : locale
    ), [ dateTimeSetting, locale ]);

    useEffect(() => {
        const fetchDateFnsLocale = async () => {
            try {
                const dfLocale = await fetchLocale(normalizeLocale(dateTimeLocale));
                setDateFnsLocale(dfLocale);
            } catch (err) {
                console.warn('[useLocale] failed to fetch dateFns locale', err);
            }
        };

        void fetchDateFnsLocale();
    }, [ dateTimeLocale ]);

    return {
        locale,
        dateTimeLocale,
        dateFnsLocale
    };
}
