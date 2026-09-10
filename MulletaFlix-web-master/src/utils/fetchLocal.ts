interface FetchOptions {
    cache?: string
    timeout?: number
}

const URL_RESOLVER = document.createElement('a');

// `fetch` with `file:` support
// Recent browsers seem to support `file` protocol under some conditions.
// Based on https://github.com/github/fetch/pull/92#issuecomment-174730593
//          https://github.com/github/fetch/pull/92#issuecomment-512187452
export default async function fetchLocal(url: string, options?: FetchOptions) {
    URL_RESOLVER.href = url;

    const requestURL = URL_RESOLVER.href;

    return new Promise<Response>((resolve, reject) => {
        const xhr = new XMLHttpRequest;

        xhr.onload = () => {
            // `file` protocol has invalid OK status
            let status = xhr.status;
            if (requestURL.startsWith('file:') && status === 0) {
                status = 200;
            }

            resolve(new Response(xhr.responseText, { status }));
        };

        xhr.onerror = () => {
            reject(new TypeError('Local request failed'));
        };

        xhr.ontimeout = () => {
            reject(new TypeError(`Local request timed out after ${options?.timeout ?? 10000}ms`));
        };

        xhr.open('GET', url);
        xhr.timeout = options?.timeout ?? 10000;

        if (options?.cache) {
            xhr.setRequestHeader('Cache-Control', options.cache);
        }

        xhr.send(null);
    });
}
