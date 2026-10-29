import { copyFile, mkdir, stat, writeFile } from 'node:fs/promises';
import { spawn } from 'node:child_process';
import path from 'node:path';

const movies = [
    { name: 'Playwright Fixture 01 (2024)', title: 'Playwright Fixture 01' },
    { name: 'Playwright Fixture 02 (2024)', title: 'Playwright Fixture 02' },
    { name: 'Playwright Fixture 03 (2024)', title: 'Playwright Fixture 03' },
    { name: 'Playwright Fixture 04 (2024)', title: 'Playwright Fixture 04' }
];

export const LONG_PLAYBACK_FIXTURE = {
    name: 'Playwright Long Playback Fixture (2024)',
    title: 'Playwright Long Playback Fixture',
    durationSeconds: 300
};

async function createLongPlaybackFixture(ffmpegPath, sourcePath, outputPath, durationSeconds) {
    await new Promise((resolve, reject) => {
        const child = spawn(ffmpegPath, [
            '-hide_banner',
            '-loglevel', 'error',
            '-y',
            '-stream_loop', '-1',
            '-i', sourcePath,
            '-t', String(durationSeconds),
            '-map', '0:v:0',
            '-map', '0:a:0?',
            '-c', 'copy',
            '-movflags', '+faststart',
            outputPath
        ], { stdio: [ 'ignore', 'ignore', 'pipe' ] });

        let stderr = '';
        child.stderr.setEncoding('utf8');
        child.stderr.on('data', chunk => {
            stderr = `${stderr}${chunk}`.slice(-4_000);
        });
        child.once('error', reject);
        child.once('close', code => {
            if (code === 0) {
                resolve();
            } else {
                reject(new Error(`Unable to generate long playback fixture using ${ffmpegPath} (exit ${code}): ${stderr.trim()}`));
            }
        });
    });
}

export async function seedPlaywrightMediaFixtures(mediaRoot, sampleVideoPath, { ffmpegPath } = {}) {
    const videoStat = await stat(sampleVideoPath);
    if (!videoStat.isFile() || videoStat.size === 0) {
        throw new Error(`Playwright media fixture source is not a non-empty file: ${sampleVideoPath}`);
    }

    const moviesRoot = path.join(mediaRoot, 'Filmes');
    const episodeRoot = path.join(mediaRoot, 'Series', 'Playwright Fixture Series', 'Season 01');
    await Promise.all([
        mkdir(moviesRoot, { recursive: true }),
        mkdir(episodeRoot, { recursive: true })
    ]);

    for (const movie of movies) {
        const videoPath = path.join(moviesRoot, `${movie.name}.mp4`);
        await copyFile(sampleVideoPath, videoPath);
        await writeFile(path.join(moviesRoot, `${movie.name}.nfo`), [
            '<movie>',
            `  <title>${movie.title}</title>`,
            '  <year>2024</year>',
            '  <plot>Deterministic local fixture for Playwright.</plot>',
            '  <lockdata>true</lockdata>',
            '</movie>',
            ''
        ].join('\n'), 'utf8');
    }

    if (ffmpegPath) {
        const videoPath = path.join(moviesRoot, `${LONG_PLAYBACK_FIXTURE.name}.mp4`);
        await createLongPlaybackFixture(ffmpegPath, sampleVideoPath, videoPath, LONG_PLAYBACK_FIXTURE.durationSeconds);
        await writeFile(path.join(moviesRoot, `${LONG_PLAYBACK_FIXTURE.name}.nfo`), [
            '<movie>',
            `  <title>${LONG_PLAYBACK_FIXTURE.title}</title>`,
            '  <year>2024</year>',
            '  <plot>Long local fixture for concurrent playback validation.</plot>',
            '  <lockdata>true</lockdata>',
            '</movie>',
            ''
        ].join('\n'), 'utf8');
    }

    await writeFile(path.join(mediaRoot, 'Series', 'Playwright Fixture Series', 'tvshow.nfo'), [
        '<tvshow>',
        '  <title>Playwright Fixture Series</title>',
        '  <year>2024</year>',
        '  <plot>Deterministic local series fixture for Playwright.</plot>',
        '  <lockdata>true</lockdata>',
        '</tvshow>',
        ''
    ].join('\n'), 'utf8');

    const episodeName = 'Playwright Fixture Series - S01E01';
    await copyFile(sampleVideoPath, path.join(episodeRoot, `${episodeName}.mp4`));
    await writeFile(path.join(episodeRoot, `${episodeName}.nfo`), [
        '<episodedetails>',
        '  <title>Fixture Episode</title>',
        '  <season>1</season>',
        '  <episode>1</episode>',
        '  <plot>Deterministic local episode fixture for Playwright.</plot>',
        '  <lockdata>true</lockdata>',
        '</episodedetails>',
        ''
    ].join('\n'), 'utf8');

    return { movieCount: movies.length + (ffmpegPath ? 1 : 0), episodeCount: 1 };
}
