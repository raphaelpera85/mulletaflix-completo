import React, { useMemo } from 'react';
import { useTheme } from '@mui/material/styles';

interface BarChartProps {
    data: Record<string, number>;
    height?: number;
}

interface LineChartProps {
    data: Record<string, number>;
    height?: number;
}

/**
 * Lightweight SVG bar chart (no external chart library).
 */
export const BarChart: React.FC<BarChartProps> = ({ data, height = 160 }) => {
    const theme = useTheme();
    const entries = useMemo(() => Object.entries(data), [ data ]);

    if (entries.length === 0) {
        return null;
    }

    const max = Math.max(...entries.map(([, v]) => v), 1);
    const width = 600;
    const barGap = 8;
    const barWidth = Math.max(4, (width - barGap * (entries.length + 1)) / entries.length);
    const accent = theme.palette.primary.main;

    return (
        <svg
            viewBox={`0 0 ${width} ${height + 24}`}
            width='100%'
            height={height + 24}
            role='img'
            aria-label='Bar chart'
        >
            {entries.map(([label, value], i) => {
                const barHeight = Math.max(2, (value / max) * height);
                const x = barGap + i * (barWidth + barGap);
                const y = height - barHeight;
                return (
                    <g key={label}>
                        <rect
                            x={x}
                            y={y}
                            width={barWidth}
                            height={barHeight}
                            fill={accent}
                            rx={2}
                        />
                        <text
                            x={x + barWidth / 2}
                            y={height + 16}
                            textAnchor='middle'
                            fontSize='9'
                            fill={theme.palette.text.secondary}
                        >
                            {label}
                        </text>
                    </g>
                );
            })}
        </svg>
    );
};

/**
 * Lightweight SVG line chart (no external chart library).
 */
export const LineChart: React.FC<LineChartProps> = ({ data, height = 160 }) => {
    const theme = useTheme();
    const entries = useMemo(() => Object.entries(data).sort(([a], [b]) => a.localeCompare(b)), [ data ]);

    if (entries.length === 0) {
        return null;
    }

    const max = Math.max(...entries.map(([, v]) => v), 1);
    const width = 600;
    const padding = 12;
    const stepX = entries.length > 1 ? (width - padding * 2) / (entries.length - 1) : 0;
    const accent = theme.palette.primary.main;

    const points = entries.map(([, value], i) => {
        const x = padding + i * stepX;
        const y = height - padding - (value / max) * (height - padding * 2);
        return `${x},${y}`;
    });

    return (
        <svg
            viewBox={`0 0 ${width} ${height}`}
            width='100%'
            height={height}
            role='img'
            aria-label='Line chart'
        >
            <polyline
                points={points.join(' ')}
                fill='none'
                stroke={accent}
                strokeWidth={2}
            />
            {entries.map(([label], i) => {
                const [x, y] = points[i].split(',').map(Number);
                return (
                    <g key={label}>
                        <circle cx={x} cy={y} r={3} fill={accent} />
                        {entries.length <= 20 && (
                            <text
                                x={x}
                                y={height - 2}
                                textAnchor='middle'
                                fontSize='8'
                                fill={theme.palette.text.secondary}
                            >
                                {label.slice(5)}
                            </text>
                        )}
                    </g>
                );
            })}
        </svg>
    );
};
