import { createGuide, type GuideInstance } from '../../components/guide/guide';

declare const ApiClient: {
    serverId(): string;
};

interface LiveTvGuideController {
    renderTab: () => void;
    onShow: () => void;
    onHide: () => void;
}

export default function (this: LiveTvGuideController, view: HTMLElement, params: Record<string, string>, tabContent: HTMLElement): void {
    let guideInstance: GuideInstance | null = null;

    this.renderTab = function (): void {
        if (!guideInstance) {
            guideInstance = createGuide({
                element: tabContent,
                serverId: ApiClient.serverId()
            });
        }
    };

    this.onShow = function (): void {
        if (guideInstance) {
            guideInstance.resume();
        }
    };

    this.onHide = function (): void {
        if (guideInstance) {
            guideInstance.pause();
        }
    };
}
