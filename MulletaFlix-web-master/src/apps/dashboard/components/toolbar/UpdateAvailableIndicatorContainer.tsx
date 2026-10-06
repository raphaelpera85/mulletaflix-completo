import React from 'react';

import { useServerUpdateInfo } from 'apps/dashboard/features/updates/api/useServerUpdateInfo';

import UpdateAvailableIndicator from './UpdateAvailableIndicator';

/**
 * Connects UpdateAvailableIndicator to the existing /System/Update/Status
 * backend endpoint (via useServerUpdateInfo). Kept separate from the
 * presentational component so the indicator itself stays trivially testable.
 */
const UpdateAvailableIndicatorContainer = () => {
    const { data: updateInfo } = useServerUpdateInfo();

    return (
        <UpdateAvailableIndicator
            updateAvailable={Boolean(updateInfo?.UpdateAvailable)}
            availableVersion={updateInfo?.AvailableVersion}
        />
    );
};

export default UpdateAvailableIndicatorContainer;
