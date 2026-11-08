import { createRoot, type Root } from 'react-dom/client';
import { act } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import AndroidAppInstallPrompt from './AndroidAppInstallPrompt';
import browser from 'scripts/browser';

vi.mock('scripts/browser', () => ({ default: { android: false } }));

describe('AndroidAppInstallPrompt', () => {
    let container: HTMLDivElement;
    let root: Root;

    beforeEach(() => {
        container = document.createElement('div');
        document.body.appendChild(container);
        root = createRoot(container);
        window.localStorage.clear();
        (browser as { android: boolean }).android = true;
    });

    afterEach(() => {
        act(() => root.unmount());
        container.remove();
        delete (window as Window & { appMode?: string }).appMode;
        delete window.NativeShell;
    });

    it('suggests the published APK only in the Android web browser', async () => {
        await act(async () => root.render(<AndroidAppInstallPrompt />));

        expect(document.body.textContent).toContain('Instale o app MulletaFlix');
        expect(document.querySelector<HTMLAnchorElement>('a[href*="mulletaflix-app-v1.3.82.apk"]')).not.toBeNull();
    });

    it('does not show the prompt again after dismissal', async () => {
        await act(async () => root.render(<AndroidAppInstallPrompt />));
        const dismissButton = Array.from(document.querySelectorAll('button'))
            .find(button => button.textContent === 'Agora não');
        expect(dismissButton).toBeDefined();

        await act(async () => dismissButton?.click());
        expect(window.localStorage.getItem('mulletaflix.androidAppInstallPrompt.dismissed')).toBe('true');

        await act(async () => {
            root.unmount();
            root = createRoot(container);
            root.render(<AndroidAppInstallPrompt />);
        });
        expect(document.body.textContent).not.toContain('Instale o app MulletaFlix');
    });

    it('does not show the prompt inside a native Android app', async () => {
        (window as Window & { appMode?: string }).appMode = 'android';
        await act(async () => root.render(<AndroidAppInstallPrompt />));
        expect(document.body.textContent).not.toContain('Instale o app MulletaFlix');
    });
});
