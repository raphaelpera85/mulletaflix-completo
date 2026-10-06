import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import React, { type FC } from 'react';

import globalize from 'lib/globalize';

interface OfflineStateProps {
    onRetry?: () => void;
    isDegraded?: boolean;
    message?: string;
}

/**
 * Standardized offline/degraded state used across pages when network is unavailable.
 * - isDegraded: shows lighter warning (not full offline)
 * - message: optional custom message override
 * - onRetry: optional callback for retry button
 */
const OfflineState: FC<OfflineStateProps> = ({
    onRetry,
    isDegraded = false,
    message
}) => {
    const severity = isDegraded ? 'warning' : 'error';
    const defaultMsg = isDegraded
        ? globalize.translate('OfflineModeWarning') || 'Some features may be unavailable. Check your connection.'
        : globalize.translate('OfflineModeError') || 'You are currently offline.';

    return (
        <Alert
            severity={severity}
            action={onRetry ? (
                <Button
                    color='inherit'
                    size='small'
                    onClick={onRetry}
                >
                    {globalize.translate('Retry')}
                </Button>
            ) : undefined}
        >
            {message ?? defaultMsg}
        </Alert>
    );
};

export default OfflineState;
