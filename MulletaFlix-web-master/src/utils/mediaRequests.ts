export interface MediaCatalogTitle {
    Title: string;
    MediaType: string;
    Year?: number;
}

export interface MediaRequestActivity {
    Name?: string | null;
    Overview?: string | null;
}

const requestTitlePrefix = /^Solicitação de mídia:\s*/i;
const yearPattern = /(?:19|20)\d{2}/;

const lastChar = (value: string) => value.slice(-1);

const stripTrailingYear = (value: string) => {
    const trimmed = value.trimEnd();
    const lastCharacter = lastChar(trimmed);
    const hasClosingBracket = lastCharacter === ')' || lastCharacter === ']';
    const withoutBracket = hasClosingBracket ? trimmed.slice(0, -1).trimEnd() : trimmed;

    const yearMatch = /\d{4}$/.exec(withoutBracket);
    if (!yearMatch || !yearPattern.test(yearMatch[0])) return value;

    let prefix = withoutBracket.slice(0, yearMatch.index).trimEnd();
    if (hasClosingBracket) {
        const openingCharacter = lastChar(prefix);
        if (openingCharacter === '(' || openingCharacter === '[') {
            prefix = prefix.slice(0, -1).trimEnd();
        }
    } else {
        const separatorCharacter = lastChar(prefix);
        if (separatorCharacter === '-' || separatorCharacter === '–') {
            prefix = prefix.slice(0, -1).trimEnd();
        }
    }

    return prefix;
};

const normalizeTitle = (value: string) => {
    const normalized = value.normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .toLocaleLowerCase();
    return stripTrailingYear(normalized)
        .replace(/[^\p{L}\p{N}]+/gu, ' ')
        .trim();
};

const getRequestTitle = (entry: MediaRequestActivity) => (entry.Name || '').replace(requestTitlePrefix, '').trim();

const getYear = (title: string, overview?: string | null) => {
    const overviewYear = overview ? yearPattern.exec(overview)?.[0] : undefined;
    const titleYear = yearPattern.exec(title)?.[0];
    const rawYear = overviewYear || titleYear;
    return rawYear ? Number(rawYear) : undefined;
};

const getRequestMediaType = (overview?: string | null) => overview?.split('·', 1)[0]?.trim().toLocaleLowerCase() || '';

export const isMediaRequestIncluded = (entry: MediaRequestActivity, catalog: readonly MediaCatalogTitle[]) => {
    const title = getRequestTitle(entry);
    const normalizedTitle = normalizeTitle(title);
    if (!normalizedTitle) return false;

    const mediaType = getRequestMediaType(entry.Overview);
    const year = getYear(title, entry.Overview);
    const matches = catalog.filter(item => {
        const catalogYear = item.Year ?? getYear(item.Title);
        return normalizeTitle(item.Title) === normalizedTitle
            && (!mediaType || item.MediaType.trim().toLocaleLowerCase() === mediaType)
            && (year === undefined || catalogYear === year);
    });

    // Don't hide requests when the catalog contains multiple possible works
    // (for example, remakes with the same title and category).
    const identities = new Set(matches.map(item => {
        const catalogYear = item.Year ?? getYear(item.Title);
        return `${item.MediaType.trim().toLocaleLowerCase()}:${catalogYear ?? 'unknown'}`;
    }));
    return identities.size === 1;
};

export const classifyMediaRequests = <T extends MediaRequestActivity>(entries: readonly T[], catalog: readonly MediaCatalogTitle[]) => {
    const pending: T[] = [];
    const included: T[] = [];

    for (const entry of entries) {
        (isMediaRequestIncluded(entry, catalog) ? included : pending).push(entry);
    }

    return { pending, included };
};
