/** Open a small byte range to warm up the stream without retaining its body. */
export async function warmupMediaStream(url: string, fetcher: typeof fetch = fetch): Promise<Response> {
    const response = await fetcher(url, {
        method: 'GET',
        headers: { Range: 'bytes=0-65535' }
    });

    if (!response.ok) {
        await response.body?.cancel().catch(() => undefined);
        throw new Error(`Media stream warmup returned HTTP ${response.status}`);
    }

    if (response.body) {
        const reader = response.body.getReader();
        try {
            await reader.read();
        } finally {
            await reader.cancel().catch(() => undefined);
        }
    }

    return response;
}
