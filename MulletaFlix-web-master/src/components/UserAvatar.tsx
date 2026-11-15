import React, { type FC } from 'react';
import type { UserDto } from '@jellyfin/sdk/lib/generated-client/models/user-dto';
import Avatar, { type AvatarProps } from '@mui/material/Avatar';
import type {} from '@mui/material/themeCssVarsAugmentation';

import { useApi } from 'hooks/useApi';

interface UserAvatarProps extends AvatarProps {
    user?: UserDto,
    size?: number
}

const UserAvatar: FC<UserAvatarProps> = ({
    user,
    size
}) => {
    const { __legacyApiClient__ } = useApi();
    const imageUrl = __legacyApiClient__ && user?.Id && user.PrimaryImageTag ?
        __legacyApiClient__.getUserImageUrl(user.Id, {
            tag: user.PrimaryImageTag,
            type: 'Primary'
        }) : undefined;

    return user ? (
        <Avatar
            alt={user.Name ?? undefined}
            src={imageUrl}
            // eslint-disable-next-line react/jsx-no-bind
            sx={(theme) => ({
                bgcolor: imageUrl ?
                    theme.vars.palette.background.paper :
                    theme.vars.palette.primary.dark,
                color: 'inherit',
                width: size,
                height: size
            })}
        />
    ) : null;
};

export default UserAvatar;

