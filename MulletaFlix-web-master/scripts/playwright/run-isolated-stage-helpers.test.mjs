import assert from 'node:assert/strict';
import test from 'node:test';
import {
    buildIsolatedIntroSkipperConfiguration,
    buildIsolatedNebulaConfiguration,
    buildStageEnvironment,
    buildStageLaunchArguments,
    createStageDirName
} from './run-isolated-stage-helpers.mjs';

test('createStageDirName uses a timestamp and UUID-shaped entropy', () => {
    const name = createStageDirName(1700000000000, '12345678-1234-4abc-8def-123456789abc');

    assert.match(name, /^mflx-playwright-stage-1700000000000-12345678$/);
});

test('buildStageEnvironment isolates TEMP and TMP without mutating the parent environment', () => {
    const parent = { PATH: 'safe', TEMP: 'old-temp', TMP: 'old-tmp' };
    const child = buildStageEnvironment(parent, 'C:\\Users\\Raphael\\.mulletaflix-tmp', {
        PW_ISOLATED_STAGE_DATA_DIR: 'C:\\Users\\Raphael\\.mulletaflix-tmp\\mflx-playwright-stage-test'
    });

    assert.deepEqual(parent, { PATH: 'safe', TEMP: 'old-temp', TMP: 'old-tmp' });
    assert.equal(child.PATH, 'safe');
    assert.equal(child.TEMP, 'C:\\Users\\Raphael\\.mulletaflix-tmp');
    assert.equal(child.TMP, 'C:\\Users\\Raphael\\.mulletaflix-tmp');
    assert.equal(child.PW_ISOLATED_STAGE_DATA_DIR, 'C:\\Users\\Raphael\\.mulletaflix-tmp\\mflx-playwright-stage-test');
});

test('buildStageLaunchArguments can point an isolated stage at a current web bundle', () => {
    assert.deepEqual(
        buildStageLaunchArguments('stage/MulletaFlix.dll', 'stage-data', 'web-dist'),
        [ 'stage/MulletaFlix.dll', '--datadir=stage-data', '--webdir=web-dist' ]
    );
});

test('buildIsolatedNebulaConfiguration disables external notifications and credentials', () => {
    const configuration = buildIsolatedNebulaConfiguration();

    assert.match(configuration, /<Enabled>false<\/Enabled>/);
    assert.match(configuration, /<TelegramNotificationsEnabled>false<\/TelegramNotificationsEnabled>/);
    assert.match(configuration, /<NotificationsEnabled>false<\/NotificationsEnabled>/);
    assert.match(configuration, /<ChatId\s*\/>/);
    assert.match(configuration, /<NotificationsChannelIds\s*\/>/);
    assert.match(configuration, /<TelegramNotificationChatIds\s*\/>/);
    assert.match(configuration, /<BotTokens\s*\/>/);
    assert.match(configuration, /<ApiHash\s*\/>/);
    assert.match(configuration, /<SupabaseKey\s*\/>/);
    assert.doesNotMatch(configuration, /-[0-9]{8,}|duckdns|supabase\.co/);
});

test('buildIsolatedIntroSkipperConfiguration disables automatic media analysis', () => {
    const configuration = buildIsolatedIntroSkipperConfiguration();

    assert.match(configuration, /<PluginConfiguration\b/);
    assert.match(configuration, /<AutoDetectIntros>false<\/AutoDetectIntros>/);
    assert.doesNotMatch(configuration, /<AutoDetectIntros>true<\/AutoDetectIntros>/);
});
