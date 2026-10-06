import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import Grid from '@mui/material/Grid';
import React, { type FC } from 'react';

interface ListPageSkeletonProps {
    /**
     * Number of skeleton items to show per row (default: 4)
     */
    itemsPerRow?: number;
    /**
     * Number of rows to show (default: 2)
     */
    rows?: number;
    /**
     * Aspect ratio of each item skeleton (default: '3/4' for movie posters)
     */
    itemAspectRatio?: string;
}

/**
 * Skeleton loader for list/grid pages (home, search, library views).
 * Shows multiple skeleton items in a grid with consistent spacing.
 * Used while loading list content.
 */
const ListPageSkeleton: FC<ListPageSkeletonProps> = ({
    itemsPerRow = 4,
    rows = 2,
    itemAspectRatio = '3/4'
}) => {
    const gridSize = Math.floor(12 / itemsPerRow) as any;

    return (
        <Stack spacing={3} sx={{ py: 2, px: 2 }}>
            {/* Title skeleton */}
            <Skeleton variant='text' width='25%' height={28} />

            {/* Grid of item skeletons */}
            <Grid container spacing={2}>
                {Array.from({ length: itemsPerRow * rows }).map((_, index) => (
                    <Grid item xs={12} sm={gridSize} key={index}>
                        <Box sx={{ aspectRatio: itemAspectRatio }}>
                            <Skeleton
                                variant='rectangular'
                                width='100%'
                                height='100%'
                                sx={{ borderRadius: 1 }}
                            />
                        </Box>
                    </Grid>
                ))}
            </Grid>
        </Stack>
    );
};

export default ListPageSkeleton;
