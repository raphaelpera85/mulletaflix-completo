export declare global {
    import { ApiClient, Events } from 'jellyfin-apiclient';

    interface NativeShellAppHost {
        appName(): string;
        appVersion(): string;
        deviceId(): string;
        deviceName(): string;
        exit(): void;
        getDefaultLayout(): string;
        getDeviceProfile(profileBuilder: (...args: never[]) => unknown, version: string): unknown;
        init(): unknown;
        screen(): { width: number; height: number; maxAllowedWidth?: number } | null;
        supports(command: string): boolean;
    }

    interface NativeShell {
        AppHost: NativeShellAppHost;
        disableFullscreen?(): void;
        downloadFile?(item: Record<string, unknown>): void;
        downloadFiles?(items: Array<Record<string, unknown>>): void;
        enableFullscreen?(): void;
        findServers?(timeout: number): Promise<Array<{ Id: string; Address: string; EndpointAddress?: string; Name: string; [key: string]: unknown }>>;
        getPlugins(): string[];
        hideMediaSession?(): void;
        onLocalUserSignedIn?(user: unknown, accessToken: string): Promise<void> | void;
        onLocalUserSignedOut?(logoutInfo: unknown): void;
        openClientSettings?(): void;
        openDownloadManager?(): void;
        openUrl?(url: string, target?: string): void;
        selectServer?(): void;
        updateMediaSession?(mediaInfo: Record<string, unknown>): void;
        updateVolumeLevel?(volume: number): void;
    }

    interface Window {
        ApiClient: ApiClient;
        Events: Events;
        NativeShell?: NativeShell;
        YT?: {
            Player: new (elementId: string, options: unknown) => unknown;
            PlayerState: Record<string, number>;
        };
        onYouTubeIframeAPIReady?: () => void;
        Loading: {
            show();
            hide();
        }
    }

    interface DocumentEventMap {
        'viewshow': CustomEvent;
    }

    interface Document {
        registerElement(name: string, options: { prototype: object; extends?: string }): void;
    }

    const __COMMIT_SHA__: string;
    const __JF_BUILD_VERSION__: string;
    const __PACKAGE_JSON_NAME__: string;
    const __PACKAGE_JSON_VERSION__: string;
    const __USE_SYSTEM_FONTS__: boolean;
    const __WEBPACK_SERVE__: boolean;
}
