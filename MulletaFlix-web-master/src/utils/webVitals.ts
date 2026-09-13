type VitalName = 'LCP' | 'CLS' | 'INP' | 'FCP' | 'DOM_INTERACTIVE';

export interface WebVitalMeasurement {
    name: VitalName;
    value: number;
    navigationType?: string;
}

const emitMeasurement = (measurement: WebVitalMeasurement): void => {
    window.dispatchEvent(new CustomEvent('mulletaflix:web-vital', {
        detail: measurement
    }));

    // Keep local diagnostics available without creating a network dependency.
    if (import.meta.env?.DEV) {
        console.debug('[web-vitals]', measurement.name, measurement.value);
    }
};

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

    const flush = (): void => {
        if (flushed) return;
        flushed = true;

        if (latestLcp) emitMeasurement({ name: 'LCP', value: latestLcp.startTime });
        emitMeasurement({ name: 'CLS', value: clsValue });
        if (inpValue > 0) emitMeasurement({ name: 'INP', value: inpValue });

        const navigation = performance.getEntriesByType('navigation')[0] as PerformanceNavigationTiming | undefined;
        if (navigation) emitMeasurement({ name: 'DOM_INTERACTIVE', value: navigation.domInteractive });
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
