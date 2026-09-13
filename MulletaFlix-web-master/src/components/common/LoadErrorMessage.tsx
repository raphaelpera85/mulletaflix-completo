import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import React, { type FC } from 'react';

import globalize from 'lib/globalize';

interface LoadErrorMessageProps {
    onRetry?: () => void;
}

const LoadErrorMessage: FC<LoadErrorMessageProps> = ({ onRetry }) => (
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
        {globalize.translate('ErrorDefault')}
    </Alert>
);

export default LoadErrorMessage;
