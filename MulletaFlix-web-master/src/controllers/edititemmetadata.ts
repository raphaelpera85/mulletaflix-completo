import loading from 'components/loading/loading';
import { getCurrentItemId, setCurrentItemId } from 'scripts/editorsidebar';

declare const ApiClient: {
    serverInfo: () => { Id: string };
};

interface ItemClickedEventDetail {
    id: string;
}

interface ItemClickedEvent extends CustomEvent<ItemClickedEventDetail> {
    detail: ItemClickedEventDetail;
}

function reload(context: HTMLElement, itemId: string | undefined): void {
    loading.show();

    if (itemId) {
        void import('../components/metadataEditor/metadataEditor').then(({ default: metadataEditor }) => {
            const content = context.querySelector('.editPageInnerContent');
            if (!(content instanceof HTMLElement)) {
                return;
            }

            return metadataEditor.embed(content, itemId, ApiClient.serverInfo().Id);
        }).then(() => {
            loading.hide();
        }).catch((error: unknown) => {
            console.error('[EditItemMetadata] failed to load metadata editor', error);
            loading.hide();
        });
    } else {
        const content = context.querySelector('.editPageInnerContent');
        if (content) {
            content.innerHTML = '';
        }
        loading.hide();
    }
}

export default function (view: HTMLElement): void {
    view.addEventListener('viewshow', (() => {
        reload(view, getCurrentItemId());
    }) as EventListener);

    setCurrentItemId('');

    (view.querySelector('.libraryTree') as HTMLElement).addEventListener(
        'itemclicked',
        ((event: ItemClickedEvent) => {
            const data = event.detail;

            if (data.id != getCurrentItemId()) {
                setCurrentItemId(data.id);
                reload(view, data.id);
            }
        }) as EventListener
    );
}
