import { playbackManager } from '../../../../components/playback/playbackmanager';
import QueueManager from './QueueManager';
import GenericPlayer from '../../core/players/GenericPlayer';

const pm: any = playbackManager;

type SyncPlayCommand = {
    Name: string;
    Arguments?: {
        RepeatMode?: string;
        ShuffleMode?: string;
    };
};

class NoActivePlayer extends GenericPlayer {
    static readonly type: string = 'default';

    private readonly syncPlayManager: any;

    constructor(player: any, _syncPlayManager: any) {
        super(player, _syncPlayManager);
        this.syncPlayManager = _syncPlayManager;
    }

    localBindToPlayer(): void {
        if (pm.syncPlayEnabled) return;

        pm._localPlayPause = pm.playPause;
        pm._localUnpause = pm.unpause;
        pm._localPause = pm.pause;
        pm._localSeek = pm.seek;
        pm._localSendCommand = pm.sendCommand;

        pm.playPause = this.playPauseRequest.bind(this);
        pm.unpause = this.unpauseRequest.bind(this);
        pm.pause = this.pauseRequest.bind(this);
        pm.seek = this.seekRequest.bind(this);
        pm.sendCommand = this.sendCommandRequest.bind(this);

        pm._localPlayQueueManager = pm._playQueueManager;

        pm._localPlay = pm.play;
        pm._localSetCurrentPlaylistItem = pm.setCurrentPlaylistItem;
        pm._localClearQueue = pm.clearQueue;
        pm._localRemoveFromPlaylist = pm.removeFromPlaylist;
        pm._localMovePlaylistItem = pm.movePlaylistItem;
        pm._localQueue = pm.queue;
        pm._localQueueNext = pm.queueNext;

        pm._localNextTrack = pm.nextTrack;
        pm._localPreviousTrack = pm.previousTrack;

        pm._localSetRepeatMode = pm.setRepeatMode;
        pm._localSetQueueShuffleMode = pm.setQueueShuffleMode;
        pm._localToggleQueueShuffleMode = pm.toggleQueueShuffleMode;

        pm._playQueueManager = new QueueManager(this.manager as any);

        pm.play = this.playRequest.bind(this);
        pm.setCurrentPlaylistItem = this.setCurrentPlaylistItemRequest.bind(this);
        pm.clearQueue = this.clearQueueRequest.bind(this);
        pm.removeFromPlaylist = this.removeFromPlaylistRequest.bind(this);
        pm.movePlaylistItem = this.movePlaylistItemRequest.bind(this);
        pm.queue = this.queueRequest.bind(this);
        pm.queueNext = this.queueNextRequest.bind(this);

        pm.nextTrack = this.nextTrackRequest.bind(this);
        pm.previousTrack = this.previousTrackRequest.bind(this);

        pm.setRepeatMode = this.setRepeatModeRequest.bind(this);
        pm.setQueueShuffleMode = this.setQueueShuffleModeRequest.bind(this);
        pm.toggleQueueShuffleMode = this.toggleQueueShuffleModeRequest.bind(this);

        pm.syncPlayEnabled = true;
    }

    localUnbindFromPlayer(): void {
        if (!pm.syncPlayEnabled) return;

        pm.playPause = pm._localPlayPause;
        pm.unpause = pm._localUnpause;
        pm.pause = pm._localPause;
        pm.seek = pm._localSeek;
        pm.sendCommand = pm._localSendCommand;

        pm._playQueueManager = pm._localPlayQueueManager;

        pm.play = pm._localPlay;
        pm.setCurrentPlaylistItem = pm._localSetCurrentPlaylistItem;
        pm.clearQueue = pm._localClearQueue;
        pm.removeFromPlaylist = pm._localRemoveFromPlaylist;
        pm.movePlaylistItem = pm._localMovePlaylistItem;
        pm.queue = pm._localQueue;
        pm.queueNext = pm._localQueueNext;

        pm.nextTrack = pm._localNextTrack;
        pm.previousTrack = pm._localPreviousTrack;

        pm.setRepeatMode = pm._localSetRepeatMode;
        pm.setQueueShuffleMode = pm._localSetQueueShuffleMode;
        pm.toggleQueueShuffleMode = pm._localToggleQueueShuffleMode;

        pm.syncPlayEnabled = false;
    }

    playPauseRequest(): void {
        const controller = this.syncPlayManager.getController();
        controller.playPause();
    }

    unpauseRequest(): void {
        const controller = this.syncPlayManager.getController();
        controller.unpause();
    }

    pauseRequest(): void {
        const controller = this.syncPlayManager.getController();
        controller.pause();
    }

    seekRequest(positionTicks: number): void {
        const controller = this.syncPlayManager.getController();
        controller.seek(positionTicks);
    }

    sendCommandRequest(command: SyncPlayCommand): void {
        console.debug('SyncPlay sendCommand:', command.Name, command);
        const controller = this.syncPlayManager.getController();
        const playerWrapper = this.syncPlayManager.getPlayerWrapper();

        const defaultAction = (_command: SyncPlayCommand): void => {
            playerWrapper.localSendCommand(_command);
        };

        const ignoreCallback = (): void => {
            // Do nothing.
        };

        const SetRepeatModeCallback = (_command: SyncPlayCommand): void => {
            controller.setRepeatMode(_command.Arguments!.RepeatMode!);
        };

        const SetShuffleQueueCallback = (_command: SyncPlayCommand): void => {
            controller.setShuffleMode(_command.Arguments!.ShuffleMode!);
        };

        const overrideCommands: Record<string, (command: SyncPlayCommand) => void> = {
            PlaybackRate: ignoreCallback,
            SetRepeatMode: SetRepeatModeCallback,
            SetShuffleQueue: SetShuffleQueueCallback
        };

        const commandHandler = overrideCommands[command.Name];
        if (typeof commandHandler === 'function') {
            commandHandler(command);
        } else {
            defaultAction(command);
        }
    }

    localUnpause(): void {
        if (pm.syncPlayEnabled) {
            pm._localUnpause(this.player);
        } else {
            pm.unpause(this.player);
        }
    }

    localPause(): void {
        if (pm.syncPlayEnabled) {
            pm._localPause(this.player);
        } else {
            pm.pause(this.player);
        }
    }

    localSeek(positionTicks: number): void {
        if (pm.syncPlayEnabled) {
            pm._localSeek(positionTicks, this.player);
        } else {
            pm.seek(positionTicks, this.player);
        }
    }

    localStop(): void {
        pm.stop(this.player);
    }

    localSendCommand(cmd: { Name: string }): void {
        if (pm.syncPlayEnabled) {
            pm._localSendCommand(cmd, this.player);
        } else {
            pm.sendCommand(cmd, this.player);
        }
    }

    playRequest(options: any): any {
        const controller = this.syncPlayManager.getController();
        return controller.play(options);
    }

    setCurrentPlaylistItemRequest(playlistItemId: string): void {
        this.syncPlayManager.getController().setCurrentPlaylistItem(playlistItemId);
    }

    clearQueueRequest(clearPlayingItem: boolean): void {
        this.syncPlayManager.getController().clearPlaylist(clearPlayingItem);
    }

    removeFromPlaylistRequest(playlistItemIds: string[]): void {
        this.syncPlayManager.getController().removeFromPlaylist(playlistItemIds);
    }

    movePlaylistItemRequest(playlistItemId: string, newIndex: number): void {
        this.syncPlayManager.getController().movePlaylistItem(playlistItemId, newIndex);
    }

    queueRequest(options: any): void {
        this.syncPlayManager.getController().queue(options);
    }

    queueNextRequest(options: any): void {
        this.syncPlayManager.getController().queueNext(options);
    }

    nextTrackRequest(): void {
        this.syncPlayManager.getController().nextItem();
    }

    previousTrackRequest(): void {
        this.syncPlayManager.getController().previousItem();
    }

    setRepeatModeRequest(mode: string): void {
        this.syncPlayManager.getController().setRepeatMode(mode);
    }

    setQueueShuffleModeRequest(mode: string): void {
        this.syncPlayManager.getController().setShuffleMode(mode);
    }

    toggleQueueShuffleModeRequest(): void {
        this.syncPlayManager.getController().toggleShuffleMode();
    }

    localPlay(options: any): any {
        if (pm.syncPlayEnabled) {
            return pm._localPlay(options);
        }

        return pm.play(options);
    }

    localSetCurrentPlaylistItem(playlistItemId: string): any {
        if (pm.syncPlayEnabled) {
            return pm._localSetCurrentPlaylistItem(playlistItemId, this.player);
        }

        return pm.setCurrentPlaylistItem(playlistItemId, this.player);
    }

    localRemoveFromPlaylist(playlistItemIds: string[]): any {
        if (pm.syncPlayEnabled) {
            return pm._localRemoveFromPlaylist(playlistItemIds, this.player);
        }

        return pm.removeFromPlaylist(playlistItemIds, this.player);
    }

    localMovePlaylistItem(playlistItemId: string, newIndex: number): any {
        if (pm.syncPlayEnabled) {
            return pm._localMovePlaylistItem(playlistItemId, newIndex, this.player);
        }

        return pm.movePlaylistItem(playlistItemId, newIndex, this.player);
    }

    localQueue(options: any): any {
        if (pm.syncPlayEnabled) {
            return pm._localQueue(options, this.player);
        }

        return pm.queue(options, this.player);
    }

    localQueueNext(options: any): any {
        if (pm.syncPlayEnabled) {
            return pm._localQueueNext(options, this.player);
        }

        return pm.queueNext(options, this.player);
    }

    localNextItem(): void {
        if (pm.syncPlayEnabled) {
            pm._localNextTrack(this.player);
        } else {
            pm.nextTrack(this.player);
        }
    }

    localPreviousItem(): void {
        if (pm.syncPlayEnabled) {
            pm._localPreviousTrack(this.player);
        } else {
            pm.previousTrack(this.player);
        }
    }

    localSetRepeatMode(value: string): void {
        if (pm.syncPlayEnabled) {
            pm._localSetRepeatMode(value, this.player);
        } else {
            pm.setRepeatMode(value, this.player);
        }
    }

    localSetQueueShuffleMode(value: string): void {
        if (pm.syncPlayEnabled) {
            pm._localSetQueueShuffleMode(value, this.player);
        } else {
            pm.setQueueShuffleMode(value, this.player);
        }
    }

    localToggleQueueShuffleMode(): void {
        if (pm.syncPlayEnabled) {
            pm._localToggleQueueShuffleMode(this.player);
        } else {
            pm.toggleQueueShuffleMode(this.player);
        }
    }
}

export default NoActivePlayer;
