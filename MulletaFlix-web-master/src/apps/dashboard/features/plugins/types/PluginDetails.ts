import type { ConfigurationPageInfo, PluginStatus, VersionInfo } from '@jellyfin/sdk/lib/generated-client';

// Extended VersionInfo with additional fields from backend
export interface ExtendedVersionInfo extends VersionInfo {
    dependencies?: string[];
    targetAbi?: string;
}

export interface PluginDetails {
    canUninstall: boolean
    category?: string
    description?: string
    id: string
    imageUrl?: string
    isEnabled: boolean
    name?: string
    owner?: string
    configurationPage?: ConfigurationPageInfo
    status?: PluginStatus
    version?: ExtendedVersionInfo
    versions: VersionInfo[]
    targetAbi?: string
}

