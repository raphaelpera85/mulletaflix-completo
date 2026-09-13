import { ThemeProvider } from '@mui/material/styles';
import React from 'react';
import { Outlet } from 'react-router-dom';

import AppHeader from 'components/AppHeader';
import AppBody from 'components/AppBody';
import Backdrop from 'components/Backdrop';
import appTheme from 'themes';
import { ThemeStorageManager } from 'themes/themeStorageManager';

/**
 * Keeps the theme and legacy DOM shell out of the initial route bootstrap.
 * The route tree still renders the same provider and compatibility elements.
 */
export default function ThemedRootAppLayout() {
    return (
        <ThemeProvider
            theme={appTheme}
            defaultMode='dark'
            storageManager={ThemeStorageManager}
        >
            <Backdrop />
            <AppHeader isHidden />
            <AppBody>
                <Outlet />
            </AppBody>
        </ThemeProvider>
    );
}
