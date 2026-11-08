import test from 'node:test';
import assert from 'node:assert/strict';
import { extractSitemapUrls, extractSocialImageUrls } from './verify-build-artifact-helpers.mjs';

test('extractSocialImageUrls accepts the two canonical social meta tags', () => {
    const html = [
        '<meta property="og:image" content="https://cdn.example/og.png">',
        '<meta name="twitter:image" content="https://cdn.example/twitter.png">'
    ].join('\n');

    assert.deepEqual(extractSocialImageUrls(html), [
        'https://cdn.example/og.png',
        'https://cdn.example/twitter.png'
    ]);
});

test('extractSocialImageUrls ignores unrelated or incomplete meta tags', () => {
    const html = [
        '<meta name="description" content="ignored">',
        '<meta property="og:image">',
        '<meta property="og:title" content="ignored">',
        '<meta name="twitter:image" content="https://cdn.example/twitter.png">'
    ].join('\n');

    assert.deepEqual(extractSocialImageUrls(html), [ 'https://cdn.example/twitter.png' ]);
});

test('extractSitemapUrls returns trimmed loc values without regex backtracking', () => {
    const xml = '<urlset><url><loc> https://mulletaflix.com </loc></url><url><loc>https://ignored.example</loc></url></urlset>';

    assert.deepEqual(extractSitemapUrls(xml), [
        'https://mulletaflix.com',
        'https://ignored.example'
    ]);
});
