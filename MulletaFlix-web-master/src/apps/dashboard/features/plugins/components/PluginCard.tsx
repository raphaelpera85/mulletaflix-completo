import ExtensionIcon from '@mui/icons-material/Extension';
import UpdateIcon from '@mui/icons-material/Update';
import InfoIcon from '@mui/icons-material/Info';
import WarningIcon from '@mui/icons-material/Warning';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import React, { useMemo, useState } from 'react';
import { useLocation } from 'react-router-dom';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import Menu from '@mui/material/Menu';
import MenuItem from '@mui/material/MenuItem';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Chip from '@mui/material/Chip';
import Typography from '@mui/material/Typography';
import Box from '@mui/material/Box';
import Stack from '@mui/material/Stack';
import Divider from '@mui/material/Divider';

import BaseCard from 'apps/dashboard/components/BaseCard';
import { PluginDetails } from '../types/PluginDetails';
import { useEnablePlugin } from '../api/useEnablePlugin';
import { useDisablePlugin } from '../api/useDisablePlugin';
import { useUninstallPlugin } from '../api/useUninstallPlugin';
import { useInstallPackage } from '../api/useInstallPackage';
import type { PackageApiInstallPackageRequest } from '@jellyfin/sdk/lib/generated-client/api/package-api';
import { useApi } from 'hooks/useApi';
import { checkPluginCompatibility, getCompatibilityStatus } from '../utils/compatibility';
import globalize from 'lib/globalize';

interface PluginCardProps {
    plugin: PluginDetails;
}

const PluginCard = ({ plugin }: PluginCardProps) => {
    const location = useLocation();
    const { api } = useApi();
    const enablePlugin = useEnablePlugin();
    const disablePlugin = useDisablePlugin();
    const uninstallPlugin = useUninstallPlugin();
    const installPackage = useInstallPackage();

    const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
    const [expandedVersion, setExpandedVersion] = useState<string | null>(null);

    const pluginPage = useMemo(() => ({
        pathname: `/dashboard/plugins/${plugin.id}`,
        search: `?name=${encodeURIComponent(plugin.name || '')}`,
        hash: location.hash
    }), [location, plugin]);

    const isInstalled = !!plugin.status;
    const hasUpdate = plugin.versions && plugin.versions.length > 1 && plugin.version && 
        plugin.versions.some(v => v.VersionNumber && v.VersionNumber !== plugin.version?.VersionNumber);

    const handleMenuOpen = (event: React.MouseEvent<HTMLElement>) => {
        setAnchorEl(event.currentTarget);
    };

    const handleMenuClose = () => {
        setAnchorEl(null);
    };

    const handleEnable = () => {
        if (plugin.version?.VersionNumber) {
            enablePlugin.mutate({ pluginId: plugin.id, version: plugin.version.VersionNumber });
            handleMenuClose();
        }
    };

    const handleDisable = () => {
        if (plugin.version?.VersionNumber) {
            disablePlugin.mutate({ pluginId: plugin.id, version: plugin.version.VersionNumber });
            handleMenuClose();
        }
    };

    const handleUninstall = () => {
        if (plugin.version?.VersionNumber) {
            uninstallPlugin.mutate({ pluginId: plugin.id, version: plugin.version.VersionNumber });
            handleMenuClose();
        }
    };

    const handleInstall = (version: { VersionNumber?: string; version?: string }) => {
        const installVersion: string = version.VersionNumber || version.version || '';
        const params: PackageApiInstallPackageRequest = {
            name: plugin.name || '',
            assemblyGuid: plugin.id,
            version: installVersion
        };
        installPackage.mutate(params);
        handleMenuClose();
    };

    const getStatusChip = () => {
        // Check compatibility for uninstalled plugins
        const compatibility = plugin.versions?.[0] ? getCompatibilityStatus(plugin.versions[0], plugin.targetAbi) : null;

        if (isInstalled) {
            if (plugin.isEnabled) {
                return <Chip label={globalize.translate('LabelInstalled')} color="success" variant="outlined" icon={<CheckCircleIcon fontSize="small" />} size="small" />;
            } else {
                return <Chip label={globalize.translate('LabelDisabled')} color="default" variant="outlined" size="small" />;
            }
        } else if (hasUpdate) {
            return <Chip label={globalize.translate('LabelUpdateAvailable')} color="warning" variant="outlined" icon={<UpdateIcon fontSize="small" />} size="small" />;
        } else if (compatibility && compatibility.status !== 'unknown') {
            return <Chip label={compatibility.label} color={compatibility.color} variant="outlined" size="small" />;
        } else {
            return <Chip label={globalize.translate('LabelAvailable')} color="primary" variant="outlined" size="small" />;
        }
    };

    return (
        <>
            <BaseCard
                title={plugin.name}
                to={pluginPage}
                text={
                    <Stack direction="row" spacing={1} style={{ flexWrap: 'wrap' }}>
                        {getStatusChip()}
                        {plugin.category && (
                            <Chip
                                label={globalize.translate(`PluginCategory${plugin.category}`) || plugin.category}
                                size="small"
                                variant="outlined"
                            />
                        )}
                        {plugin.version && plugin.version.VersionNumber && (
                            <Chip
                                label={`v${plugin.version.VersionNumber}`}
                                size="small"
                                variant="outlined"
                            />
                        )}
                    </Stack>
                }
                image={plugin.imageUrl}
                icon={<ExtensionIcon sx={{ width: 80, height: 80 }} />}
            />
            
            <Menu
                anchorEl={anchorEl}
                open={Boolean(anchorEl)}
                onClose={handleMenuClose}
                transformOrigin={{ horizontal: 'right', vertical: 'top' }}
                anchorOrigin={{ horizontal: 'right', vertical: 'bottom' }}
            >
                {isInstalled && plugin.isEnabled && (
                    <MenuItem onClick={handleDisable}>
                        <ListItemIcon><ExtensionIcon fontSize="small" /></ListItemIcon>
                        <ListItemText>{globalize.translate('LabelDisable')}</ListItemText>
                    </MenuItem>
                )}
                {isInstalled && !plugin.isEnabled && (
                    <MenuItem onClick={handleEnable}>
                        <ListItemIcon><CheckCircleIcon fontSize="small" /></ListItemIcon>
                        <ListItemText>{globalize.translate('LabelEnable')}</ListItemText>
                    </MenuItem>
                )}
                {isInstalled && plugin.canUninstall && (
                    <MenuItem onClick={handleUninstall}>
                        <ListItemIcon><ExtensionIcon fontSize="small" /></ListItemIcon>
                        <ListItemText>{globalize.translate('LabelUninstall')}</ListItemText>
                    </MenuItem>
                )}
                {!isInstalled && plugin.versions && (
                    <>
                        <Divider />
                        {plugin.versions.map(v => (
                            <MenuItem key={v.VersionNumber} onClick={() => handleInstall(v)}>
                                <ListItemIcon><UpdateIcon fontSize="small" /></ListItemIcon>
                                <ListItemText>{globalize.translate('LabelInstall')} v{v.VersionNumber}</ListItemText>
                            </MenuItem>
                        ))}
                    </>
                )}
            </Menu>
            
            <IconButton
                size="small"
                onClick={handleMenuOpen}
                aria-label={globalize.translate('LabelMoreActions')}
            >
                <ExtensionIcon />
            </IconButton>
        </>
    );
};

export default PluginCard;