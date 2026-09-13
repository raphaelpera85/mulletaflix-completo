import type { CollectionType } from '@jellyfin/sdk/lib/generated-client/models/collection-type';
import type { UserItemDataDto } from '@jellyfin/sdk/lib/generated-client/models/user-item-data-dto';

import { ItemAction } from 'constants/itemAction';

import type { NullableBoolean, NullableNumber, NullableString } from './base/common/shared/types';

export type AttributesOpts = {
    context?: CollectionType | string,
    parentId?: Exclude<NullableString, undefined>,
    collectionId?: Exclude<NullableString, undefined>,
    playlistId?: Exclude<NullableString, undefined>,
    prefix?: Exclude<NullableString, undefined>,
    action?: ItemAction | null,
    itemServerId?: Exclude<NullableString, undefined>,
    itemId?: Exclude<NullableString, undefined>,
    itemTimerId?: Exclude<NullableString, undefined>,
    itemSeriesTimerId?: Exclude<NullableString, undefined>,
    itemChannelId?: Exclude<NullableString, undefined>,
    itemPlaylistItemId?: Exclude<NullableString, undefined>,
    itemType?: Exclude<NullableString, undefined>,
    itemMediaType?: Exclude<NullableString, undefined>,
    itemCollectionType?: Exclude<NullableString, undefined>,
    itemIsFolder?: Exclude<NullableBoolean, undefined>,
    itemPath?: Exclude<NullableString, undefined>,
    itemStartDate?: Exclude<NullableString, undefined>,
    itemEndDate?: Exclude<NullableString, undefined>,
    itemUserData?: UserItemDataDto
};

export type DataAttributes = {
    'data-playlistitemid'?: Exclude<NullableString, undefined>;
    'data-timerid'?: Exclude<NullableString, undefined>;
    'data-seriestimerid'?: Exclude<NullableString, undefined>;
    'data-serverid'?: Exclude<NullableString, undefined>;
    'data-id'?: Exclude<NullableString, undefined>;
    'data-type'?: Exclude<NullableString, undefined>;
    'data-collectionid'?: Exclude<NullableString, undefined>;
    'data-playlistid'?: Exclude<NullableString, undefined>;
    'data-mediatype'?: Exclude<NullableString, undefined>;
    'data-channelid'?: Exclude<NullableString, undefined>;
    'data-path'?: Exclude<NullableString, undefined>;
    'data-collectiontype'?: Exclude<NullableString, undefined>;
    'data-context'?: Exclude<NullableString, undefined>;
    'data-parentid'?: Exclude<NullableString, undefined>;
    'data-startdate'?: Exclude<NullableString, undefined>;
    'data-enddate'?: Exclude<NullableString, undefined>;
    'data-prefix'?: Exclude<NullableString, undefined>;
    'data-action'?: ItemAction | null;
    'data-positionticks'?: Exclude<NullableNumber, undefined>;
    'data-isfolder'?: Exclude<NullableBoolean, undefined>;
};
