import Alert from '@mui/material/Alert/Alert';
import AlertTitle from '@mui/material/AlertTitle/AlertTitle';
import Box from '@mui/material/Box/Box';
import Button from '@mui/material/Button/Button';
import Paper from '@mui/material/Paper/Paper';
import Typography from '@mui/material/Typography/Typography';
import classNames from 'classnames';
import React, { type FC, useCallback, useEffect } from 'react';
import { useRouteError } from 'react-router-dom';

import loading from 'components/loading/loading';
import Page from 'components/Page';
import globalize from 'lib/globalize';

interface ErrorBoundaryParams {
    pageClasses?: string[]
}

const ErrorBoundary: FC<ErrorBoundaryParams> = ({
    pageClasses = [ 'libraryPage' ]
}) => {
    const routeError = useRouteError();
    let error: Error;
    if (routeError instanceof Error) {
        error = routeError;
    } else if (typeof routeError === 'string') {
        error = new Error(routeError);
    } else {
        error = new Error('Unknown route error');
    }

    const retry = useCallback(() => {
        window.location.reload();
    }, []);

    useEffect(() => {
        loading.hide();
    }, []);

    return (
        <Page
            id='errorBoundary'
            className={classNames('mainAnimatedPage', pageClasses)}
        >
            <Box className='content-primary'>
                <Alert
                    severity='error'
                    role='alert'
                    action={
                        <Button
                            color='inherit'
                            size='small'
                            onClick={retry}
                        >
                            {globalize.translate('Retry')}
                        </Button>
                    }
                >
                    <AlertTitle>
                        {error.name}
                    </AlertTitle>

                    <Typography>
                        {error.message}
                    </Typography>

                    {error.stack && (
                        <Paper
                            variant='outlined'
                            sx={{
                                marginTop: 1,
                                backgroundColor: 'transparent'
                            }}
                        >
                            <Box
                                component='pre'
                                sx={{
                                    overflow: 'auto',
                                    margin: 2,
                                    maxHeight: '25rem' // 20 lines
                                }}
                            >
                                {error.stack}
                            </Box>
                        </Paper>
                    )}
                </Alert>
            </Box>
        </Page>
    );
};

export default ErrorBoundary;
