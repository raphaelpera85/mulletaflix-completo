declare module 'jquery';

declare module '*.png' {
    const value: string;
    export = value;
}

declare module '*.html' {
    const value: string;
    export default value;
}

// Legacy controllers still access the browser-global constructor injected by the
// Jellyfin runtime. Keep this boundary isolated while callers are migrated.
declare const ApiClient: import('jellyfin-apiclient').ApiClient;

declare module 'jellyfin-apiclient' {
    interface ApiClient {
        serverInfo(info?: { Id?: string; [key: string]: unknown }): { Id?: string; [key: string]: unknown };
        subscribe(messageTypes: unknown[], callback: (message: unknown) => void): () => void;
    }
}
