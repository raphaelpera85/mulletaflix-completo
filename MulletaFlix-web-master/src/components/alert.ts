import { appRouter } from './router/appRouter';
import dialog, { type DialogOptions } from './dialog/dialog';
import globalize from '../lib/globalize';

export default async function alert(text: string | DialogOptions, title?: string): Promise<void> {
    const options: DialogOptions = typeof text === 'string' ? { title, text } : text;

    await appRouter.ready();

    options.buttons = [
        {
            name: globalize.translate('ButtonGotIt'),
            id: 'ok',
            type: 'submit'
        }
    ];

    await dialog.show(options);
}
