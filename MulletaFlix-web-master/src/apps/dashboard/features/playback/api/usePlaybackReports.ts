import type { AxiosRequestConfig } from 'axios';
import type { Api } from '@jellyfin/sdk';
import { useQuery } from '@tanstack/react-query';

import { useApi } from 'hooks/useApi';

export interface PlaybackReportQuery {
    userId?: string;
    itemId?: string;
    deviceId?: string;
    libraryId?: string;
    minDate?: string;
    maxDate?: string;
    itemType?: string;
    wasTranscoded?: boolean;
    playedToCompletion?: boolean;
    hasError?: boolean;
    skip?: number;
    limit?: number;
    sortBy?: string[];
    sortOrder?: string[];
}

export interface PlaybackReportDto {
    Id: number;
    UserId: string;
    Username?: string;
    ItemId: string;
    ItemName: string;
    ItemType: string;
    SeriesName?: string;
    SeasonNumber?: number;
    EpisodeNumber?: number;
    Artist?: string;
    Album?: string;
    DeviceId: string;
    DeviceName: string;
    ClientName: string;
    PlaySessionId: string;
    SessionId: string;
    StartTimeUtc: string;
    EndTimeUtc?: string;
    DurationSeconds?: number;
    StartPositionTicks?: number;
    EndPositionTicks?: number;
    ItemRuntimeTicks?: number;
    CompletionPercentage?: number;
    PlayedToCompletion: boolean;
    WasTranscoded: boolean;
    VideoCodec?: string;
    AudioCodec?: string;
    Container?: string;
    Bitrate?: number;
    Width?: number;
    Height?: number;
    Protocol?: string;
    PlayMethod?: string;
    RemoteEndPoint?: string;
    IsLocal: boolean;
    LibraryId?: string;
    LibraryName?: string;
    ErrorMessage?: string;
    DateCreated: string;
    LogSeverity: number;
}

export interface QueryResult<T> {
    StartIndex?: number;
    TotalRecordCount?: number;
    Items?: T[];
}

export interface PlaybackReportStats {
    TotalPlays: number;
    UniqueUsers: number;
    UniqueItems: number;
    TotalDurationSeconds: number;
    AverageDurationSeconds: number;
    AverageCompletionPercentage: number;
    TranscodedPlays: number;
    DirectPlayPlays: number;
    DirectStreamPlays: number;
    ErrorPlays: number;
    PlaysByItemType: Record<string, number>;
    PlaysByDevice: Record<string, number>;
    PlaysByPlayMethod: Record<string, number>;
    PlaysByDate: Record<string, number>;
    TopUsers: Array<{
        UserId: string;
        Username: string;
        PlayCount: number;
        TotalDurationSeconds: number;
        AverageCompletionPercentage: number;
    }>;
    TopItems: Array<{
        ItemId: string;
        ItemName: string;
        ItemType: string;
        PlayCount: number;
        TotalDurationSeconds: number;
        AverageCompletionPercentage: number;
        UniqueUsers: number;
    }>;
}

export type PlaybackReportSortBy = 
    | 'DateCreated' 
    | 'UserId' 
    | 'ItemId' 
    | 'DurationSeconds' 
    | 'CompletionPercentage' 
    | 'Bitrate';

const fetchPlaybackReports = async (
    api: Api,
    requestParams?: PlaybackReportQuery,
    options?: AxiosRequestConfig
) => {
    const response = await fetch(api.basePath + '/PlaybackReports/Entries?' + new URLSearchParams(requestParams as any), {
        signal: options?.signal as AbortSignal | undefined,
        headers: {
            Authorization: 'MediaBrowser Token="' + api.accessToken + '"'
        }
    });

    if (!response.ok) {
        throw new Error('HTTP ' + response.status);
    }

    return response.json() as Promise<QueryResult<PlaybackReportDto>>;
};

export const usePlaybackReports = (
    requestParams: PlaybackReportQuery
) => {
    const { api } = useApi();
    return useQuery({
        queryKey: ['PlaybackReports', api?.basePath, requestParams],
        queryFn: ({ signal }) =>
            fetchPlaybackReports(api!, requestParams, { signal }),
        enabled: !!api,
        refetchOnMount: false
    });
};

const fetchPlaybackReportStats = async (
    api: Api,
    requestParams?: PlaybackReportQuery,
    options?: AxiosRequestConfig
) => {
    const response = await fetch(api.basePath + '/PlaybackReports/Stats?' + new URLSearchParams(requestParams as any), {
        signal: options?.signal as AbortSignal | undefined,
        headers: {
            Authorization: 'MediaBrowser Token="' + api.accessToken + '"'
        }
    });

    if (!response.ok) {
        throw new Error('HTTP ' + response.status);
    }

    return response.json() as Promise<PlaybackReportStats>;
};

export const usePlaybackReportStats = (
    requestParams: PlaybackReportQuery
) => {
    const { api } = useApi();
    return useQuery({
        queryKey: ['PlaybackReportStats', api?.basePath, requestParams],
        queryFn: ({ signal }) =>
            fetchPlaybackReportStats(api!, requestParams, { signal }),
        enabled: !!api,
        refetchOnMount: false
    });
};

export const downloadPlaybackReportsCsv = async (
    api: Api,
    requestParams?: PlaybackReportQuery
) => {
    const response = await fetch(api.basePath + '/PlaybackReports/Export?' + new URLSearchParams(requestParams as any), {
        headers: {
            Authorization: 'MediaBrowser Token="' + api.accessToken + '"'
        }
    });

    if (!response.ok) {
        throw new Error('HTTP ' + response.status);
    }

    const blob = await response.blob();
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'playback-reports.csv';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};