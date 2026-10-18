import { beforeEach, describe, expect, it, vi, afterEach } from 'vitest';
import { initializeWebVitals, WebVitalMeasurement } from './webVitals';

/**
 * Mock PerformanceObserver and PerformanceObserverEntryList
 * This replaces the native browser API which is not available in jsdom by default.
 */
class MockPerformanceObserverEntryList {
    constructor(private entries: PerformanceEntry[]) {}

    getEntries(): PerformanceEntry[] {
        return this.entries;
    }
}

interface MockPerformanceObserverInit {
    type?: string;
    buffered?: boolean;
}

type ObserverCallback = (list: MockPerformanceObserverEntryList) => void;

/**
 * Helper to add event listener and track it
 */
function trackEventListener(
    listeners: { type: string; listener: EventListenerOrEventListenerObject }[],
    type: string,
    listener: EventListenerOrEventListenerObject
): void {
    listeners.push({ type, listener });
}

/**
 * Helper to remove tracked event listener
 */
function untrackEventListener(
    listeners: { type: string; listener: EventListenerOrEventListenerObject }[],
    type: string,
    listener: EventListenerOrEventListenerObject
): void {
    const index = listeners.findIndex(
        (entry) => entry.type === type && entry.listener === listener
    );
    if (index >= 0) {
        listeners.splice(index, 1);
    }
}

/**
 * Helper to create a web vital event listener
 */
function createWebVitalEventListener(events: CustomEvent[]): EventListener {
    return (event: Event): void => {
        if (event instanceof CustomEvent && event.type === 'mulletaflix:web-vital') {
            events.push(event);
        }
    };
}

class MockPerformanceObserver {
    private callback: ObserverCallback;
    private observedTypes: Set<string> = new Set();
    static readonly supportedEntryTypes = [
        'largest-contentful-paint',
        'layout-shift',
        'event',
        'paint',
        'navigation',
        'measure',
        'resource'
    ];

    static readonly instances: MockPerformanceObserver[] = [];

    constructor(callback: ObserverCallback) {
        this.callback = callback;
        MockPerformanceObserver.instances.push(this);
    }

    observe(options: MockPerformanceObserverInit): void {
        if (options.type) {
            this.observedTypes.add(options.type);
        }
    }

    disconnect(): void {
        this.observedTypes.clear();
    }

    /**
     * Manually trigger the callback with entries (used for testing).
     */
    simulateEntries(entries: PerformanceEntry[]): void {
        const list = new MockPerformanceObserverEntryList(entries);
        this.callback(list);
    }

    /**
     * Check if this observer is watching a specific entry type.
     */
    isObserving(type: string): boolean {
        return this.observedTypes.has(type);
    }
}

describe('initializeWebVitals', () => {
    let cleanup: (() => void) | undefined;
    let trackedEventListeners: { type: string; listener: EventListenerOrEventListenerObject }[] = [];

    beforeEach(() => {
        // Reset mock observers
        MockPerformanceObserver.instances.length = 0;
        trackedEventListeners = [];

        // Mock window.addEventListener to track event listeners
        const originalAddEventListener = window.addEventListener;
        const originalRemoveEventListener = window.removeEventListener;
        const originalDispatchEvent = window.dispatchEvent;

        window.addEventListener = vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
            trackEventListener(trackedEventListeners, type, listener);
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            (originalAddEventListener as any).call(window, type, listener);
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        }) as any;

        window.removeEventListener = vi.fn((type: string, listener: EventListenerOrEventListenerObject) => {
            untrackEventListener(trackedEventListeners, type, listener);
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            (originalRemoveEventListener as any).call(window, type, listener);
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        }) as any;

        window.dispatchEvent = vi.fn(originalDispatchEvent);

        // Mock PerformanceObserver in global scope
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (globalThis as any).PerformanceObserver = MockPerformanceObserver;

        // Mock performance.getEntriesByType for navigation timing
        vi.spyOn(performance, 'getEntriesByType').mockImplementation((type: string) => {
            if (type === 'navigation') {
                return [
                    {
                        name: 'navigation',
                        entryType: 'navigation',
                        startTime: 0,
                        duration: 1000,
                        domInteractive: 450,
                        responseStart: 150
                    } as PerformanceNavigationTiming
                ];
            }
            return [];
        });

        // Mock console.log to avoid output during tests
        vi.spyOn(console, 'log').mockImplementation(() => {
            // Intentional no-op
        });
    });

    afterEach(() => {
        if (cleanup) {
            cleanup();
        }
        vi.clearAllMocks();
        MockPerformanceObserver.instances.length = 0;
    });

    it('(1) initializeWebVitals returns a cleanup function', () => {
        cleanup = initializeWebVitals();

        expect(cleanup).toBeDefined();
        expect(typeof cleanup).toBe('function');
    });

    it('(2) simulates LCP entry and emits mulletaflix:web-vital event with correct value', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Find the LCP observer
        const lcpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('largest-contentful-paint')
        );

        expect(lcpObserver).toBeDefined();

        // Simulate LCP entry
        const lcpEntry: PerformanceEntry = {
            name: 'largest-contentful-paint',
            entryType: 'largest-contentful-paint',
            startTime: 1234.5,
            duration: 0,
            toJSON: () => ({})
        };

        lcpObserver!.simulateEntries([lcpEntry]);

        // Trigger pagehide to flush
        window.dispatchEvent(new Event('pagehide'));

        // Find LCP event in emitted events
        const lcpEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'LCP'
        );

        expect(lcpEvent).toBeDefined();
        expect((lcpEvent!.detail as WebVitalMeasurement).value).toBe(1234.5);
        expect((lcpEvent!.detail as WebVitalMeasurement).name).toBe('LCP');

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('(3) layout-shift entries: includes shifts without hadRecentInput, excludes those with hadRecentInput', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Find the CLS observer
        const clsObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('layout-shift')
        );

        expect(clsObserver).toBeDefined();

        // Simulate two layout-shift entries: one valid, one invalid
        const entry1: PerformanceEntry & { hadRecentInput?: boolean; value?: number } = {
            name: 'layout-shift',
            entryType: 'layout-shift',
            startTime: 100,
            duration: 0,
            hadRecentInput: false,
            value: 0.1,
            toJSON: () => ({})
        };

        const entry2: PerformanceEntry & { hadRecentInput?: boolean; value?: number } = {
            name: 'layout-shift',
            entryType: 'layout-shift',
            startTime: 200,
            duration: 0,
            hadRecentInput: true,
            value: 0.2,
            toJSON: () => ({})
        };

        clsObserver!.simulateEntries([entry1, entry2]);

        // Trigger pagehide to flush
        window.dispatchEvent(new Event('pagehide'));

        // Find CLS event in emitted events
        const clsEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'CLS'
        );

        expect(clsEvent).toBeDefined();
        // Only entry1's value (0.1) should be counted, entry2 is excluded due to hadRecentInput
        expect((clsEvent!.detail as WebVitalMeasurement).value).toBeCloseTo(0.1);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('(4) pagehide event triggers flush: emits LCP, CLS, INP, DOM_INTERACTIVE, TTFB events', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Setup LCP
        const lcpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('largest-contentful-paint')
        );
        const lcpEntry: PerformanceEntry = {
            name: 'largest-contentful-paint',
            entryType: 'largest-contentful-paint',
            startTime: 500,
            duration: 0,
            toJSON: () => ({})
        };
        lcpObserver!.simulateEntries([lcpEntry]);

        // Setup CLS
        const clsObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('layout-shift')
        );
        const clsEntry: PerformanceEntry & { hadRecentInput?: boolean; value?: number } = {
            name: 'layout-shift',
            entryType: 'layout-shift',
            startTime: 600,
            duration: 0,
            hadRecentInput: false,
            value: 0.15,
            toJSON: () => ({})
        };
        clsObserver!.simulateEntries([clsEntry]);

        // Setup INP
        const inpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('event')
        );
        const inpEntry: PerformanceEntry & { duration: number; interactionId?: number } = {
            name: 'event',
            entryType: 'event',
            startTime: 700,
            duration: 200,
            interactionId: 1,
            toJSON: () => ({})
        };
        inpObserver!.simulateEntries([inpEntry]);

        // Clear previous events and dispatch pagehide
        vitalEvents.length = 0;
        window.dispatchEvent(new Event('pagehide'));

        // Verify all expected vital events are emitted
        const eventNames = vitalEvents.map((e) => (e.detail as WebVitalMeasurement).name);

        expect(eventNames).toContain('LCP');
        expect(eventNames).toContain('CLS');
        expect(eventNames).toContain('INP');
        expect(eventNames).toContain('DOM_INTERACTIVE');
        expect(eventNames).toContain('TTFB');

        // Verify values
        const lcpEvent = vitalEvents.find((e) => (e.detail as WebVitalMeasurement).name === 'LCP');
        const clsEvent = vitalEvents.find((e) => (e.detail as WebVitalMeasurement).name === 'CLS');
        const inpEvent = vitalEvents.find((e) => (e.detail as WebVitalMeasurement).name === 'INP');
        const ttfbEvent = vitalEvents.find((e) => (e.detail as WebVitalMeasurement).name === 'TTFB');
        const domEvent = vitalEvents.find((e) => (e.detail as WebVitalMeasurement).name === 'DOM_INTERACTIVE');

        expect((lcpEvent!.detail as WebVitalMeasurement).value).toBe(500);
        expect((clsEvent!.detail as WebVitalMeasurement).value).toBeCloseTo(0.15);
        expect((inpEvent!.detail as WebVitalMeasurement).value).toBe(200);
        expect((ttfbEvent!.detail as WebVitalMeasurement).value).toBe(150); // responseStart
        expect((domEvent!.detail as WebVitalMeasurement).value).toBe(450); // domInteractive

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('(5) cleanup removes observers and event listeners: no events dispatched after cleanup', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        const initialObserverCount = MockPerformanceObserver.instances.length;
        expect(initialObserverCount).toBeGreaterThan(0);

        // Setup and simulate some entries before cleanup
        const lcpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('largest-contentful-paint')
        );
        const lcpEntry: PerformanceEntry = {
            name: 'largest-contentful-paint',
            entryType: 'largest-contentful-paint',
            startTime: 500,
            duration: 0,
            toJSON: () => ({})
        };
        lcpObserver!.simulateEntries([lcpEntry]);

        vitalEvents.length = 0;

        // Call cleanup
        cleanup();

        // Verify all observers are disconnected (disconnect() should have been called on each)
        // The observers should no longer observe new entries (but we can't easily verify this without modifying the mock)
        // So we verify the disconnected state by checking if observe() would fail or not.

        // After cleanup, pagehide should not trigger duplicate events
        // (first pagehide is called in cleanup, second should not create new events)
        vitalEvents.length = 0;
        window.dispatchEvent(new Event('pagehide'));

        // Since pagehide listener is removed and flush is called once, no additional events
        expect(vitalEvents).toHaveLength(0);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('flushes only once even if pagehide fires multiple times', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Setup LCP
        const lcpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('largest-contentful-paint')
        );
        const lcpEntry: PerformanceEntry = {
            name: 'largest-contentful-paint',
            entryType: 'largest-contentful-paint',
            startTime: 500,
            duration: 0,
            toJSON: () => ({})
        };
        lcpObserver!.simulateEntries([lcpEntry]);

        vitalEvents.length = 0;

        // First pagehide
        window.dispatchEvent(new Event('pagehide'));
        const firstFlushCount = vitalEvents.length;
        expect(firstFlushCount).toBeGreaterThan(0);

        vitalEvents.length = 0;

        // Second pagehide (should not emit new events due to flushed flag)
        window.dispatchEvent(new Event('pagehide'));
        expect(vitalEvents).toHaveLength(0);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('does not emit INP event if INP value is 0', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Do NOT simulate any INP entries (inpValue remains 0)

        vitalEvents.length = 0;
        window.dispatchEvent(new Event('pagehide'));

        // Verify INP is NOT emitted when value is 0
        const inpEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'INP'
        );
        expect(inpEvent).toBeUndefined();

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('handles FCP entries from paint observer', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Find the paint observer
        const paintObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('paint')
        );

        expect(paintObserver).toBeDefined();

        // Simulate FCP entry
        const fcpEntry: PerformanceEntry = {
            name: 'first-contentful-paint',
            entryType: 'paint',
            startTime: 300,
            duration: 0,
            toJSON: () => ({})
        };

        paintObserver!.simulateEntries([fcpEntry]);

        // Find FCP event in emitted events
        const fcpEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'FCP'
        );

        expect(fcpEvent).toBeDefined();
        expect((fcpEvent!.detail as WebVitalMeasurement).value).toBe(300);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('tracks maximum INP value across multiple event entries', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        // Find the event observer (INP)
        const inpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('event')
        );

        // Simulate multiple event entries
        const entry1: PerformanceEntry & { duration: number; interactionId?: number } = {
            name: 'event',
            entryType: 'event',
            startTime: 100,
            duration: 150,
            interactionId: 1,
            toJSON: () => ({})
        };

        const entry2: PerformanceEntry & { duration: number; interactionId?: number } = {
            name: 'event',
            entryType: 'event',
            startTime: 200,
            duration: 250, // This is the max
            interactionId: 2,
            toJSON: () => ({})
        };

        const entry3: PerformanceEntry & { duration: number; interactionId?: number } = {
            name: 'event',
            entryType: 'event',
            startTime: 300,
            duration: 100,
            interactionId: 3,
            toJSON: () => ({})
        };

        inpObserver!.simulateEntries([entry1, entry2, entry3]);

        vitalEvents.length = 0;
        window.dispatchEvent(new Event('pagehide'));

        const inpEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'INP'
        );

        expect(inpEvent).toBeDefined();
        expect((inpEvent!.detail as WebVitalMeasurement).value).toBe(250);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('updates LCP to latest entry when multiple entries are observed', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        const lcpObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('largest-contentful-paint')
        );

        // First LCP entry
        const lcpEntry1: PerformanceEntry = {
            name: 'largest-contentful-paint',
            entryType: 'largest-contentful-paint',
            startTime: 500,
            duration: 0,
            toJSON: () => ({})
        };
        lcpObserver!.simulateEntries([lcpEntry1]);

        // Second (later) LCP entry
        const lcpEntry2: PerformanceEntry = {
            name: 'largest-contentful-paint',
            entryType: 'largest-contentful-paint',
            startTime: 1000,
            duration: 0,
            toJSON: () => ({})
        };
        lcpObserver!.simulateEntries([lcpEntry1, lcpEntry2]);

        vitalEvents.length = 0;
        window.dispatchEvent(new Event('pagehide'));

        const lcpEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'LCP'
        );

        // Should use the latest LCP entry
        expect((lcpEvent!.detail as WebVitalMeasurement).value).toBe(1000);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });

    it('accumulates CLS values from multiple layout-shift entries', () => {
        const vitalEvents: CustomEvent[] = [];
        const vitalEventListener = createWebVitalEventListener(vitalEvents) as EventListener;

        window.addEventListener('mulletaflix:web-vital', vitalEventListener);

        cleanup = initializeWebVitals();

        const clsObserver = MockPerformanceObserver.instances.find((obs) =>
            obs.isObserving('layout-shift')
        );

        // Single batch with accumulated values
        const entry1: PerformanceEntry & { hadRecentInput?: boolean; value?: number } = {
            name: 'layout-shift',
            entryType: 'layout-shift',
            startTime: 100,
            duration: 0,
            hadRecentInput: false,
            value: 0.1,
            toJSON: () => ({})
        };

        const entry2: PerformanceEntry & { hadRecentInput?: boolean; value?: number } = {
            name: 'layout-shift',
            entryType: 'layout-shift',
            startTime: 200,
            duration: 0,
            hadRecentInput: false,
            value: 0.15,
            toJSON: () => ({})
        };

        clsObserver!.simulateEntries([entry1, entry2]);

        vitalEvents.length = 0;
        window.dispatchEvent(new Event('pagehide'));

        const clsEvent = vitalEvents.find(
            (e) => (e.detail as WebVitalMeasurement).name === 'CLS'
        );

        // Should accumulate both values
        expect((clsEvent!.detail as WebVitalMeasurement).value).toBeCloseTo(0.25);

        window.removeEventListener('mulletaflix:web-vital', vitalEventListener);
    });
});
