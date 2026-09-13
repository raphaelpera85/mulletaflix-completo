
/**
 * Module for imageUploader.
 * @module components/imageUploader/imageUploader
 */

import dialogHelper from '../dialogHelper/dialogHelper';
import escapeHtml from 'escape-html';
import dom from '../../utils/dom';
import loading from '../loading/loading';
import scrollHelper from '../../scripts/scrollHelper';
import layoutManager from '../layoutManager';
import globalize from '../../lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';

import '../../elements/emby-button/emby-button';
import '../../elements/emby-select/emby-select';
import '../formdialog.scss';
import './style.scss';
import toast from '../toast/toast';
import template from './imageUploader.template.html';

/* Legacy Jellyfin API clients and dialog options are dynamically shaped at runtime. */
/* eslint-disable @typescript-eslint/no-explicit-any */

let currentItemId: string;
let currentServerId: string;
let currentFile: File | null;
let hasChanges = false;

function onFileReaderError(evt: ProgressEvent<FileReader>): void {
    const error = (evt.target as FileReader).error;
    if (!error) return;
    switch (error.name) {
        case 'NotFoundError':
            toast(globalize.translate('MessageFileReadError'));
            break;
        case 'AbortError':
            break;
        default:
            toast(globalize.translate('MessageFileReadError'));
            break;
    }
}

function setFiles(page: HTMLElement, files: FileList | null): void {
    if (!files || files.length === 0) return;
    const file = files[0];

    if (!file?.type.startsWith('image/')) {
        page.querySelector('#imageOutput')!.innerHTML = '';
        page.querySelector('#fldUpload')!.classList.add('hide');
        currentFile = null;
        return;
    }

    currentFile = file;

    const reader = new FileReader();

    reader.onerror = onFileReaderError;
    reader.onloadstart = () => {
        page.querySelector('#fldUpload')!.classList.add('hide');
    };
    reader.onabort = () => {
        console.debug('File read cancelled');
    };

    // Closure to capture the file information.
    reader.onload = (theFile => {
        return (e: ProgressEvent<FileReader>) => {
            // Render thumbnail.
            const imageData = (e.target as FileReader).result;
            const safeImageData = typeof imageData === 'string' && /^data:image\/(?:jpeg|png|gif|webp|bmp);base64,/i.test(imageData) ? imageData : '';
            const html = ['<img style="max-width:100%;max-height:100%;" src="', escapeHtml(safeImageData), '" title="', escapeHtml(theFile.name), '"/>'].join('');

            page.querySelector('#imageOutput')!.innerHTML = html;
            page.querySelector('#dropImageText')!.classList.add('hide');
            page.querySelector('#fldUpload')!.classList.remove('hide');
        };
    })(file);

    // Read in the image file as a data URL.
    reader.readAsDataURL(file);
}

// eslint-disable-next-line sonarjs/no-invariant-returns
function onSubmit(this: HTMLElement, e: Event): boolean {
    const file = currentFile;

    if (!file) {
        return false;
    }

    if (!file.type.startsWith('image/')) {
        toast(globalize.translate('MessageImageFileTypeAllowed'));
        e.preventDefault();
        return false;
    }

    const dlg = dom.parentWithClass(this, 'dialog') as HTMLElement;

    const imageType = (dlg.querySelector('#selectImageType') as HTMLSelectElement).value;
    if (imageType === 'None') {
        toast(globalize.translate('MessageImageTypeNotSelected'));
        e.preventDefault();
        return false;
    }

    void loading.withLoading(() => (ServerConnections.getApiClient(currentServerId) as any).uploadItemImage(currentItemId, imageType, file))
        .then(() => {
            (dlg.querySelector('#uploadImage') as HTMLInputElement).value = '';
            hasChanges = true;
            dialogHelper.close(dlg);
        })
        .catch((error: unknown) => {
            console.error('[ImageUploader] failed to upload image', error);
            toast(globalize.translate('ImageUploadFailed'));
        });

    e.preventDefault();
    return false;
}

function initEditor(page: HTMLElement): void {
    (page.querySelector('form') as HTMLFormElement).addEventListener('submit', onSubmit as EventListener);

    (page.querySelector('#uploadImage') as HTMLInputElement).addEventListener('change', function (this: HTMLInputElement) {
        setFiles(page, this.files);
    });

    page.querySelector('.btnBrowse')!.addEventListener('click', () => {
        (page.querySelector('#uploadImage') as HTMLInputElement).click();
    });
}

interface ImageUploaderOptions {
    itemId?: string;
    serverId?: string;
    imageType?: string;
}

function showEditor(options: ImageUploaderOptions, resolve: (hasChanges: boolean) => void): void {
    options = options || {};

    currentItemId = options.itemId || '';
    currentServerId = options.serverId || '';

    const dialogOptions: any = {
        removeOnClose: true
    };

    if (layoutManager.tv) {
        dialogOptions.size = 'fullscreen';
    } else {
        dialogOptions.size = 'small';
    }

    const dlg = dialogHelper.createDialog(dialogOptions);

    dlg.classList.add('formDialog');

    dlg.innerHTML = globalize.translateHtml(template, 'core');

    if (layoutManager.tv) {
        scrollHelper.centerFocus.on(dlg, false);
    }

    // Has to be assigned a z-index after the call to .open()
    dlg.addEventListener('close', () => {
        if (layoutManager.tv) {
            scrollHelper.centerFocus.off(dlg, false);
        }

        resolve(hasChanges);
    });

    dialogHelper.open(dlg).catch(() => undefined);

    initEditor(dlg);

    (dlg.querySelector('#selectImageType') as HTMLSelectElement).value = options.imageType || 'Primary';

    dlg.querySelector('.btnCancel')!.addEventListener('click', () => {
        dialogHelper.close(dlg);
    });
}

export function show(options: ImageUploaderOptions): Promise<boolean> {
    return new Promise(resolve => {
        hasChanges = false;

        showEditor(options, resolve);
    });
}

export default {
    show: show
};

/* eslint-enable @typescript-eslint/no-explicit-any */
