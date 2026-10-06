import React, { useCallback, useEffect, useMemo, useRef } from 'react';
import { useSearchParams } from 'react-router-dom';
import Box from '@mui/material/Box';

import globalize from '../../../lib/globalize';
import { clearBackdrop } from '../../../components/backdrop/backdrop';
import layoutManager from '../../../components/layoutManager';
import Page from '../../../components/Page';
import { EventType } from 'constants/eventType';
import Events from 'utils/events';
import type { TabChangeDetail } from '../../../components/maintabsmanager';
import { PageStateContainer } from 'components/common';
import ListPageSkeleton from 'components/common/ListPageSkeleton';

import '../../../elements/emby-tabs/emby-tabs';
import '../../../elements/emby-button/emby-button';
import '../../../elements/emby-scroller/emby-scroller';

const controllerModules = import.meta.glob('../../../controllers/*.ts');

type OnResumeOptions = {
    autoFocus?: boolean;
    refresh?: boolean
};

type ControllerProps = {
    onResume: (
        options: OnResumeOptions
    ) => void;
    refreshed: boolean;
    onPause: () => void;
    destroy: () => void;
};

type ControllerModule = {
    default: new (element: Element, options: null) => ControllerProps;
};

const Home = () => {
    const [ searchParams ] = useSearchParams();
    const initialTabIndex = parseInt(searchParams.get('tab') ?? '0', 10);

    const libraryMenu = useMemo(async () => ((await import('../../../scripts/libraryMenu')).default), []);
    const mainTabsManager = useMemo(() => import('../../../components/maintabsmanager'), []);
    const tabController = useRef<ControllerProps | null>();
    const tabControllers = useMemo<ControllerProps[]>(() => [], []);
    // ponytail: HEADER_RENDERED and the mount effect can race; only start the first tab load once.
    const homeTabLoadPending = useRef(false);
    const [pageState, setPageState] = React.useState<'loading' | 'error' | 'success'>('loading');

    const documentRef = useRef<Document>(document);
    const element = useRef<HTMLDivElement>(null);

    const setTitle = useCallback(async () => {
        (await libraryMenu).setTitle(null);
    }, [libraryMenu]);

    const getTabs = () => {
        return [{
            name: globalize.translate('Home')
        }, {
            name: globalize.translate('Favorites')
        }];
    };

    const getTabContainers = () => {
        return element.current?.querySelectorAll('.tabContent');
    };

    const getTabController = useCallback((index: number) => {
        if (index == null) {
            throw new Error('index cannot be null');
        }

        let depends = '';

        switch (index) {
            case 0:
                depends = 'hometab';
                break;

            case 1:
                depends = 'favorites';
        }

        const globPath = `../../../controllers/${depends}.ts`;
        const loadFn = controllerModules[globPath];
        if (!loadFn) {
            return Promise.reject(new Error(`Controller not found in glob: ${depends}`));
        }
        return loadFn().then((mod) => {
            const controllerModule = mod as ControllerModule;
            const ControllerFactory = controllerModule.default;
            let controller = tabControllers[index];

            if (!controller) {
                const tabContent = element.current?.querySelector(".tabContent[data-index='" + index + "']")
                    || documentRef.current.querySelector(".homePage .tabContent[data-index='" + index + "']");

                if (!tabContent) {
                    throw new Error(`Home tab content not ready: ${index}`);
                }

                controller = new ControllerFactory(tabContent, null);
                tabControllers[index] = controller;
            }

            return controller;
        });
    }, [ tabControllers ]);

    const loadTab = useCallback((index: number, previousIndex: number | null, retryCount = 0) => {
        getTabController(index)
            .then((controller: ControllerProps) => {
                const refresh = !controller.refreshed;

                return (controller.onResume({
                    autoFocus: previousIndex == null && layoutManager.tv,
                    refresh: refresh
                }) as unknown as Promise<void>).then(() => {
                    controller.refreshed = true;
                    tabController.current = controller;
                    homeTabLoadPending.current = false;
                    setPageState('success');
                }).catch((err: unknown) => {
                    console.error('[Home] failed to resume tab', err);
                    setPageState('error');
                    homeTabLoadPending.current = false;
                });
            })
            .catch((err: unknown) => {
                if (err instanceof Error && err.message.startsWith('Home tab content not ready') && retryCount < 10) {
                    window.requestAnimationFrame(() => loadTab(index, previousIndex, retryCount + 1));
                    return;
                }

                homeTabLoadPending.current = false;
                console.error('[Home] failed to get tab controller', err);
                setPageState('error');
            });
    }, [ getTabController ]);

    const onTabChange = useCallback((e: CustomEvent<TabChangeDetail>) => {
        const newIndex = parseInt(e.detail.selectedTabIndex, 10);
        const previousIndex = e.detail.previousIndex == null ? null : Number(e.detail.previousIndex);

        const previousTabController = previousIndex == null ? null : tabControllers[previousIndex];
        if (previousTabController?.onPause) {
            previousTabController.onPause();
        }

        loadTab(newIndex, previousIndex);
    }, [ loadTab, tabControllers ]);

    const onSetTabs = useCallback(async () => {
        (await mainTabsManager).setTabs(element.current, initialTabIndex, getTabs, getTabContainers, null, onTabChange, false);
    }, [ initialTabIndex, mainTabsManager, onTabChange ]);

    const onResume = useCallback(async () => {
        void setTitle();
        clearBackdrop();

        const currentTabController = tabController.current;

        if (!currentTabController) {
            if (homeTabLoadPending.current) {
                return;
            }

            homeTabLoadPending.current = true;
            const tabsMgr = await mainTabsManager;
            tabsMgr.selectedTabIndex(initialTabIndex);

            window.setTimeout(() => {
                if (!tabController.current) {
                    loadTab(initialTabIndex, null);
                }
            }, 150);
        } else if (currentTabController?.onResume) {
            currentTabController.onResume({});
        }
        documentRef.current.querySelector('.skinHeader')?.classList.add('noHomeButtonHeader');
    }, [ initialTabIndex, loadTab, mainTabsManager, setTitle ]);

    const onPause = useCallback(() => {
        const currentTabController = tabController.current;
        if (currentTabController?.onPause) {
            currentTabController.onPause();
        }
        documentRef.current.querySelector('.skinHeader')?.classList.remove('noHomeButtonHeader');
    }, []);

    const renderHome = useCallback(async () => {
        await onSetTabs();
        await onResume();
    }, [ onResume, onSetTabs ]);

    const handleRetry = useCallback(() => {
        setPageState('loading');
        void renderHome();
    }, [renderHome]);

    useEffect(() => {
        void renderHome();

        return () => {
            onPause();
        };
    }, [onPause, renderHome]);

    useEffect(() => {
        const doc = documentRef.current;
        if (doc) Events.on(doc, EventType.HEADER_RENDERED, renderHome);

        return () => {
            if (doc) Events.off(doc, EventType.HEADER_RENDERED, renderHome);
        };
    }, [ renderHome ]);

    return (
        <div ref={element}>
            <Page
                id='indexPage'
                className='mainAnimatedPage homePage libraryPage allLibraryPage backdropPage pageWithAbsoluteTabs withTabs'
                isBackButtonEnabled={false}
                backDropType='movie,series,book'
            >
                <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100%', position: 'relative' }}>
                    {/*
                        Home uses an imperative, DOM-query-based tab controller system
                        (getTabController above queries '.tabContent[data-index=...]' directly).
                        Those divs must always be mounted -- PageStateContainer's 'loading'/'error'
                        cases normally REPLACE children instead of rendering them, which previously
                        hid these divs until pageState became 'success'. But pageState only becomes
                        'success' AFTER getTabController finds the div, so it could never find it:
                        infinite "Home tab content not ready" retries, then a permanent error state.
                        Render the structural tabContent divs unconditionally and only overlay the
                        loading/error UI on top, instead of letting PageStateContainer swap them out.
                    */}
                    {pageState !== 'success' && (
                        <PageStateContainer
                            state={pageState}
                            onRetry={handleRetry}
                            loadingComponent={<ListPageSkeleton rows={2} itemsPerRow={4} />}
                            errorMessage={globalize.translate('ErrorDefault')}
                        />
                    )}
                    <div
                        className='tabContent pageTabContent'
                        id='homeTab'
                        data-index='0'
                        style={pageState === 'success' ? undefined : { display: 'none' }}
                    >
                        <div className='sections'></div>
                    </div>
                    <div
                        className='tabContent pageTabContent'
                        id='favoritesTab'
                        data-index='1'
                        style={pageState === 'success' ? undefined : { display: 'none' }}
                    >
                        <div className='sections'></div>
                    </div>
                </Box>
            </Page>
        </div>
    );
};

export default Home;
