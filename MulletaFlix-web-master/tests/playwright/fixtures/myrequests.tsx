import React from 'react';
import { createRoot } from 'react-dom/client';
import { extendTheme, ThemeProvider } from '@mui/material/styles';
import MyMediaRequestsPage from '../../../src/apps/experimental/routes/myrequests';
import { DEFAULT_COLOR_SCHEME, DEFAULT_THEME_OPTIONS } from '../../../src/themes/_base/theme';

const theme = extendTheme({ ...DEFAULT_THEME_OPTIONS, colorSchemes: { dark: DEFAULT_COLOR_SCHEME } });

createRoot(document.getElementById('root')!).render(
    <ThemeProvider theme={theme} defaultMode='dark'>
        <MyMediaRequestsPage />
    </ThemeProvider>
);
