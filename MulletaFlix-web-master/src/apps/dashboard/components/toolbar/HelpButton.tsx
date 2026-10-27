import HelpOutline from '@mui/icons-material/HelpOutline';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip/Tooltip';
import React from 'react';
import { matchPath, useLocation } from 'react-router-dom';

import { HelpLinks } from 'apps/dashboard/constants/helpLinks';
import globalize from 'lib/globalize';

const HelpButton = () => {
    const { pathname } = useLocation();
    const helpLink = HelpLinks.find(({ paths }) => paths.some(path => matchPath(path, pathname)));

    if (!helpLink) return null;

    return (
        <Tooltip title={globalize.translate('Help')}>
            <IconButton
                href={helpLink.url}
                rel='noopener noreferrer'
                target='_blank'
                size='large'
                color='inherit'
            >
                <HelpOutline />
            </IconButton>
        </Tooltip>
    );
};

export default HelpButton;
