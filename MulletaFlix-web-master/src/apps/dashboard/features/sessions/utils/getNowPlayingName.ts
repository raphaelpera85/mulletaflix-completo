import type { SessionInfo } from '@jellyfin/sdk/lib/generated-client/models/session-info';
import { ImageType } from '@jellyfin/sdk/lib/generated-client/models/image-type';
import type { ApiClient } from 'jellyfin-apiclient';
import itemHelper from 'components/itemHelper';
import formatDistanceToNow from 'date-fns/formatDistanceToNow';
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import { getLocaleWithSuffix } from 'utils/dateFnsLocale';

type NowPlayingInfo = {
    topText?: string;
    bottomText: string;
    image?: string;
};

const getNowPlayingName = (session: SessionInfo): NowPlayingInfo => {
    let imgUrl = '';
    const nowPlayingItem = session.NowPlayingItem;
    // FIXME: It seems that, sometimes, server sends date in the future, so date-fns displays messages like 'in less than a minute'. We should fix
    // how dates are returned by the server when the session is active and show something like 'Active now', instead of past/future sentences
    if (!nowPlayingItem) {
        const lastActivityTime = Date.parse(session.LastActivityDate!);
        const safeDate = new Date(Math.min(Date.now(), lastActivityTime));
        return {
            bottomText: globalize.translate('LastSeen', formatDistanceToNow(safeDate, getLocaleWithSuffix()))
        };
    }

    let topText = itemHelper.getDisplayName(nowPlayingItem);
    let bottomText = '';

    if (nowPlayingItem.Artists?.length) {
        bottomText = topText;
        topText = nowPlayingItem.Artists[0] || '';
    } else if (nowPlayingItem.SeriesName || nowPlayingItem.Album) {
        bottomText = topText;
        topText = nowPlayingItem.SeriesName || nowPlayingItem.Album || '';
    } else if (nowPlayingItem.ProductionYear) {
        bottomText = nowPlayingItem.ProductionYear.toString();
    }

    const apiClient = session.ServerId ?
        ServerConnections.getApiClient(session.ServerId) as unknown as ApiClient :
        null;

    if (apiClient && nowPlayingItem.Id && nowPlayingItem.ImageTags?.Logo) {
        imgUrl = apiClient.getScaledImageUrl(nowPlayingItem.Id, {
            tag: nowPlayingItem.ImageTags.Logo,
            maxHeight: 24,
            maxWidth: 130,
            type: ImageType.Logo
        });
    } else if (apiClient && nowPlayingItem.ParentLogoItemId && nowPlayingItem.ParentLogoImageTag) {
        imgUrl = apiClient.getScaledImageUrl(nowPlayingItem.ParentLogoItemId, {
            tag: nowPlayingItem.ParentLogoImageTag,
            maxHeight: 24,
            maxWidth: 130,
            type: ImageType.Logo
        });
    }

    return {
        topText: topText,
        bottomText: bottomText,
        image: imgUrl
    };
};

export default getNowPlayingName;
