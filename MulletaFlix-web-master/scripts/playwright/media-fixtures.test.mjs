import assert from 'node:assert/strict';
import { mkdtemp, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { LONG_PLAYBACK_FIXTURE, seedPlaywrightMediaFixtures } from './media-fixtures.mjs';

test('seeds playable movie and series fixtures with local metadata', async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), 'mfx-playwright-fixtures-'));
    const mediaRoot = path.join(root, 'media');
    const source = path.join(root, 'sample.mp4');

    try {
        await writeFile(source, Buffer.from('sample-video-fixture'));
        const result = await seedPlaywrightMediaFixtures(mediaRoot, source);

        assert.deepEqual(result, { movieCount: 4, episodeCount: 1 });
        const movieFiles = await readdir(path.join(mediaRoot, 'Filmes'));
        assert.equal(movieFiles.filter(name => name.endsWith('.mp4')).length, 4);
        assert.equal(movieFiles.filter(name => name.endsWith('.nfo')).length, 4);
        assert.equal(
            await readFile(path.join(mediaRoot, 'Filmes', 'Playwright Fixture 01 (2024).mp4'), 'utf8'),
            'sample-video-fixture'
        );
        assert.match(
            await readFile(path.join(mediaRoot, 'Series', 'Playwright Fixture Series', 'tvshow.nfo'), 'utf8'),
            /<title>Playwright Fixture Series<\/title>/
        );
        assert.match(
            await readFile(path.join(mediaRoot, 'Series', 'Playwright Fixture Series', 'Season 01', 'Playwright Fixture Series - S01E01.nfo'), 'utf8'),
            /<season>1<\/season>/
        );
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test('rejects an empty fixture source file', async () => {
    const root = await mkdtemp(path.join(os.tmpdir(), 'mfx-playwright-fixtures-'));
    const mediaRoot = path.join(root, 'media');
    const source = path.join(root, 'empty.mp4');

    try {
        await writeFile(source, '');
        await assert.rejects(seedPlaywrightMediaFixtures(mediaRoot, source), /non-empty file/);
    } finally {
        await rm(root, { recursive: true, force: true });
    }
});

test('long concurrent-playback media is opt-in and has a stable fixture identity', async () => {
    assert.equal(LONG_PLAYBACK_FIXTURE.title, 'Playwright Long Playback Fixture');
    assert.equal(LONG_PLAYBACK_FIXTURE.durationSeconds, 300);
});
