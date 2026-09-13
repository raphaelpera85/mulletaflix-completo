interface CastWindow extends Window {
    appMode?: 'cordova' | 'android' | string;
    chrome?: Record<string, unknown>;
}

class CastSenderApi {
    load(): Promise<void> {
        const castWindow = window as CastWindow;

        if (castWindow.appMode === 'cordova' || castWindow.appMode === 'android') {
            castWindow.chrome = castWindow.chrome || {};
            return Promise.resolve();
        }

        let ccLoaded = false;
        if (ccLoaded) {
            return Promise.resolve();
        }

        return new Promise<void>((resolve, reject) => {
            const fileref = document.createElement('script');
            fileref.setAttribute('type', 'text/javascript');

            // Resolve when loaded; on error or timeout, resolve gracefully so app startup is never blocked
            fileref.onload = () => {
                ccLoaded = true;
                resolve();
            };

            fileref.onerror = () => {
                console.warn('[CastSenderApi] Cast SDK failed to load (blocked or unavailable); continuing without cast');
                resolve();
            };

            // Safety timeout: CSP blocks do not always fire onerror in all browsers
            const timeout = window.setTimeout(() => {
                console.warn('[CastSenderApi] Cast SDK load timed out; continuing without cast');
                resolve();
            }, 3000);

            const originalResolve = resolve;
            resolve = () => { window.clearTimeout(timeout); originalResolve(); };

            fileref.setAttribute('src', 'https://www.gstatic.com/cv/js/sender/v1/cast_sender.js');
            document.querySelector('head')?.appendChild(fileref);
        });
    }
}

export default CastSenderApi;
