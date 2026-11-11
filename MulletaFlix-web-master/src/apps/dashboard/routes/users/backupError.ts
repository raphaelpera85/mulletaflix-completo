type ErrorPayload = {
    message?: unknown;
    Message?: unknown;
    error?: unknown;
    detail?: unknown;
};

type RequestError = {
    message?: unknown;
    responseJSON?: unknown;
    responseText?: unknown;
    response?: {
        data?: unknown;
        body?: unknown;
    };
};

const extractMessage = (value: unknown): string | undefined => {
    if (typeof value === 'string') {
        const text = value.trim();
        if (!text) return undefined;

        try {
            return extractMessage(JSON.parse(text)) || text;
        } catch {
            return text;
        }
    }

    if (!value || typeof value !== 'object') return undefined;

    const payload = value as ErrorPayload;
    for (const candidate of [ payload.message, payload.Message, payload.error, payload.detail ]) {
        const message = extractMessage(candidate);
        if (message) return message;
    }

    return undefined;
};

export const getBackupErrorMessage = (error: unknown): string => {
    const requestError = (error && typeof error === 'object' ? error : {}) as RequestError;
    return extractMessage(requestError.responseJSON)
        || extractMessage(requestError.response?.data)
        || extractMessage(requestError.response?.body)
        || extractMessage(requestError.responseText)
        || extractMessage(requestError.message)
        || 'erro desconhecido';
};
