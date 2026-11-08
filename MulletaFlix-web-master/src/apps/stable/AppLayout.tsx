import React from 'react';
import { Outlet } from 'react-router-dom';

import AppBody from 'components/AppBody';
import CustomCss from 'components/CustomCss';
import ThemeCss from 'components/ThemeCss';
import AndroidAppInstallPrompt from 'components/AndroidAppInstallPrompt';

export default function AppLayout() {
    return (
        <>
            <AppBody>
                <Outlet />
            </AppBody>
            <AndroidAppInstallPrompt />
            <ThemeCss />
            <CustomCss />
        </>
    );
}
