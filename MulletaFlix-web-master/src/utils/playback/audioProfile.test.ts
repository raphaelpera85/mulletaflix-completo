import { describe, expect, it } from 'vitest';

import { getAudioMaxValues } from './audioProfile';

describe('getAudioMaxValues', () => {
    it('keeps the lowest valid limit when multiple audio conditions exist', () => {
        expect(getAudioMaxValues({
            CodecProfiles: [{
                Type: 'Audio',
                Conditions: [
                    { Condition: 'LessThanEqual', Property: 'AudioBitrate', Value: '320000' },
                    { Condition: 'LessThanEqual', Property: 'AudioBitrate', Value: 192000 },
                    { Condition: 'LessThanEqual', Property: 'AudioSampleRate', Value: 48000 },
                    { Condition: 'LessThanEqual', Property: 'AudioSampleRate', Value: 44100 },
                    { Condition: 'LessThanEqual', Property: 'AudioBitDepth', Value: 24 },
                    { Condition: 'LessThanEqual', Property: 'AudioBitDepth', Value: 16 }
                ]
            }]
        })).toEqual({
            maxAudioBitrate: 192000,
            maxAudioSampleRate: 44100,
            maxAudioBitDepth: 16
        });
    });

    it('ignores non-numeric and non-audio conditions', () => {
        expect(getAudioMaxValues({
            CodecProfiles: [
                { Type: 'Video', Conditions: [{ Condition: 'LessThanEqual', Property: 'AudioBitrate', Value: 1 }] },
                { Type: 'Audio', Conditions: [
                    { Condition: 'Equals', Property: 'AudioBitrate', Value: 2 },
                    { Condition: 'LessThanEqual', Property: 'AudioBitrate', Value: 'unknown' }
                ] }
            ]
        })).toEqual({
            maxAudioBitrate: null,
            maxAudioSampleRate: null,
            maxAudioBitDepth: null
        });
    });
});
