import React, { FC, useCallback } from 'react';
import IconButton from '@mui/material/IconButton';
import StopIcon from '@mui/icons-material/Stop';
import { useQueryClient } from '@tanstack/react-query';

import { useCancelTimer } from 'hooks/api/liveTvHooks';
import globalize from 'lib/globalize';
import loading from 'components/loading/loading';
import toast from 'components/toast/toast';

interface CancelTimerButtonProps {
    timerId: string;
    queryKey?: string[];
}

const CancelTimerButton: FC<CancelTimerButtonProps> = ({
    timerId,
    queryKey
}) => {
    const queryClient = useQueryClient();
    const cancelTimer = useCancelTimer();

    const onCancelTimerClick = useCallback(() => {
        void loading.withLoading(async () => {
            try {
                await cancelTimer.mutateAsync({ timerId });
                toast(globalize.translate('RecordingCancelled'));
                await queryClient.invalidateQueries({ queryKey });
            } catch (err) {
                toast(globalize.translate('MessageCancelTimerError'));
                console.error('[cancelTimer] failed to cancel timer', err);
            }
        });
    }, [cancelTimer, queryClient, queryKey, timerId]);

    return (
        <IconButton
            className='button-flat btnCancelTimer'
            title={globalize.translate('StopRecording')}
            onClick={onCancelTimerClick}
        >
            <StopIcon />
        </IconButton>
    );
};

export default CancelTimerButton;
