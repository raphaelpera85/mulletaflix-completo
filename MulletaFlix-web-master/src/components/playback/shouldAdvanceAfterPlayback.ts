interface PlaybackAdvanceOptions {
    hasNextItem: boolean;
    isIntro: boolean;
    nextMediaType: string | null | undefined;
    enableNextEpisodeAutoPlay: boolean;
}

export function shouldAdvanceAfterPlayback({
    hasNextItem,
    isIntro,
    nextMediaType,
    enableNextEpisodeAutoPlay
}: PlaybackAdvanceOptions): boolean {
    if (!hasNextItem) {
        return false;
    }

    return isIntro || enableNextEpisodeAutoPlay || nextMediaType !== 'Video';
}
