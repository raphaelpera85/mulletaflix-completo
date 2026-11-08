import { describe, expect, it } from 'vitest';
import { applyNotificationBranding, MULLETAFLIX_NOTIFICATION_ICON } from './notificationBranding';

describe('notification branding', () => {
    it('forces the MulletaFlix logo for icon and badge, including media-cover overrides', () => {
        const options = {
            icon: '/media/poster.jpg',
            badge: '/jellyfin/notificationicon.png',
            data: { id: 'media-id' }
        };

        const brandedOptions = applyNotificationBranding(options);

        expect(brandedOptions).toBe(options);
        expect(brandedOptions.icon).toBe(MULLETAFLIX_NOTIFICATION_ICON);
        expect(brandedOptions.badge).toBe(MULLETAFLIX_NOTIFICATION_ICON);
        expect(MULLETAFLIX_NOTIFICATION_ICON).toContain('icon.png');
    });
});
