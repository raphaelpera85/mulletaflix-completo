import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import React, { type FC } from 'react';

import globalize from 'lib/globalize';

interface LoadErrorMessageProps {
    onRetry?: () => void;
    message?: string;
}

const LoadErrorMessage: FC<LoadErrorMessageProps> = ({ onRetry, message }) => (
    <Alert
        severity='error'
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
        {message ?? globalize.translate('ErrorDefault')}
    </Alert>
);

export default LoadErrorMessage;
