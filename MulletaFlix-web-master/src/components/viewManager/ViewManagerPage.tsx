import { Action } from 'history';
import Alert from '@mui/material/Alert';
import Button from '@mui/material/Button';
import { FunctionComponent, useCallback, useEffect, useState } from 'react';
import { useLocation, useNavigationType } from 'react-router-dom';

import globalize from 'lib/globalize';
import type { RestoreViewFailResponse } from 'types/viewManager';

import viewManager from './viewManager';
import { AppType } from 'constants/appType';
import type { ControllerFactory } from '../viewContainer';

export interface ViewManagerPageProps {
    appType?: AppType
    controller: string
    view: string
    type?: string
    isFullscreen?: boolean
    isNowPlayingBarEnabled?: boolean
    isThemeMediaSupported?: boolean
    transition?: string
}

interface ViewOptions {
    url: string
    type?: string
    state: unknown
    autoFocus: boolean
    fullscreen?: boolean
    transition?: string
    options: {
        supportsThemeMedia?: boolean
        enableMediaControl?: boolean
    }
}

const dashboardControllers = import.meta.glob([
    '../../apps/dashboard/controllers/**/*.ts',
    '../../apps/dashboard/controllers/**/*.tsx',
    '../../apps/dashboard/controllers/**/*.html'
]);

const wizardControllers = import.meta.glob([
    '../../apps/wizard/controllers/**/*.ts',
    '../../apps/wizard/controllers/**/*.tsx',
    '../../apps/wizard/controllers/**/*.html'
]);

const defaultControllers = import.meta.glob([
    '../../controllers/**/*.ts',
    '../../controllers/**/*.tsx',
    '../../controllers/**/*.html'
]);

// Separate glob for raw HTML views
const dashboardViews = import.meta.glob([
    '../../apps/dashboard/controllers/**/*.html'
], { query: '?raw', import: 'default' });

const wizardViews = import.meta.glob([
    '../../apps/wizard/controllers/**/*.html'
], { query: '?raw', import: 'default' });

const defaultViews = import.meta.glob([
    '../../controllers/**/*.html'
], { query: '?raw', import: 'default' });

function getDefaultExport(moduleValue: unknown): unknown {
    if (moduleValue && typeof moduleValue === 'object' && 'default' in moduleValue) {
        return (moduleValue as { default?: unknown }).default || moduleValue;
    }

    return moduleValue;
}

const resolveModule = <T = unknown>(glob: Record<string, () => Promise<unknown>>, basePath: string, name: string): Promise<T> => {
    const extensions = ['', '.ts', '.tsx', '.html'];
    for (const ext of extensions) {
        const path = `${basePath}/${name}${ext}`;
        const loadFn = glob[path];
        if (loadFn) {
            return loadFn().then(mod => getDefaultExport(mod) as T);
        }
    }
    return Promise.reject(new Error(`Module not found: ${basePath}/${name}`));
};

const importController = (
    appType: AppType,
    controller: string,
    view: string
) : Promise<[ControllerFactory, string]> => {
    const resolveView = (htmlModule: unknown): string => {
        const html = getDefaultExport(htmlModule);
        if (typeof html === 'string') {
            return globalize.translateHtml(html);
        }
        console.warn('[ViewManagerPage] view module is not a string', controller, view, html);
        return '';
    };
    switch (appType) {
        case AppType.Dashboard:
            return Promise.all([
                resolveModule<ControllerFactory>(dashboardControllers, '../../apps/dashboard/controllers', controller),
                resolveModule<unknown>(dashboardViews, '../../apps/dashboard/controllers', view)
                    .then(resolveView)
            ]);
        case AppType.Wizard:
            return Promise.all([
                resolveModule<ControllerFactory>(wizardControllers, '../../apps/wizard/controllers', controller),
                resolveModule<unknown>(wizardViews, '../../apps/wizard/controllers', view)
                    .then(resolveView)
            ]);
        default:
            return Promise.all([
                resolveModule<ControllerFactory>(defaultControllers, '../../controllers', controller),
                resolveModule<unknown>(defaultViews, '../../controllers', view)
                    .then(resolveView)
            ]);
    }
};

const loadView = async (
    appType: AppType,
    controller: string,
    view: string,
    viewOptions: ViewOptions
) => {
    const [ controllerFactory, viewHtml ] = await importController(appType, controller, view);

    viewManager.loadView({
        ...viewOptions,
        controllerFactory,
        view: viewHtml
    });
};

/**
 * Page component that renders legacy views via the ViewManager.
 * NOTE: Any new pages should use the generic Page component instead.
 */
const ViewManagerPage: FunctionComponent<ViewManagerPageProps> = ({
    appType = AppType.Stable,
    controller,
    view,
    type,
    isFullscreen = false,
    isNowPlayingBarEnabled = true,
    isThemeMediaSupported = false,
    transition
}) => {
    const location = useLocation();
    const navigationType = useNavigationType();
    const [ loadError, setLoadError ] = useState<unknown>(null);
    const [ retryCount, setRetryCount ] = useState(0);
    const handleRetry = useCallback(() => setRetryCount(value => value + 1), []);

    useEffect(() => {
        setLoadError(null);

        const loadPage = () => {
            const viewOptions = {
                url: location.pathname + location.search,
                type,
                state: location.state,
                autoFocus: false,
                fullscreen: isFullscreen,
                transition,
                options: {
                    supportsThemeMedia: isThemeMediaSupported,
                    enableMediaControl: isNowPlayingBarEnabled
                }
            };

            if (navigationType !== Action.Pop) {
                console.debug('[ViewManagerPage] loading view [%s]', view);
                return loadView(appType, controller, view, viewOptions);
            }

            console.debug('[ViewManagerPage] restoring view [%s]', view);
            return viewManager.tryRestoreView(viewOptions)
                .catch(async (result?: RestoreViewFailResponse) => {
                    if (!result?.cancelled) {
                        console.debug('[ViewManagerPage] restore failed; loading view [%s]', view);
                        return loadView(appType, controller, view, viewOptions);
                    }
                });
        };

        loadPage().catch((error: unknown) => {
            console.error('[ViewManagerPage] failed to load legacy view', { view, error });
            setLoadError(error);
        });
    },
    // location.state and navigationType are NOT included as dependencies here since dialogs will update state while the current view stays the same
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
        controller,
        view,
        type,
        isFullscreen,
        isNowPlayingBarEnabled,
        isThemeMediaSupported,
        transition,
        location.pathname,
        location.search,
        retryCount
    ]);

    if (loadError) {
        return (
            <Alert
                severity='error'
                role='alert'
                action={(
                    <Button color='inherit' size='small' onClick={handleRetry}>
                        Tentar novamente
                    </Button>
                )}
                sx={{ m: 2 }}
            >
                Não foi possível carregar esta tela. Tente novamente.
            </Alert>
        );
    }

    return null;
};

export default ViewManagerPage;
