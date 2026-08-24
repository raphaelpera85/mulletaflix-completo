import React, { FC, ReactNode } from 'react';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';

interface EmptyStateProps {
    /** Short title line, e.g. "No groups available". */
    title?: string;
    /** Optional longer description below the title. */
    description?: string;
    /** Optional action (button/link) rendered below the description. */
    action?: ReactNode;
    /** Optional icon or custom illustration. */
    icon?: ReactNode;
    className?: string;
}

/**
 * Standardized empty-state block used across dashboard pages so that
 * "nothing to show" moments look and read consistently.
 */
export const EmptyState: FC<EmptyStateProps> = ({
    title,
    description,
    action,
    icon,
    className
}) => (
    <Box
        className={className}
        sx={{
            py: 6,
            px: 2,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            justifyContent: 'center',
            textAlign: 'center',
            gap: 1
        }}
    >
        {icon}
        {title && (
            <Typography variant='h6' color='text.primary'>
                {title}
            </Typography>
        )}
        {description && (
            <Typography variant='body2' color='text.secondary' sx={{ maxWidth: 420 }}>
                {description}
            </Typography>
        )}
        {action && (
            <Box sx={{ mt: 1 }}>
                {action}
            </Box>
        )}
    </Box>
);
