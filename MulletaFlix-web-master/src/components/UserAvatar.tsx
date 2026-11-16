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
    const accessToken = __legacyApiClient__?.accessToken();
    const authenticatedImageUrl = imageUrl && accessToken ? new URL(imageUrl, window.location.href) : undefined;

    if (authenticatedImageUrl && accessToken) {
        authenticatedImageUrl.searchParams.set('api_key', accessToken);
    }
    const src = authenticatedImageUrl?.href ?? imageUrl;

    return user ? (
        <Avatar
            alt={user.Name ?? undefined}
            src={src}
            // eslint-disable-next-line react/jsx-no-bind
            sx={(theme) => ({
                bgcolor: src ?
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

