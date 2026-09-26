/**
 * Play an intro when possible, then start the main item if the intro cannot play.
 * Intro failures are deliberately non-fatal to the requested media playback.
 */
export async function playIntroThenMain<T>(
    playIntro: () => Promise<T>,
    onIntroError: (error: unknown) => void,
    playMain: () => Promise<T>
): Promise<T | unknown> {
    try {
        return await playIntro();
    } catch (error) {
        onIntroError(error);
        return playMain();
    }
}
