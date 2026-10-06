import SystemUpdateAltIcon from '@mui/icons-material/SystemUpdateAlt';
import Badge from '@mui/material/Badge';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import React from 'react';
import { Link } from 'react-router-dom';

export interface UpdateAvailableIndicatorProps {
    /** Whether the backend reports a pending/available update. */
    updateAvailable: boolean;
    /** Version string to show in the tooltip, when known. */
    availableVersion?: string;
}

/**
 * Discreet header indicator for the "Centro de Atualizações" dashboard route.
 * Renders nothing when there is no pending update, keeping the toolbar clean,
 * and shows a badge dot over an icon button linking to the update center
 * when the server reports UpdateAvailable from /System/Update/Status.
 */
const UpdateAvailableIndicator = ({ updateAvailable, availableVersion }: UpdateAvailableIndicatorProps) => {
    if (!updateAvailable) {
        return null;
    }

    const tooltipTitle = availableVersion ?
        `Atualização disponível: ${availableVersion}` :
        'Atualização disponível';

    return (
        <Tooltip title={tooltipTitle}>
            <IconButton
                component={Link}
                to='/dashboard/updates'
                size='large'
                color='inherit'
                aria-label={tooltipTitle}
            >
                <Badge color='error' variant='dot' overlap='circular'>
                    <SystemUpdateAltIcon />
                </Badge>
            </IconButton>
        </Tooltip>
    );
};

export default UpdateAvailableIndicator;
