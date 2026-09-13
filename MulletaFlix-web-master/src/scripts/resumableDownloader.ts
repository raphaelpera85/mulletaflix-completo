const DATABASE_NAME = 'mulletaflix-downloads';
const DATABASE_VERSION = 1;
const CHUNKS_STORE = 'chunks';
const METADATA_STORE = 'metadata';
const CHUNK_SIZE = 4 * 1024 * 1024;

interface DownloadMetadata {
    url: string
    fileName: string
    totalBytes?: number
    chunkSize: number
    updatedAt: number
}

interface DownloadChunk {
    id: string
    url: string
    start: number
    data: ArrayBuffer
}

export interface ResumableDownloadOptions {
    url: string
    fileName: string
    signal?: AbortSignal
    onProgress?: (downloadedBytes: number, totalBytes?: number) => void
}

export function parseContentRange(value: string | null): { start: number; total?: number } | null {
    if (!value) return null;

    const match = /^bytes\s+(\d+)-(\d+)\/(\d+|\*)$/i.exec(value.trim());
    if (!match) return null;

    const start = Number(match[1]);
    const total = match[3] === '*' ? undefined : Number(match[3]);
    if (!Number.isSafeInteger(start) || (total !== undefined && !Number.isSafeInteger(total))) return null;

    return { start, total };
}

function getChunkId(url: string, start: number): string {
    return `${url}|${start}`;
}

function openDatabase(): Promise<IDBDatabase | null> {
    if (typeof indexedDB === 'undefined') return Promise.resolve(null);

    return new Promise(resolve => {
        const request = indexedDB.open(DATABASE_NAME, DATABASE_VERSION);
        request.onupgradeneeded = () => {
            const database = request.result;
            if (!database.objectStoreNames.contains(CHUNKS_STORE)) {
                database.createObjectStore(CHUNKS_STORE, { keyPath: 'id' });
            }
            if (!database.objectStoreNames.contains(METADATA_STORE)) {
                database.createObjectStore(METADATA_STORE, { keyPath: 'url' });
            }
        };
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => resolve(null);
    });
}

function readRecord<T>(database: IDBDatabase, storeName: string, key: IDBValidKey): Promise<T | undefined> {
    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, 'readonly');
        const request = transaction.objectStore(storeName).get(key);
        request.onsuccess = () => resolve(request.result as T | undefined);
        request.onerror = () => reject(request.error);
    });
}

function writeRecord(database: IDBDatabase, storeName: string, record: object): Promise<void> {
    return new Promise((resolve, reject) => {
        const transaction = database.transaction(storeName, 'readwrite');
        transaction.objectStore(storeName).put(record);
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
    });
}

function removeRecords(database: IDBDatabase, url: string, starts: number[]): Promise<void> {
    return new Promise((resolve, reject) => {
        const transaction = database.transaction([CHUNKS_STORE, METADATA_STORE], 'readwrite');
        const chunks = transaction.objectStore(CHUNKS_STORE);
        starts.forEach(start => {
            chunks.delete(getChunkId(url, start));
        });
        transaction.objectStore(METADATA_STORE).delete(url);
        transaction.oncomplete = () => resolve();
        transaction.onerror = () => reject(transaction.error);
    });
}

function notifyProgress(options: ResumableDownloadOptions, downloadedBytes: number, totalBytes?: number): void {
    options.onProgress?.(downloadedBytes, totalBytes);
    globalThis.dispatchEvent(new CustomEvent('mulletaflix:download-progress', {
        detail: { url: options.url, downloadedBytes, totalBytes }
    }));
}

function saveBlob(blob: Blob, fileName: string): void {
    const objectUrl = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = objectUrl;
    anchor.download = fileName;
    anchor.rel = 'noopener';
    anchor.click();
    setTimeout(() => URL.revokeObjectURL(objectUrl), 0);
}

async function getStoredChunks(database: IDBDatabase | null, url: string, metadata?: DownloadMetadata): Promise<DownloadChunk[]> {
    if (!database || !metadata) return [];

    const chunks: DownloadChunk[] = [];
    for (let start = 0; start < (metadata.totalBytes ?? Number.MAX_SAFE_INTEGER); start += metadata.chunkSize) {
        const chunk = await readRecord<DownloadChunk>(database, CHUNKS_STORE, getChunkId(url, start));
        if (!chunk) break;
        chunks.push(chunk);
        if (chunk.data.byteLength < metadata.chunkSize) break;
    }
    return chunks;
}

type DownloadChunkResult = {
    data: ArrayBuffer;
    totalBytes?: number;
    actualStart: number;
    complete: boolean;
};

async function fetchChunk(options: ResumableDownloadOptions, nextStart: number, totalBytes?: number): Promise<DownloadChunkResult | null> {
    if (options.signal?.aborted) {
        throw options.signal.reason ?? new DOMException('The download was aborted', 'AbortError');
    }

    const response = await fetch(options.url, {
        cache: 'no-store',
        credentials: 'same-origin',
        headers: nextStart ? { Range: `bytes=${nextStart}-${nextStart + CHUNK_SIZE - 1}` } : undefined,
        signal: options.signal
    });

    if (!response.ok) throw new Error(`Download failed with HTTP ${response.status}`);

    const contentRange = parseContentRange(response.headers.get('Content-Range'));
    if (nextStart > 0 && response.status !== 206) return null;

    const data = await response.arrayBuffer();
    if (!data.byteLength) {
        return { data, totalBytes, actualStart: nextStart, complete: true };
    }

    const actualStart = contentRange?.start ?? nextStart;
    if (actualStart !== nextStart) throw new Error('Server returned a non-contiguous byte range');

    const resolvedTotalBytes = contentRange?.total
        ?? totalBytes
        ?? (response.status === 200 ? Number(response.headers.get('Content-Length')) || undefined : undefined);

    return {
        data,
        totalBytes: resolvedTotalBytes,
        actualStart,
        complete: response.status === 200 || (resolvedTotalBytes !== undefined && nextStart + data.byteLength >= resolvedTotalBytes)
    };
}

/** Downloads a file in persisted byte ranges and resumes after a reload or network failure. */
export async function downloadWithResume(options: ResumableDownloadOptions): Promise<void> {
    const database = await openDatabase();
    let metadata = database ? await readRecord<DownloadMetadata>(database, METADATA_STORE, options.url) : undefined;
    const storedChunks = await getStoredChunks(database, options.url, metadata);
    let downloadedBytes = storedChunks.reduce((total, chunk) => total + chunk.data.byteLength, 0);
    let totalBytes = metadata?.totalBytes;
    let nextStart = storedChunks.length ? storedChunks[storedChunks.length - 1].start + storedChunks[storedChunks.length - 1].data.byteLength : 0;
    const chunks = [...storedChunks];

    while (totalBytes === undefined || nextStart < totalBytes) {
        const result = await fetchChunk(options, nextStart, totalBytes);
        if (!result) {
            // The server ignored Range. Discard partial state to avoid corrupting the file.
            if (database) await removeRecords(database, options.url, chunks.map(chunk => chunk.start));
            chunks.length = 0;
            downloadedBytes = 0;
            nextStart = 0;
            totalBytes = undefined;
            continue;
        }

        if (!result.data.byteLength) break;
        totalBytes = result.totalBytes;

        const chunk: DownloadChunk = {
            id: getChunkId(options.url, result.actualStart),
            url: options.url,
            start: result.actualStart,
            data: result.data
        };
        chunks.push(chunk);
        if (database) {
            metadata = {
                url: options.url,
                fileName: options.fileName,
                totalBytes,
                chunkSize: CHUNK_SIZE,
                updatedAt: Date.now()
            };
            await writeRecord(database, CHUNKS_STORE, chunk);
            await writeRecord(database, METADATA_STORE, metadata);
        }

        downloadedBytes += result.data.byteLength;
        nextStart += result.data.byteLength;
        notifyProgress(options, downloadedBytes, totalBytes);

        if (result.complete) break;
    }

    const orderedChunks = [ ...chunks ].sort((left, right) => left.start - right.start);
    const blob = new Blob(orderedChunks.map(chunk => chunk.data));
    saveBlob(blob, options.fileName);
    if (database) await removeRecords(database, options.url, orderedChunks.map(chunk => chunk.start));
}
