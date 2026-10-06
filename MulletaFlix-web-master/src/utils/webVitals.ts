type VitalName = 'LCP' | 'CLS' | 'INP' | 'FCP' | 'TTFB' | 'DOM_INTERACTIVE';

export interface WebVitalMeasurement {
    name: VitalName;
    value: number;
    navigationType?: string;
    rating?: string;
}

const emitMeasurement = (measurement: WebVitalMeasurement): void => {
    window.dispatchEvent(new CustomEvent('mulletaflix:web-vital', {
        detail: measurement
    }));

    // Keep local diagnostics available without creating a network dependency.
    if (import.meta.env?.DEV) {
        const ratingLabel = measurement.rating ? ` [${measurement.rating.toUpperCase()}]` : '';
        const color = getRatingConsoleColor(measurement.rating);
        console.log(
            `%c[Web Vitals] ${measurement.name}: ${measurement.value.toFixed(2)}ms${ratingLabel}`,
            `color: ${color}; font-weight: bold;`
        );
    }
};

/**
 * Get console color for rating display.
 */
function getRatingConsoleColor(rating?: string): string {
    switch (rating) {
        case 'good':
            return '#10b981';
        case 'needs-improvement':
            return '#f59e0b';
        case 'poor':
            return '#ef4444';
        default:
            return '#6b7280';
    }
}

const observe = <T extends PerformanceEntry>(
    type: string,
    callback: (entries: T[]) => void
): PerformanceObserver | undefined => {
    if (typeof PerformanceObserver === 'undefined'
        || !PerformanceObserver.supportedEntryTypes?.includes(type)) {
        return undefined;
    }

    const observer = new PerformanceObserver(list => callback(list.getEntries() as T[]));
    observer.observe({ type, buffered: true } as PerformanceObserverInit);
    return observer;
};

/**
 * Starts local, privacy-preserving performance measurements for the current
 * page. Consumers can listen to `mulletaflix:web-vital` to forward metrics to
 * an explicitly configured telemetry backend.
 *
 * This implementation combines:
 * 1. Native PerformanceObserver API for basic vitals (LCP, CLS, INP, FCP)
 * 2. Optional web-vitals library for improved accuracy and TTFB measurement
 */
export const initializeWebVitals = (): (() => void) => {
    const observers: PerformanceObserver[] = [];
    let latestLcp: PerformanceEntry | undefined;
    let clsValue = 0;
    let inpValue = 0;
    let flushed = false;

    const lcpObserver = observe<PerformanceEntry>('largest-contentful-paint', entries => {
        latestLcp = entries[entries.length - 1];
    });
    if (lcpObserver) observers.push(lcpObserver);

    const clsObserver = observe<PerformanceEntry & { value?: number; hadRecentInput?: boolean }>(
        'layout-shift',
        entries => {
            entries.forEach(entry => {
                if (!entry.hadRecentInput) clsValue += entry.value || 0;
            });
        }
    );
    if (clsObserver) observers.push(clsObserver);

    const inpObserver = observe<PerformanceEntry & { duration: number; interactionId?: number }>(
        'event',
        entries => {
            entries.forEach(entry => {
                if (entry.interactionId) inpValue = Math.max(inpValue, entry.duration);
            });
        }
    );
    if (inpObserver) observers.push(inpObserver);

    const paintObserver = observe<PerformanceEntry>('paint', entries => {
        const fcp = entries.find(entry => entry.name === 'first-contentful-paint');
        if (fcp) emitMeasurement({ name: 'FCP', value: fcp.startTime });
    });
    if (paintObserver) observers.push(paintObserver);

    // Try to supplement with web-vitals library if available (provides TTFB and better rating)
    if (import.meta.env?.DEV) {
        Promise.resolve().then(() => {
            import('web-vitals')
                .then(({ onCLS, onINP, onLCP, onTTFB }) => {
                    onCLS((metric: any) => {
                        emitMeasurement({
                            name: 'CLS',
                            value: metric.value,
                            rating: metric.rating
                        });
                    });
                    onINP((metric: any) => {
                        emitMeasurement({
                            name: 'INP',
                            value: metric.value,
                            rating: metric.rating
                        });
                    });
                    onLCP((metric: any) => {
                        emitMeasurement({
                            name: 'LCP',
                            value: metric.value,
                            rating: metric.rating
                        });
                    });
                    onTTFB((metric: any) => {
                        emitMeasurement({
                            name: 'TTFB',
                            value: metric.value,
                            rating: metric.rating
                        });
                    });
                })
                .catch(() => {
                    // web-vitals not available; fallback to basic PerformanceObserver
                });
        });
    }

    const flush = (): void => {
        if (flushed) return;
        flushed = true;

        if (latestLcp) emitMeasurement({ name: 'LCP', value: latestLcp.startTime });
        emitMeasurement({ name: 'CLS', value: clsValue });
        if (inpValue > 0) emitMeasurement({ name: 'INP', value: inpValue });

        const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
        if (navigation) {
            emitMeasurement({ name: 'DOM_INTERACTIVE', value: navigation.domInteractive });
            emitMeasurement({ name: 'TTFB', value: navigation.responseStart });
        }
    };

    window.addEventListener('pagehide', flush, { once: true });

    return () => {
        observers.forEach(observer => {
            observer.disconnect();
        });
        window.removeEventListener('pagehide', flush);
        flush();
    };
};
