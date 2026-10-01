import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.hoisted(() => {
    if (typeof Worker === 'undefined') {
        (globalThis as unknown as { Worker: unknown }).Worker = class {
            postMessage = vi.fn();
            addEventListener = vi.fn();
            removeEventListener = vi.fn();
            terminate = vi.fn();
        };
    }
    if (typeof ResizeObserver === 'undefined') {
        (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
            observe = vi.fn();
            unobserve = vi.fn();
            disconnect = vi.fn();
        };
    }
});

vi.mock('webcomponents.js/webcomponents-lite', () => {
    (document as Document & { registerElement: typeof vi.fn }).registerElement = vi.fn();
    return {};
});

vi.mock('../utils/fetchLocal', () => ({
    default: vi.fn().mockResolvedValue({
        ok: true,
        json: async () => ({})
    })
}));

const mockGetCurrentUser = vi.fn();
const mockLoadSections = vi.fn();
const mockResume = vi.fn();
const mockApiClient = {
    getCurrentUser: mockGetCurrentUser,
    serverId: () => 'test-server-id',
    getCurrentUserId: () => 'test-user-id',
    getItems: vi.fn().mockResolvedValue({ Items: [] }),
    getArtists: vi.fn().mockResolvedValue({ Items: [] }),
    getPeople: vi.fn().mockResolvedValue({ Items: [] }),
    subscribe: vi.fn().mockReturnValue(vi.fn())
};

vi.mock('lib/jellyfin-apiclient', () => ({
    ServerConnections: {
        currentApiClient: () => mockApiClient,
        getApiClients: () => [mockApiClient],
        getApiClient: () => mockApiClient
    }
}));

vi.mock('../components/homesections/homesections', () => ({
    default: {
        loadSections: (...args: unknown[]) => mockLoadSections(...args),
        resume: (...args: unknown[]) => mockResume(...args),
        pause: vi.fn(),
        destroySections: vi.fn()
    }
}));

vi.mock('../components/focusManager', () => ({
    default: {
        autoFocus: vi.fn()
    }
}));

vi.mock('../lib/globalize', () => ({
    default: {
        translate: (key: string) => key,
        getIsElementRTL: () => false,
        getIsRTL: () => false
    }
}));

import HomeTab from './hometab';
import FavoritesTab from './favorites';

describe('HomeTab error and retry states', () => {
    let container: HTMLElement;

    beforeEach(() => {
        document.body.innerHTML = '';
        mockGetCurrentUser.mockReset();
        mockLoadSections.mockReset();
        mockResume.mockReset();

        container = document.createElement('div');
        container.innerHTML = `
            <div id="homeTabLoadError" class="hide padded-left padded-right padded-top padded-bottom-page">
                <p class="homeTabLoadErrorMessage"></p>
                <button is="emby-button" type="button" class="raised button-accent btnHomeTabRetry">
                    <span>Retry</span>
                </button>
            </div>
            <div class="homeTabContent">
                <div class="sections"></div>
            </div>
        `;
        document.body.appendChild(container);
    });

    it('shows error container and hides content when loading fails', async () => {
        mockGetCurrentUser.mockRejectedValue(new Error('Network failure'));

        const tab = new HomeTab(container, {});
        await tab.onResume({});

        const errorDiv = container.querySelector('#homeTabLoadError');
        const contentDiv = container.querySelector('.homeTabContent');
        const message = container.querySelector('.homeTabLoadErrorMessage');

        expect(errorDiv?.classList.contains('hide')).toBe(false);
        expect(contentDiv?.classList.contains('hide')).toBe(true);
        expect(message?.textContent).toBe('ErrorDefault');
    });

    it('retries loading when retry button is clicked', async () => {
        mockGetCurrentUser.mockRejectedValueOnce(new Error('First failure'));
        mockGetCurrentUser.mockResolvedValueOnce({ Id: 'user-1' });
        mockLoadSections.mockResolvedValue(undefined);

        const tab = new HomeTab(container, {});
        await tab.onResume({});

        const errorDiv = container.querySelector('#homeTabLoadError');
        const retryBtn = container.querySelector<HTMLButtonElement>('.btnHomeTabRetry');

        expect(errorDiv?.classList.contains('hide')).toBe(false);

        // Click retry
        retryBtn?.click();

        // Allow microtasks to settle
        await new Promise((resolve) => setTimeout(resolve, 10));

        const contentDiv = container.querySelector('.homeTabContent');
        expect(errorDiv?.classList.contains('hide')).toBe(true);
        expect(contentDiv?.classList.contains('hide')).toBe(false);
        expect(mockLoadSections).toHaveBeenCalled();
    });
});

describe('FavoritesTab error and empty states', () => {
    let container: HTMLElement;

    beforeEach(() => {
        document.body.innerHTML = '';
        container = document.createElement('div');
        container.innerHTML = `
            <div id="favoritesLoadError" class="hide padded-left padded-right padded-top padded-bottom-page">
                <p class="favoritesLoadErrorMessage"></p>
                <button is="emby-button" type="button" class="raised button-accent btnFavoritesRetry">
                    <span>Retry</span>
                </button>
            </div>
            <div id="favoritesEmptyState" class="hide padded-left padded-right padded-top padded-bottom-page centerMessage">
                <div class="noItemsMessage">
                    <p class="secondary">MessageNoFavoritesAvailable</p>
                </div>
            </div>
            <div class="sections"></div>
        `;
        document.body.appendChild(container);
    });

    it('displays empty state when all sections have no items', async () => {
        const tab = new FavoritesTab(container, {});

        // Mock itemsContainer resume to resolve with all sections hidden (empty)
        const itemsContainers = container.querySelectorAll('.itemsContainer');
        for (const ic of itemsContainers) {
            (ic as unknown as { resume: () => Promise<void> }).resume = vi.fn().mockResolvedValue(undefined);
            const parent = ic.closest('.verticalSection');
            parent?.classList.add('hide');
        }

        await tab.onResume({});

        const emptyState = container.querySelector('#favoritesEmptyState');
        expect(emptyState?.classList.contains('hide')).toBe(false);
    });

    it('hides empty state when at least one section has items', async () => {
        const tab = new FavoritesTab(container, {});

        const itemsContainers = container.querySelectorAll('.itemsContainer');
        for (const ic of itemsContainers) {
            (ic as unknown as { resume: () => Promise<void> }).resume = vi.fn().mockResolvedValue(undefined);
            const parent = ic.closest('.verticalSection');
            parent?.classList.add('hide');
        }

        // Simulate a section that loaded items and is visible
        const firstSection = container.querySelector('.verticalSection');
        firstSection?.classList.remove('hide');

        await tab.onResume({});

        const emptyState = container.querySelector('#favoritesEmptyState');
        expect(emptyState?.classList.contains('hide')).toBe(true);
    });

    it('shows error state when section resumption fails and retries upon button click', async () => {
        const tab = new FavoritesTab(container, {});

        const itemsContainers = container.querySelectorAll('.itemsContainer');
        if (itemsContainers.length > 0) {
            (itemsContainers[0] as unknown as { resume: () => Promise<void> }).resume =
                vi.fn().mockRejectedValue(new Error('Connection error'));
        }

        await tab.onResume({});

        const errorDiv = container.querySelector('#favoritesLoadError');
        const sectionsDiv = container.querySelector('.sections');
        expect(errorDiv?.classList.contains('hide')).toBe(false);
        expect(sectionsDiv?.classList.contains('hide')).toBe(true);

        // Click retry
        if (itemsContainers.length > 0) {
            (itemsContainers[0] as unknown as { resume: () => Promise<void> }).resume =
                vi.fn().mockResolvedValue(undefined);
        }
        const retryBtn = container.querySelector<HTMLButtonElement>('.btnFavoritesRetry');
        retryBtn?.click();

        await new Promise((resolve) => setTimeout(resolve, 20));

        expect(errorDiv?.classList.contains('hide')).toBe(true);
        expect(sectionsDiv?.classList.contains('hide')).toBe(false);
    });
});
