import Box from '@mui/material/Box';
import Skeleton from '@mui/material/Skeleton';
import Stack from '@mui/material/Stack';
import React, { type FC } from 'react';

interface DetailPageSkeletonProps {
    /**
     * Number of skeleton sections to show (default: 3)
     */
    sections?: number;
    /**
     * Height of each section in pixels (default: 200)
     */
    sectionHeight?: number;
}

/**
 * Skeleton loader for detail pages.
 * Shows multiple skeleton sections with consistent spacing.
 * Used while loading detail page content.
 */
const DetailPageSkeleton: FC<DetailPageSkeletonProps> = ({
    sections = 3,
    sectionHeight = 200
}) => (
    <Stack spacing={3} sx={{ py: 2, px: 2 }}>
        {/* Header skeleton */}
        <Box>
            <Skeleton variant='text' width='60%' height={40} />
            <Skeleton variant='text' width='40%' height={24} sx={{ mt: 1 }} />
        </Box>

        {/* Content sections */}
        {Array.from({ length: sections }).map((_, index) => (
            <Box key={index}>
                <Skeleton variant='text' width='30%' height={24} sx={{ mb: 1 }} />
                <Skeleton variant='rectangular' height={sectionHeight} sx={{ borderRadius: 1 }} />
            </Box>
        ))}
    </Stack>
);

export default DetailPageSkeleton;
