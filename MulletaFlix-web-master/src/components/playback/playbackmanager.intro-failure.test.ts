import { describe, expect, it, vi } from 'vitest';
import appSettings from '../../scripts/settings/appSettings';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { PlaybackManager } from './playbackmanager';

describe('PlaybackManager intro failure cleanup', () => {
    it('destroys the player that failed before playback started', async () => {
        const apiClient = {
            getIntros: vi.fn().mockResolvedValue({
                Items: [{
                    Id: 'intro-1',
                    ServerId: 'server-1',
                    MediaType: 'Video',
                    Type: 'Movie',
                    Name: 'Synthetic intro'
                }]
            }),
            getEndpointInfo: vi.fn().mockResolvedValue({ IsInNetwork: false }),
            getSavedEndpointInfo: vi.fn().mockReturnValue({ IsInNetwork: false }),
            getCurrentUserId: vi.fn().mockReturnValue('user-1'),
            getItem: vi.fn().mockResolvedValue({ MediaStreams: [] }),
            getCurrentUser: vi.fn().mockResolvedValue({
                Configuration: {
                    RememberAudioSelections: false,
                    RememberSubtitleSelections: false
                }
            })
        };
        const player = {
            name: 'Html Video Player',
            id: 'htmlvideoplayer',
            isLocalPlayer: true,
            canPlayMediaType: vi.fn().mockReturnValue(true),
            canPlayItem: vi.fn().mockReturnValue(true),
            getDeviceProfile: vi.fn().mockRejectedValue(new Error('device profile unavailable')),
            destroy: vi.fn(),
            stop: vi.fn().mockResolvedValue(undefined)
        };
        const manager = new PlaybackManager();
        manager.getPlayers().push(player);

        vi.spyOn(ServerConnections, 'getApiClient').mockReturnValue(apiClient as any);
        vi.spyOn(appSettings, 'enableAutomaticBitrateDetection').mockReturnValue(false);
        vi.spyOn(appSettings, 'maxStreamingBitrate').mockReturnValue(1500000);

        await manager.play({
            items: [{
                Id: 'movie-1',
                ServerId: 'server-1',
                MediaType: 'Video',
                Type: 'Movie',
                Name: 'Synthetic movie'
            }],
            fullscreen: false,
            startIndex: 0
        });

        expect(player.destroy).toHaveBeenCalled();
    });
});
