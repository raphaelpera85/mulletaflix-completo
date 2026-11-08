const socialImageAttributes = new Set([ 'property="og:image"', 'name="twitter:image"' ]);

export function extractSitemapUrls(sitemap) {
    const urls = [];
    const openingTag = '<loc>';
    const closingTag = '</loc>';
    let searchFrom = 0;

    while (searchFrom < sitemap.length) {
        const start = sitemap.indexOf(openingTag, searchFrom);
        if (start < 0) break;

        const valueStart = start + openingTag.length;
        const valueEnd = sitemap.indexOf(closingTag, valueStart);
        if (valueEnd < 0) break;

        urls.push(sitemap.slice(valueStart, valueEnd).trim());
        searchFrom = valueEnd + closingTag.length;
    }

    return urls;
}

export function extractSocialImageUrls(indexHtml) {
    const urls = [];

    for (const fragment of indexHtml.split('<meta ')) {
        const tagEnd = fragment.indexOf('>');
        if (tagEnd < 0) continue;

        const attributes = fragment.slice(0, tagEnd);
        const hasSocialImageAttribute = [...socialImageAttributes].some((attribute) => attributes.includes(attribute));
        if (!hasSocialImageAttribute) continue;

        const contentMarker = 'content="';
        const contentStart = attributes.indexOf(contentMarker);
        if (contentStart < 0) continue;

        const valueStart = contentStart + contentMarker.length;
        const valueEnd = attributes.indexOf('"', valueStart);
        if (valueEnd > valueStart) urls.push(attributes.slice(valueStart, valueEnd));
    }

    return urls;
}
