interface PluginApiClient {
    getUrl(path: string): string;
    accessToken(): string;
}

/** Module imports cannot set authorization headers; authenticate the resource URL. */
export function getPluginControllerUrl(client: PluginApiClient, resource: string): string {
    const token = client.accessToken();
    const url = new URL(client.getUrl('/web/' + resource));
    if (token) url.searchParams.set('api_key', token);
    return url.href;
}
