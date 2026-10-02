import { FunctionComponent, memo, useEffect } from 'react';

import loading from './loading';
import globalize from 'lib/globalize';

const Loading: FunctionComponent = () => {
    useEffect(() => {
        loading.show();

        return () => {
            loading.hide();
        };
    }, []);

    return (
        <span className='clipForScreenReader' role='status' aria-live='polite'>
            {globalize.translate('AccessibilityLoading')}
        </span>
    );
};

export default memo(Loading);
