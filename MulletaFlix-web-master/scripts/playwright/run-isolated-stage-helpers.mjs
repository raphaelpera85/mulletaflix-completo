import { randomUUID } from 'node:crypto';

export function createStageDirName(timestamp = Date.now(), randomId = randomUUID()) {
    return `mflx-playwright-stage-${timestamp}-${randomId.slice(0, 8)}`;
}

export function buildStageEnvironment(parentEnvironment, tempBase, overrides = {}) {
    return {
        ...parentEnvironment,
        TEMP: tempBase,
        TMP: tempBase,
        ...overrides
    };
}

export function buildStageLaunchArguments(stageDll, stageDataDir, webDir = '') {
    const argumentsList = [ stageDll, `--datadir=${stageDataDir}` ];
    if (webDir.trim()) {
        argumentsList.push(`--webdir=${webDir}`);
    }

    return argumentsList;
}

export function buildIsolatedNebulaConfiguration() {
    return '<NebulaFtpConfiguration>\n'
        + '  <Enabled>false</Enabled>\n'
        + '  <MonitorPaths />\n'
        + '  <StagePaths />\n'
        + '  <UseMappedDrive>false</UseMappedDrive>\n'
        + '  <DeleteSourceAfterUpload>false</DeleteSourceAfterUpload>\n'
        + '  <TelegramNotificationsEnabled>false</TelegramNotificationsEnabled>\n'
        + '  <TelegramNotificationChatIds />\n'
        + '  <TelegramNotificationIntervalSeconds>60</TelegramNotificationIntervalSeconds>\n'
        + '  <NotificationsEnabled>false</NotificationsEnabled>\n'
        + '  <NotificationsChannelIds />\n'
        + '  <NotificationsIntervalSeconds>60</NotificationsIntervalSeconds>\n'
        + '  <ChatId />\n'
        + '  <BotTokens />\n'
        + '  <ApiHash />\n'
        + '  <SupabaseKey />\n'
        + '  <SupabaseUrl />\n'
        + '  <SupabaseManagementToken />\n'
        + '  <SupabaseAutoBackup>false</SupabaseAutoBackup>\n'
        + '  <PublicServerUrl>http://127.0.0.1</PublicServerUrl>\n'
        + '</NebulaFtpConfiguration>\n';
}

export function buildIsolatedIntroSkipperConfiguration() {
    return '<?xml version="1.0" encoding="utf-8"?>\n'
        + '<PluginConfiguration xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">\n'
        + '  <AutoDetectIntros>false</AutoDetectIntros>\n'
        + '</PluginConfiguration>\n';
}
