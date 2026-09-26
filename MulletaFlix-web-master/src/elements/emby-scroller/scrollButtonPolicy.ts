interface ScrollButtonPolicyOptions {
    horizontal: boolean;
    setting: string | null;
    desktop: boolean;
    touch: boolean;
}

export const shouldEnableScrollButtons = ({ horizontal, setting, desktop, touch }: ScrollButtonPolicyOptions): boolean => {
    return horizontal
        && setting !== 'false'
        && (setting === 'true' || (desktop && !touch));
};
