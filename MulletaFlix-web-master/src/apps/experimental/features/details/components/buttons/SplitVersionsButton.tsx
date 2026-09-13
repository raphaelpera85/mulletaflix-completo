import React, { FC, useCallback } from 'react';
import IconButton from '@mui/material/IconButton';
import CallSplitIcon from '@mui/icons-material/CallSplit';
import { useQueryClient } from '@tanstack/react-query';

import { useDeleteAlternateSources } from 'hooks/api/videosHooks';
import globalize from 'lib/globalize';
import confirm from 'components/confirm/confirm';
import loading from 'components/loading/loading';
import toast from 'components/toast/toast';

interface SplitVersionsButtonProps {
    paramId: string;
    queryKey?: string[];
}

const SplitVersionsButton: FC<SplitVersionsButtonProps> = ({
    paramId,
    queryKey
}) => {
    const queryClient = useQueryClient();
    const deleteAlternateSources = useDeleteAlternateSources();

    const splitVersions = useCallback(() => {
        confirm({
            title: globalize.translate('HeaderSplitMediaApart'),
            text: globalize.translate('MessageConfirmSplitMediaSources')
        })
            .then(async function () {
                await loading.withLoading(async () => {
                    try {
                        await deleteAlternateSources.mutateAsync({ itemId: paramId });
                        await queryClient.invalidateQueries({ queryKey });
                    } catch (err) {
                        toast(globalize.translate('MessageSplitVersionsError'));
                        console.error('[splitVersions] failed to split versions', err);
                    }
                });
            })
            .catch(() => {
                // confirm dialog closed
            });
    }, [deleteAlternateSources, paramId, queryClient, queryKey]);

    return (
        <IconButton
            className='button-flat btnSplitVersions'
            title={globalize.translate('ButtonSplit')}
            onClick={splitVersions}
        >
            <CallSplitIcon />
        </IconButton>
    );
};

export default SplitVersionsButton;
