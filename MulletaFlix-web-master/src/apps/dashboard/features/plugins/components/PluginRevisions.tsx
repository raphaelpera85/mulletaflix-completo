import type { VersionInfo } from '@jellyfin/sdk/lib/generated-client';
import Download from '@mui/icons-material/Download';
import DownloadDone from '@mui/icons-material/DownloadDone';
import ExpandMore from '@mui/icons-material/ExpandMore';
import WarningIcon from '@mui/icons-material/Warning';
import CheckCircleIcon from '@mui/icons-material/CheckCircle';
import HelpIcon from '@mui/icons-material/Help';
import Accordion from '@mui/material/Accordion/Accordion';
import AccordionDetails from '@mui/material/AccordionDetails/AccordionDetails';
import AccordionSummary from '@mui/material/AccordionSummary/AccordionSummary';
import Button from '@mui/material/Button/Button';
import Chip from '@mui/material/Chip';
import Stack from '@mui/material/Stack/Stack';
import Tooltip from '@mui/material/Tooltip';
import Typography from '@mui/material/Typography';
import React, { type FC } from 'react';

import MarkdownBox from 'components/MarkdownBox';
import { getDisplayDateTime } from 'scripts/datetime';
import globalize from 'lib/globalize';
import { checkPluginCompatibility, formatTimestamp, getCompatibilityStatus } from '../utils/compatibility';

import type { PluginDetails } from '../types/PluginDetails';

// Extended VersionInfo with additional fields from backend
interface ExtendedVersionInfo extends VersionInfo {
    dependencies?: string[];
    targetAbi?: string;
}

interface PluginRevisionsProps {
    pluginDetails?: PluginDetails,
    onInstall: (version?: ExtendedVersionInfo) => () => void
}

const PluginRevisions: FC<PluginRevisionsProps> = ({
    pluginDetails,
    onInstall
}) => {
    if (!pluginDetails?.versions?.length) {
        return <Typography>{globalize.translate('LabelNoVersionsAvailable')}</Typography>;
    }

    return (
        <>
            {pluginDetails.versions.map(version => {
                const extVersion = version as ExtendedVersionInfo;
                const compatibility = getCompatibilityStatus(extVersion, pluginDetails.targetAbi);
                const isInstalled = pluginDetails.status && version.version === pluginDetails.version?.version;

                return (
                    <Accordion key={version.checksum || version.version}>
                        <AccordionSummary expandIcon={<ExpandMore />}>
                            <Stack direction="row" spacing={1} alignItems="center">
                                <Typography variant="subtitle1">{version.version}</Typography>
                                {version.timestamp && (
                                    <Typography variant="body2" color="text.secondary">
                                        &mdash; {getDisplayDateTime(version.timestamp)}
                                    </Typography>
                                )}
                                <Chip
                                    size="small"
                                    label={compatibility.label}
                                    color={compatibility.color}
                                    icon={
                                        compatibility.status === 'compatible' ? <CheckCircleIcon fontSize="small" /> :
                                        compatibility.status === 'incompatible' ? <WarningIcon fontSize="small" /> :
                                        <HelpIcon fontSize="small" />
                                    }
                                />
                            </Stack>
                        </AccordionSummary>
                        <AccordionDetails>
                            <Stack spacing={2}>
                                {extVersion.targetAbi && (
                                    <Tooltip title={globalize.translate('LabelTargetAbi')}>
                                        <Typography variant="body2" color="text.secondary">
                                            <strong>Target ABI:</strong> {extVersion.targetAbi}
                                        </Typography>
                                    </Tooltip>
                                )}
                                {extVersion.dependencies?.length && (
                                    <Tooltip title={globalize.translate('LabelDependencies')}>
                                        <Typography variant="body2" color="text.secondary">
                                            <strong>Dependências:</strong> {extVersion.dependencies.length} plugin(s)
                                        </Typography>
                                    </Tooltip>
                                )}
                                <MarkdownBox
                                    fallback={globalize.translate('LabelNoChangelog')}
                                    markdown={version.changelog}
                                />
                                {isInstalled ? (
                                    <Button disabled startIcon={<DownloadDone />} variant="outlined">
                                        {globalize.translate('LabelInstalled')}
                                    </Button>
                                ) : (
                                    <Button startIcon={<Download />} variant="outlined" onClick={onInstall(extVersion)}>
                                        {globalize.translate('HeaderInstall')}
                                    </Button>
                                )}
                            </Stack>
                        </AccordionDetails>
                    </Accordion>
                );
            })}
        </>
    );
};

export default PluginRevisions;

