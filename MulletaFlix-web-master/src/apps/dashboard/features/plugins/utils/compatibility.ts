import type { VersionInfo } from '@jellyfin/sdk/lib/generated-client';
import { useSystemInfo } from 'hooks/useSystemInfo';

/**
 * Checks if a plugin version is compatible with the current server
 * @param pluginVersion The plugin version info
 * @param serverTargetAbi The server's target ABI version (e.g., "12.0.0")
 * @returns true if compatible, false if incompatible, null if unknown
 */
export const checkPluginCompatibility = (
    pluginVersion: VersionInfo | undefined,
    serverTargetAbi: string | undefined
): boolean | null => {
    if (!pluginVersion?.targetAbi || !serverTargetAbi) {
        return null; // Unknown compatibility
    }

    try {
        const pluginAbi = parseVersion(pluginVersion.targetAbi);
        const serverAbi = parseVersion(serverTargetAbi);

        // Plugin is compatible if its target ABI <= server ABI
        return compareVersions(pluginAbi, serverAbi) <= 0;
    } catch {
        return null; // Parse error - unknown
    }
};

/**
 * Hook to get the server's target ABI from system info
 */
export const useServerTargetAbi = (): string | undefined => {
    const { data: systemInfo } = useSystemInfo();
    // PublicSystemInfo from SDK may not have TargetAbi yet (needs regen), fallback to Version
    return (systemInfo as any)?.TargetAbi || systemInfo?.Version;
};

/**
 * Parses a version string into comparable parts
 */
const parseVersion = (version: string): number[] => {
    return version.split('.').map(part => {
        const num = parseInt(part, 10);
        return isNaN(num) ? 0 : num;
    });
};

/**
 * Compares two version arrays
 * Returns -1 if a < b, 0 if a === b, 1 if a > b
 */
const compareVersions = (a: number[], b: number[]): number => {
    const maxLen = Math.max(a.length, b.length);
    for (let i = 0; i < maxLen; i++) {
        const aVal = a[i] || 0;
        const bVal = b[i] || 0;
        if (aVal < bVal) return -1;
        if (aVal > bVal) return 1;
    }
    return 0;
};

/**
 * Gets a human-readable compatibility status
 */
export const getCompatibilityStatus = (
    pluginVersion: VersionInfo | undefined,
    serverTargetAbi: string | undefined
): { status: 'compatible' | 'incompatible' | 'unknown'; label: string; color: 'success' | 'error' | 'default' } => {
    const isCompatible = checkPluginCompatibility(pluginVersion, serverTargetAbi);

    if (isCompatible === null) {
        return {
            status: 'unknown',
            label: 'Compatibilidade desconhecida',
            color: 'default'
        };
    }

    if (isCompatible) {
        return {
            status: 'compatible',
            label: 'Compatível',
            color: 'success'
        };
    }

    return {
        status: 'incompatible',
        label: 'Incompatível',
        color: 'error'
    };
};

/**
 * Formats version for display
 */
export const formatVersion = (version?: VersionInfo): string => {
    if (!version?.version) return 'Desconhecida';
    return `v${version.version}`;
};

/**
 * Formats timestamp for display
 */
export const formatTimestamp = (timestamp?: string): string => {
    if (!timestamp) return 'Data desconhecida';
    try {
        return new Date(timestamp).toLocaleDateString('pt-BR', {
            year: 'numeric',
            month: 'short',
            day: 'numeric'
        });
    } catch {
        return timestamp;
    }
};