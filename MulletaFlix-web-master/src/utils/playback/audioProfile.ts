export type AudioProfileValue = number | string | null;

export interface AudioProfileCondition {
    Condition?: string;
    Property?: string;
    Value?: AudioProfileValue;
}

export interface AudioCodecProfile {
    Type?: string;
    Conditions?: AudioProfileCondition[];
}

export interface AudioDeviceProfile {
    CodecProfiles?: AudioCodecProfile[];
}

export interface AudioMaxValues {
    maxAudioSampleRate: AudioProfileValue;
    maxAudioBitDepth: AudioProfileValue;
    maxAudioBitrate: AudioProfileValue;
}

const keepLowestLimit = (current: AudioProfileValue, candidate: AudioProfileValue | undefined): AudioProfileValue => {
    let lowest = current;
    if (candidate !== undefined) {
        const candidateNumber = Number(candidate);
        const currentNumber = Number(current);
        if (Number.isFinite(candidateNumber)
            && (current == null || !Number.isFinite(currentNumber) || candidateNumber < currentNumber)) {
            lowest = candidate;
        }
    }

    return lowest;
};

export const getAudioMaxValues = (deviceProfile: AudioDeviceProfile): AudioMaxValues => {
    let maxAudioSampleRate: AudioProfileValue = null;
    let maxAudioBitDepth: AudioProfileValue = null;
    let maxAudioBitrate: AudioProfileValue = null;

    for (const codecProfile of deviceProfile.CodecProfiles ?? []) {
        if (codecProfile.Type !== 'Audio') continue;

        for (const condition of codecProfile.Conditions ?? []) {
            if (condition.Condition !== 'LessThanEqual') continue;

            if (condition.Property === 'AudioBitDepth') {
                maxAudioBitDepth = keepLowestLimit(maxAudioBitDepth, condition.Value ?? null);
            } else if (condition.Property === 'AudioSampleRate') {
                maxAudioSampleRate = keepLowestLimit(maxAudioSampleRate, condition.Value ?? null);
            } else if (condition.Property === 'AudioBitrate') {
                maxAudioBitrate = keepLowestLimit(maxAudioBitrate, condition.Value ?? null);
            }
        }
    }

    return { maxAudioSampleRate, maxAudioBitDepth, maxAudioBitrate };
};
