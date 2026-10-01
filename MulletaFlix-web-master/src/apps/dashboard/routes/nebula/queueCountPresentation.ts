type UploadQueueSummaryCounts = {
    isAvailable?: boolean;
    pendingCount?: number;
    retryCount?: number;
};

export type UploadQueueCountPresentation = {
    totalPendingCount: number | null;
    loadedCount: number;
    retryCount: number | null;
    totalDescription: string;
    loadedDescription: string;
};

const isNonNegativeInteger = (value: unknown): value is number =>
    typeof value === 'number' && Number.isSafeInteger(value) && value >= 0;

/** Keeps the persisted total distinct from the bounded in-memory worker snapshot. */
export function getUploadQueueCountPresentation(
    loadedCount: number | undefined,
    summary: UploadQueueSummaryCounts | undefined
): UploadQueueCountPresentation {
    const hasSummary = summary?.isAvailable === true && isNonNegativeInteger(summary.pendingCount);
    const totalPendingCount = hasSummary ? summary.pendingCount! : null;
    const normalizedLoadedCount = isNonNegativeInteger(loadedCount) ? loadedCount : 0;
    const retryCount = hasSummary && isNonNegativeInteger(summary.retryCount) ? summary.retryCount : null;
    let totalDescription = 'Total pendente indisponível';
    if (totalPendingCount !== null) {
        totalDescription = totalPendingCount + ' item(ns) pendente(s) no MongoDB';
    }
    let loadedDescription = normalizedLoadedCount + ' carregado(s) nos workers';
    if (retryCount !== null && retryCount > 0) {
        loadedDescription += ' · ' + retryCount + ' aguardando retry';
    }

    return {
        totalPendingCount,
        loadedCount: normalizedLoadedCount,
        retryCount,
        totalDescription,
        loadedDescription
    };
}
