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
    const self = this;

    self.renderTab = function (): void {
        if (!guideInstance) {
            guideInstance = createGuide({
                element: tabContent,
                serverId: ApiClient.serverId()
            });
        }
    };

    self.onShow = function (): void {
        if (guideInstance) {
            guideInstance.resume();
        }
    };

    self.onHide = function (): void {
        if (guideInstance) {
            guideInstance.pause();
        }
    };
}
