import AppNotificationIcon from '../../assets/branding/icon.png';

export interface NotificationBrandingOptions {
    icon?: string;
    badge?: string;
}

export const MULLETAFLIX_NOTIFICATION_ICON = AppNotificationIcon;

/** Apply the product identity consistently, even when a caller supplies another icon. */
export function applyNotificationBranding<T extends NotificationBrandingOptions>(options: T): T {
    options.icon = MULLETAFLIX_NOTIFICATION_ICON;
    options.badge = MULLETAFLIX_NOTIFICATION_ICON;
    return options;
}
