import alert from 'components/alert';
import focusManager from 'components/focusManager';
import { playbackManager } from 'components/playback/playbackmanager';
import { pluginManager } from 'components/pluginManager';
import { appRouter } from 'components/router/appRouter';
import toast from 'components/toast/toast';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import inputManager from 'scripts/inputManager';
import Events from 'utils/events';
import { PluginType } from 'types/plugin';
import type { ItemDto } from 'types/base/models/item-dto';
import { OutboundWebSocketMessageType } from '@jellyfin/sdk/lib/websocket';

interface CommandData {
    Arguments: {
        ItemId?: string;
        Header?: string;
        Text?: string;
        TimeoutMs?: number;
        RepeatMode?: string;
        ShuffleMode?: string;
        Volume?: number;
        Index?: string;
        String?: string;
        [key: string]: unknown;
    };
    Name: string;
}

interface PlayData {
    PlayCommand: string;
    ItemIds: string[];
    StartPositionTicks?: number;
    MediaSourceId?: string;
    AudioStreamIndex?: number;
    SubtitleStreamIndex?: number;
    StartIndex?: number;
    [key: string]: unknown;
}

interface PlaystateData {
    Command: string;
    SeekPositionTicks?: number;
    [key: string]: unknown;
}

interface NotificationApiClient {
    serverId(): string;
    serverInfo(): { Id?: string };
    getCurrentUserId(): string;
    getItem(userId: string, itemId: string): Promise<unknown>;
    subscribe(messageTypes: unknown[], callback: (message: unknown) => void): void;
}

const serverNotifications: Record<string, unknown> = {};

function notifyApp(): void {
    inputManager.notify();
}

function displayMessage(cmd: CommandData): void {
    const args = cmd.Arguments;
    if (args.TimeoutMs) {
        toast(args.Text ?? '');
    } else {
        void alert({ title: args.Header ?? '', text: args.Text ?? '' });
    }
}

function displayContent(cmd: CommandData, apiClient: NotificationApiClient): void {
    if (!playbackManager.isPlayingLocally(['Video', 'Book'])) {
        appRouter.showItem(cmd.Arguments.ItemId!, apiClient.serverId());
    }
}

function playTrailers(apiClient: NotificationApiClient, itemId: string): void {
    apiClient.getItem(apiClient.getCurrentUserId(), itemId).then(function (item) {
        playbackManager.playTrailers(item as ItemDto).catch(() => undefined);
    }).catch(() => undefined);
}

function processGeneralCommand(cmd: CommandData, apiClient: NotificationApiClient): void {
    console.debug('Received command: ' + cmd.Name);
    switch (cmd.Name) {
        case 'Select':
            inputManager.handleCommand('select');
            return;
        case 'Back':
            inputManager.handleCommand('back');
            return;
        case 'MoveUp':
            inputManager.handleCommand('up');
            return;
        case 'MoveDown':
            inputManager.handleCommand('down');
            return;
        case 'MoveLeft':
            inputManager.handleCommand('left');
            return;
        case 'MoveRight':
            inputManager.handleCommand('right');
            return;
        case 'PageUp':
            inputManager.handleCommand('pageup');
            return;
        case 'PageDown':
            inputManager.handleCommand('pagedown');
            return;
        case 'PlayTrailers':
            playTrailers(apiClient, cmd.Arguments.ItemId!);
            break;
        case 'SetRepeatMode':
            playbackManager.setRepeatMode(cmd.Arguments.RepeatMode);
            break;
        case 'SetShuffleQueue':
            playbackManager.setQueueShuffleMode(cmd.Arguments.ShuffleMode);
            break;
        case 'VolumeUp':
            inputManager.handleCommand('volumeup');
            return;
        case 'VolumeDown':
            inputManager.handleCommand('volumedown');
            return;
        case 'ChannelUp':
            inputManager.handleCommand('channelup');
            return;
        case 'ChannelDown':
            inputManager.handleCommand('channeldown');
            return;
        case 'Mute':
            inputManager.handleCommand('mute');
            return;
        case 'Unmute':
            inputManager.handleCommand('unmute');
            return;
        case 'ToggleMute':
            inputManager.handleCommand('togglemute');
            return;
        case 'SetVolume':
            notifyApp();
            playbackManager.setVolume(cmd.Arguments.Volume);
            break;
        case 'SetAudioStreamIndex':
            notifyApp();
            playbackManager.setAudioStreamIndex(parseInt(cmd.Arguments.Index!, 10));
            break;
        case 'SetSubtitleStreamIndex':
            notifyApp();
            playbackManager.setSubtitleStreamIndex(parseInt(cmd.Arguments.Index!, 10));
            break;
        case 'ToggleFullscreen':
            inputManager.handleCommand('togglefullscreen');
            return;
        case 'GoHome':
            inputManager.handleCommand('home');
            return;
        case 'GoToSettings':
            inputManager.handleCommand('settings');
            return;
        case 'DisplayContent':
            displayContent(cmd, apiClient);
            break;
        case 'GoToSearch':
            inputManager.handleCommand('search');
            return;
        case 'DisplayMessage':
            displayMessage(cmd);
            break;
        case 'ToggleOsd':
        case 'ToggleContextMenu':
        case 'SendKey':
            // todo
            break;
        case 'SendString':
            focusManager.sendText(cmd.Arguments.String!);
            break;
        default:
            console.debug('processGeneralCommand does not recognize: ' + cmd.Name);
            break;
    }

    notifyApp();
}

function onPlay({ Data }: { Data: PlayData }, apiClient: NotificationApiClient): void {
    notifyApp();
    const serverId = apiClient.serverInfo().Id;
    if (Data.PlayCommand === 'PlayNext') {
        playbackManager.queueNext({ ids: Data.ItemIds, serverId });
    } else if (Data.PlayCommand === 'PlayLast') {
        playbackManager.queue({ ids: Data.ItemIds, serverId });
    } else {
        playbackManager.play({
            ids: Data.ItemIds,
            startPositionTicks: Data.StartPositionTicks,
            mediaSourceId: Data.MediaSourceId,
            audioStreamIndex: Data.AudioStreamIndex,
            subtitleStreamIndex: Data.SubtitleStreamIndex,
            startIndex: Data.StartIndex,
            serverId
        });
    }
}

function onPlaystate({ Data }: { Data: PlaystateData }): void {
    if (Data.Command === 'Stop') {
        inputManager.handleCommand('stop');
    } else if (Data.Command === 'Pause') {
        inputManager.handleCommand('pause');
    } else if (Data.Command === 'Unpause') {
        inputManager.handleCommand('play');
    } else if (Data.Command === 'PlayPause') {
        inputManager.handleCommand('playpause');
    } else if (Data.Command === 'Seek') {
        playbackManager.seek(Data.SeekPositionTicks);
    } else if (Data.Command === 'NextTrack') {
        inputManager.handleCommand('next');
    } else if (Data.Command === 'PreviousTrack') {
        inputManager.handleCommand('previous');
    } else if (Data.Command === 'Rewind') {
        inputManager.handleCommand('rewind');
    } else if (Data.Command === 'FastForward') {
        inputManager.handleCommand('fastforward');
    } else {
        notifyApp();
    }
}

function subscribeToApiClient(apiClient: unknown): void {
    const client = apiClient as NotificationApiClient;
    client.subscribe([OutboundWebSocketMessageType.Play], (msg) => onPlay(msg as { Data: PlayData }, client));
    client.subscribe([OutboundWebSocketMessageType.Playstate], (msg) => onPlaystate(msg as { Data: PlaystateData }));
    client.subscribe([OutboundWebSocketMessageType.GeneralCommand], (message) => {
        const { Data } = message as { Data: CommandData };
        processGeneralCommand(Data, client);
    });
    client.subscribe([OutboundWebSocketMessageType.SyncPlayCommand], (message) => {
        const { Data } = message as { Data: unknown };
        pluginManager.firstOfType(PluginType.SyncPlay)?.instance.Manager.processCommand(Data, client);
    });
    client.subscribe([OutboundWebSocketMessageType.SyncPlayGroupUpdate], (message) => {
        const { Data } = message as { Data: unknown };
        pluginManager.firstOfType(PluginType.SyncPlay)?.instance.Manager.processGroupUpdate(Data, client);
        Events.trigger(serverNotifications, OutboundWebSocketMessageType.SyncPlayGroupUpdate, [client, Data]);
    });
}

export function initializeServerConnections(): void {
    ServerConnections.getApiClients().forEach(subscribeToApiClient);
    Events.on(ServerConnections, 'apiclientcreated', function (_event: unknown, newApiClient: unknown) {
        subscribeToApiClient(newApiClient);
    });
}

(window as unknown as { ServerNotifications: typeof serverNotifications }).ServerNotifications = serverNotifications;

export default serverNotifications;
