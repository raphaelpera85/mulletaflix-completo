import React from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';

import { BarChart, LineChart } from './PlaybackCharts';

describe('PlaybackCharts', () => {
    describe('BarChart', () => {
        it('renders a bar per entry with mock media type / device distribution data', () => {
            const markup = renderToStaticMarkup(
                <BarChart data={{ Movie: 42, Episode: 17, Audio: 3 }} />
            );

            expect(markup).toContain('<svg');
            expect(markup).toContain('role="img"');
            // One <rect> bar per data entry
            expect(markup.match(/<rect/g)).toHaveLength(3);
            expect(markup).toContain('Movie');
            expect(markup).toContain('Episode');
            expect(markup).toContain('Audio');
        });

        it('renders nothing for empty data instead of throwing', () => {
            expect(() => renderToStaticMarkup(<BarChart data={{}} />)).not.toThrow();
            expect(renderToStaticMarkup(<BarChart data={{}} />)).toBe('');
        });
    });

    describe('LineChart', () => {
        it('renders a time-series polyline for play volume over time', () => {
            const markup = renderToStaticMarkup(
                <LineChart data={{
                    '2026-10-01': 5,
                    '2026-10-02': 12,
                    '2026-10-03': 8
                }}
                />
            );

            expect(markup).toContain('<svg');
            expect(markup).toContain('<polyline');
            // One point (circle) per date in the series
            expect(markup.match(/<circle/g)).toHaveLength(3);
        });

        it('sorts entries chronologically regardless of input order', () => {
            const markup = renderToStaticMarkup(
                <LineChart data={{
                    '2026-10-03': 8,
                    '2026-10-01': 5,
                    '2026-10-02': 12
                }}
                />
            );

            const pointsMatch = /<polyline points="([^"]+)"/.exec(markup);
            expect(pointsMatch).not.toBeNull();
            const points = pointsMatch?.[1].split(' ') ?? [];
            expect(points).toHaveLength(3);
            // First point (2026-10-01, value 5) should sit lower on the chart
            // (larger y) than the last point (2026-10-02, value 12).
            const firstY = Number(points[0].split(',')[1]);
            const lastY = Number(points[2].split(',')[1]);
            expect(firstY).toBeGreaterThan(lastY);
        });

        it('renders nothing for empty data instead of throwing', () => {
            expect(() => renderToStaticMarkup(<LineChart data={{}} />)).not.toThrow();
            expect(renderToStaticMarkup(<LineChart data={{}} />)).toBe('');
        });
    });
});
