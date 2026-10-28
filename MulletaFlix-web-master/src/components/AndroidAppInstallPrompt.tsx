import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogActions from '@mui/material/DialogActions';
import DialogContent from '@mui/material/DialogContent';
import DialogContentText from '@mui/material/DialogContentText';
import DialogTitle from '@mui/material/DialogTitle';
import React, { useEffect, useState } from 'react';

import browser from 'scripts/browser';

const DISMISSED_KEY = 'mulletaflix.androidAppInstallPrompt.dismissed';
const APK_URL = 'https://github.com/raphaelpera85/mulletaflix-completo/releases/download/app-v1.3.82/mulletaflix-app-v1.3.82.apk';

export default function AndroidAppInstallPrompt() {
    const [open, setOpen] = useState(false);

    useEffect(() => {
        const appMode = (window as Window & { appMode?: string }).appMode;
        if (!browser.android || appMode === 'android' || appMode === 'cordova' || window.NativeShell) return;

        try {
            if (window.localStorage.getItem(DISMISSED_KEY) !== 'true') setOpen(true);
        } catch (error) {
            console.warn('[AndroidAppInstallPrompt] não foi possível ler a preferência local.', error);
            setOpen(true);
        }
    }, []);

    const dismiss = () => {
        try {
            window.localStorage.setItem(DISMISSED_KEY, 'true');
        } catch (error) {
            console.warn('[AndroidAppInstallPrompt] não foi possível salvar a preferência local.', error);
        }
        setOpen(false);
    };

    return (
        <Dialog open={open} onClose={dismiss} aria-labelledby='android-install-title'>
            <DialogTitle id='android-install-title'>Instale o app MulletaFlix</DialogTitle>
            <DialogContent>
                <DialogContentText>
                    Tenha uma experiência melhor no celular com o aplicativo oficial para Android.
                </DialogContentText>
            </DialogContent>
            <DialogActions>
                <Button onClick={dismiss}>Agora não</Button>
                <Button component='a' href={APK_URL} target='_blank' rel='noopener noreferrer' variant='contained'>
                    Baixar APK
                </Button>
            </DialogActions>
        </Dialog>
    );
}
