import { expect } from '@playwright/test';

export const MOVIES_LIBRARY = {
    name: 'Filmes',
    type: 'movies',
    path: 'D:\\Users\\Raphael\\Videos\\Filmes',
    aliases: [ 'Filmes', 'Movies', 'Movie', 'Filmes locais', 'Local movies' ]
};

export const SERIES_LIBRARY = {
    name: 'Series',
    type: 'tvshows',
    path: 'D:\\Users\\Raphael\\Videos\\Series',
    aliases: [ 'Series', 'Séries', 'TV Shows', 'Shows', 'Programas de TV' ]
};

const DEFAULT_MEDIA_LIBRARIES = [ MOVIES_LIBRARY, SERIES_LIBRARY ];

function normalizeName(name) {
    return String(name || '').trim().toLowerCase();
}

function getFolderPaths(folder) {
    return [
        folder?.Path,
        folder?.PathInfo?.Path,
        ...(Array.isArray(folder?.PathInfos) ? folder.PathInfos.map(pathInfo => pathInfo?.Path) : [])
    ].filter(Boolean);
}

function matchesLibrary(folder, library) {
    const folderName = normalizeName(folder?.Name);
    const folderType = normalizeName(folder?.CollectionType);
    const libraryName = normalizeName(library?.name);
    const aliases = [ library?.name, ...(library?.aliases || []) ].map(normalizeName);
    const libraryType = normalizeName(library?.type);
    const folderPaths = getFolderPaths(folder).map(normalizeName);
    const libraryPath = normalizeName(library?.path);

    return (
        aliases.includes(folderName)
        || aliases.some(alias => alias && folderName.includes(alias))
        || folderType === libraryType
        || (libraryPath && folderPaths.includes(libraryPath))
        || (libraryName && folderName.includes(libraryName))
    );
}

/**
 * Waits for the ApiClient to be ready and authenticated.
 * This prevents page.evaluate() errors caused by accessing ApiClient before it's initialized.
 * 
 * @param {Page} page - Playwright page object
 * @param {Object} options - Configuration options
 * @param {number} options.timeoutMs - Maximum time to wait (default: 30s)
 * @returns {Promise<void>}
 */
export async function waitForApiClientReady(page, { timeoutMs = 30_000 } = {}) {
    const deadline = Date.now() + timeoutMs;
    let lastError;

    while (Date.now() < deadline) {
        try {
            const isReady = await page.evaluate(() => {
                // Check that ApiClient exists and key methods are available
                if (!window.ApiClient) {
                    return false;
                }

                // Verify critical methods exist
                if (typeof window.ApiClient.getVirtualFolders !== 'function') {
                    return false;
                }

                if (typeof window.ApiClient.getCurrentUserId !== 'function') {
                    return false;
                }

                // Try to get current user ID to verify authentication state
                try {
                    const userId = window.ApiClient.getCurrentUserId();
                    return Boolean(userId);
                } catch {
                    return false;
                }
            }).catch((err) => {
                // page.evaluate() itself threw, likely navigation or context loss
                lastError = err;
                return false;
            });

            if (isReady) {
                return;
            }
        } catch (error) {
            lastError = error;
        }

        // Wait 500ms before retry
        await page.waitForTimeout(500);
    }

    throw new Error(
        `ApiClient did not become ready within ${timeoutMs}ms. Last error: ${lastError?.message || 'Unknown'}`
    );
}

/**
 * Executes a function with retry logic, handling navigation/context losses.
 * Retries with exponential backoff if page.evaluate() fails with Response error.
 * 
 * @param {Page} page - Playwright page object
 * @param {Function} fn - Function to execute in browser context
 * @param {*} args - Arguments to pass to the function
 * @param {Object} options - Configuration options
 * @param {number} options.maxAttempts - Maximum number of attempts (default: 3)
 * @param {number} options.initialDelayMs - Initial delay before retry (default: 500ms)
 * @returns {Promise<*>} - Result of the function
 */
async function evaluateWithRetry(page, fn, args, { maxAttempts = 3, initialDelayMs = 500 } = {}) {
    let lastError;
    let delayMs = initialDelayMs;

    for (let attempt = 1; attempt <= maxAttempts; attempt++) {
        try {
            // Always wait for ApiClient to be ready before attempting evaluate
            await waitForApiClientReady(page, { timeoutMs: 10_000 });

            // Now safe to evaluate
            return await page.evaluate(fn, args);
        } catch (error) {
            lastError = error;

            // Check if this is a navigation/context loss error (Response error)
            const isContextLoss = error?.message?.includes('Response') || 
                                 error?.message?.includes('context') ||
                                 error?.message?.includes('Target page');

            if (!isContextLoss || attempt === maxAttempts) {
                // Not a context loss error, or we're out of retries - throw immediately
                throw error;
            }

            // Wait with exponential backoff before retrying
            console.warn(
                `[evaluateWithRetry] Attempt ${attempt}/${maxAttempts} failed: ${error.message}. Retrying in ${delayMs}ms...`
            );

            await page.waitForTimeout(delayMs);
            delayMs *= 2; // Exponential backoff
        }
    }

    throw lastError;
}

export async function ensureMediaLibraries(page, libraries = DEFAULT_MEDIA_LIBRARIES) {
    const createdLibraries = await evaluateWithRetry(page, async (librarySeed) => {
        const existing = await window.ApiClient.getVirtualFolders();
        const created = [];

        for (const library of librarySeed) {
            const aliases = [ library.name, ...(library.aliases || []) ]
                .map(name => String(name || '').trim().toLowerCase())
                .filter(Boolean);

            if (existing.some(folder => {
                const folderName = String(folder.Name || '').trim().toLowerCase();
                const folderType = String(folder.CollectionType || '').trim().toLowerCase();
                const folderPathInfos = Array.isArray(folder.PathInfos) ? folder.PathInfos : [];
                const folderPaths = folderPathInfos.map(pathInfo => String(pathInfo?.Path || '').trim().toLowerCase());

                return (
                    aliases.includes(folderName)
                    || aliases.some(alias => alias && folderName.includes(alias))
                    || folderType === String(library.type || '').trim().toLowerCase()
                    || folderPaths.includes(String(library.path || '').trim().toLowerCase())
                );
            })) {
                continue;
            }

            await window.ApiClient.addVirtualFolder(library.name, library.type, true, {
                PathInfos: [
                    { Path: library.path }
                ]
            });
            created.push(library.name);
        }

        return {
            existingCount: existing.length,
            created
        };
    }, libraries, { maxAttempts: 3, initialDelayMs: 500 });

    return createdLibraries;
}

export async function waitForMediaRecognition(page, {
    timeoutMs = 20 * 60 * 1000,
    pollIntervalMs = 30_000
} = {}) {
    const deadline = Date.now() + timeoutMs;

    while (Date.now() < deadline) {
        const counts = await evaluateWithRetry(
            page,
            async () => window.ApiClient.getItemCounts(),
            undefined,
            { maxAttempts: 2, initialDelayMs: 500 }
        );

        const movieCount = Number(counts?.MovieCount || 0);
        const seriesCount = Number(counts?.SeriesCount || 0);

        if (movieCount > 0 && seriesCount > 0) {
            return {
                MovieCount: movieCount,
                SeriesCount: seriesCount
            };
        }

        await page.waitForTimeout(pollIntervalMs);
    }

    throw new Error('Media recognition did not stabilize before timeout.');
}

export async function ensureMediaLibrariesReady(page, options = {}) {
    const libraries = options.libraries || DEFAULT_MEDIA_LIBRARIES;
    await ensureMediaLibraries(page, libraries);

    await evaluateWithRetry(
        page,
        async () => {
            const tasks = await window.ApiClient.getScheduledTasks();
            const refreshTask = tasks?.find(task => task?.Key === 'RefreshLibrary' && task?.Id);

            if (refreshTask?.Id) {
                try {
                    await window.ApiClient.startScheduledTask(refreshTask.Id);
                } catch (error) {
                    console.warn('Unable to start RefreshLibrary task', error);
                }
            }
        },
        undefined,
        { maxAttempts: 2, initialDelayMs: 500 }
    );

    await page.waitForTimeout(options.settleMs ?? 60_000);
    return null;
}

export async function getVirtualFolders(page) {
    return evaluateWithRetry(
        page,
        async () => {
            try {
                return await window.ApiClient.getVirtualFolders();
            } catch (error) {
                console.warn('Unable to load virtual folders', error);
                return [];
            }
        },
        undefined,
        { maxAttempts: 2, initialDelayMs: 500 }
    );
}

export async function getVirtualFolderByName(page, name) {
    const folders = await getVirtualFolders(page);
    const names = Array.isArray(name) ? name : [ name ];
    const normalizedNames = names.map(normalizeName).filter(Boolean);

    return folders.find(folder => {
        const folderName = normalizeName(folder.Name);
        const folderType = normalizeName(folder.CollectionType);
        const folderPaths = getFolderPaths(folder).map(normalizeName);

        return (
            normalizedNames.includes(folderName)
            || normalizedNames.some(alias => alias && folderName.includes(alias))
            || normalizedNames.includes(folderType)
            || folderPaths.some(path => normalizedNames.includes(path))
        );
    }) || null;
}

export async function getVirtualFolderByLibrary(page, library) {
    const folders = await getVirtualFolders(page);
    return folders.find(folder => matchesLibrary(folder, library)) || null;
}

export async function getFirstItemFromVirtualFolder(page, folderId) {
    if (!folderId) {
        return null;
    }

    return evaluateWithRetry(
        page,
        async (parentId) => {
            const result = await window.ApiClient.getItems(window.ApiClient.getCurrentUserId(), {
                ParentId: parentId,
                Limit: 1,
                Recursive: true,
                Fields: 'PrimaryImageAspectRatio,ParentId,Path,ProviderIds'
            });
            const item = result?.Items?.[0];

            if (!item?.Id) {
                return null;
            }

            return {
                id: item.Id,
                name: item.Name || item.Id
            };
        },
        folderId,
        { maxAttempts: 2, initialDelayMs: 500 }
    );
}

export async function getFirstInstalledPlugin(page) {
    return evaluateWithRetry(
        page,
        async () => {
            const plugins = await window.ApiClient.getInstalledPlugins();
            const plugin = plugins?.[0];

            if (!plugin?.Id) {
                return null;
            }

            return {
                id: plugin.Id,
                name: plugin.Name || plugin.Id
            };
        },
        undefined,
        { maxAttempts: 2, initialDelayMs: 500 }
    );
}

export async function getFirstScheduledTask(page) {
    return evaluateWithRetry(
        page,
        async () => {
            const tasks = await window.ApiClient.getScheduledTasks();
            const task = tasks?.[0];

            if (!task?.Id) {
                return null;
            }

            return {
                id: task.Id,
                name: task.Name || task.Key || task.Id
            };
        },
        undefined,
        { maxAttempts: 2, initialDelayMs: 500 }
    );
}

export async function getFirstServerLogFile(page) {
    return evaluateWithRetry(
        page,
        async () => {
            const logs = await window.ApiClient.getServerLogs();
            const log = logs?.[0];

            if (!log?.Name) {
                return null;
            }

            return {
                name: log.Name
            };
        },
        undefined,
        { maxAttempts: 2, initialDelayMs: 500 }
    );
}

export async function ensureUserMediaAccess(page, userId) {
    await evaluateWithRetry(
        page,
        async (targetUserId) => {
            const user = await window.ApiClient.getUser(targetUserId);
            const policy = {
                ...user.Policy,
                EnableAllFolders: true,
                EnableAllChannels: true,
                EnableMediaPlayback: true,
                EnableLiveTvAccess: true,
                EnableLiveTvManagement: false,
                IsAdministrator: false
            };

            await window.ApiClient.updateUserPolicy(targetUserId, policy);
        },
        userId,
        { maxAttempts: 2, initialDelayMs: 500 }
    );

    await expect(page.locator('#loginPage')).toBeHidden().catch(() => true);
}
