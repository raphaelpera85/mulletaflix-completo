declare module 'jquery';

declare module '*.png' {
    const value: string;
    export = value;
}

declare module '*.html' {
    const value: string;
    export default value;
}

declare const ApiClient: import('jellyfin-apiclient').ApiClient;
