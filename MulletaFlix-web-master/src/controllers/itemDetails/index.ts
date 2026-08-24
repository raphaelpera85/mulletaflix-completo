import escapeHtml from 'escape-html';

import autoFocuser from 'components/autoFocuser';
import { clearBackdrop, setBackdrops } from 'components/backdrop/backdrop';
import cardBuilder from 'components/cardbuilder/cardBuilder';
import peoplecardbuilder from 'components/cardbuilder/peoplecardbuilder';
import { getBackdropShape, getPortraitShape } from 'components/cardbuilder/utils/shape';
import itemContextMenu from 'components/itemContextMenu';
import itemHelper from 'components/itemHelper';
import layoutManager from 'components/layoutManager';
import loading from 'components/loading/loading';
import mediainfo from 'components/mediainfo/mediainfo';
import { playbackManager } from 'components/playback/playbackmanager';
import globalize from 'lib/globalize';
import { ServerConnections } from 'lib/jellyfin-apiclient';
import datetime from 'scripts/datetime';
import libraryMenu from 'scripts/libraryMenu';
import dom from 'utils/dom';

import 'elements/emby-button/emby-button';
import 'elements/emby-itemscontainer/emby-itemscontainer';
import 'elements/emby-playstatebutton/emby-playstatebutton';
import 'elements/emby-ratingbutton/emby-ratingbutton';
import 'elements/emby-scroller/emby-scroller';
import 'elements/emby-select/emby-select';

declare const ApiClient: any;

interface ViewParams {
    id?: string;
    itemId?: string;
    seriesTimerId?: string;
    serverId?: string;
    context?: string;
    [key: string]: any;
}

function enableScrollX(): boolean {
    return !layoutManager.desktop;
}

function renderPoster(view: HTMLElement, item: any, apiClient: any): void {
    const containers = view.querySelectorAll<HTMLElement>('.detailImageContainer');
    let imgUrl: string | null = null;

    if (item.ImageTags && item.ImageTags.Primary) {
        imgUrl = apiClient.getImageUrl(item.Id, {
            type: 'Primary',
            maxWidth: 600,
            tag: item.ImageTags.Primary
        });
    } else if (item.Type === 'Episode' && item.SeriesPrimaryImageTag) {
        imgUrl = apiClient.getImageUrl(item.SeriesId, {
            type: 'Primary',
            maxWidth: 600,
            tag: item.SeriesPrimaryImageTag
        });
    } else if (item.AlbumId && item.AlbumPrimaryImageTag) {
        imgUrl = apiClient.getImageUrl(item.AlbumId, {
            type: 'Primary',
            maxWidth: 600,
            tag: item.AlbumPrimaryImageTag
        });
    } else if (item.PrimaryImageTag) {
        imgUrl = apiClient.getImageUrl(item.Id, {
            type: 'Primary',
            maxWidth: 600,
            tag: item.PrimaryImageTag
        });
    }

    containers.forEach(container => {
        if (imgUrl) {
            container.innerHTML = `<img class="itemDetailImage" src="${imgUrl}" alt="${escapeHtml(item.Name || '')}" />`;
            container.classList.remove('hide');
        } else {
            container.innerHTML = '';
            container.classList.add('hide');
        }
    });
}

function renderLogo(view: HTMLElement, item: any, apiClient: any): void {
    const logoContainer = view.querySelector<HTMLElement>('.detailLogo');
    if (!logoContainer) return;

    if (item.ImageTags && item.ImageTags.Logo) {
        const logoUrl = apiClient.getImageUrl(item.Id, {
            type: 'Logo',
            maxHeight: 120,
            tag: item.ImageTags.Logo
        });
        logoContainer.innerHTML = `<img src="${logoUrl}" alt="${escapeHtml(item.Name || '')}" />`;
        logoContainer.classList.remove('hide');
    } else if (item.ParentLogoImageTag && item.ParentLogoItemId) {
        const logoUrl = apiClient.getImageUrl(item.ParentLogoItemId, {
            type: 'Logo',
            maxHeight: 120,
            tag: item.ParentLogoImageTag
        });
        logoContainer.innerHTML = `<img src="${logoUrl}" alt="${escapeHtml(item.Name || '')}" />`;
        logoContainer.classList.remove('hide');
    } else {
        logoContainer.innerHTML = '';
        logoContainer.classList.add('hide');
    }
}

function renderName(view: HTMLElement, item: any): void {
    const nameContainer = view.querySelector<HTMLElement>('.nameContainer');
    if (!nameContainer) return;

    let html = '';

    if (item.Type === 'Episode') {
        if (item.SeriesName && item.SeriesId) {
            html += `<h2 class="parentNameContainer" style="margin:0 0 0.3em;"><a class="button-link" is="emby-linkbutton" href="#/details?id=${item.SeriesId}&serverId=${item.ServerId || ''}">${escapeHtml(item.SeriesName)}</a></h2>`;
        }
        html += `<h1 class="itemName" style="margin:0;">${escapeHtml(itemHelper.getDisplayName(item))}</h1>`;
    } else {
        html += `<h1 class="itemName" style="margin:0;">${escapeHtml(item.Name || '')}</h1>`;
        if (item.OriginalTitle && item.OriginalTitle !== item.Name) {
            html += `<div class="originalTitle secondaryText" style="margin-top:0.2em; font-size:90%;">${escapeHtml(item.OriginalTitle)}</div>`;
        }
    }

    nameContainer.innerHTML = html;
}

function renderMetadata(view: HTMLElement, item: any): void {
    const miscInfoPrimary = view.querySelector<HTMLElement>('.itemMiscInfo-primary');
    if (miscInfoPrimary) {
        mediainfo.fillPrimaryMediaInfo(miscInfoPrimary, item, { interactive: true });
    }

    const miscInfoSecondary = view.querySelector<HTMLElement>('.itemMiscInfo-secondary');
    if (miscInfoSecondary) {
        mediainfo.fillSecondaryMediaInfo(miscInfoSecondary, item, { interactive: true });
    }

    // Tagline
    const taglineEl = view.querySelector<HTMLElement>('.tagline');
    if (taglineEl) {
        if (item.Taglines && item.Taglines.length) {
            taglineEl.textContent = item.Taglines[0];
            taglineEl.classList.remove('hide');
        } else {
            taglineEl.textContent = '';
            taglineEl.classList.add('hide');
        }
    }

    // Overview
    const overviewEl = view.querySelector<HTMLElement>('.overview');
    const overviewExpandEl = view.querySelector<HTMLElement>('.overview-expand');
    if (overviewEl) {
        if (item.Overview) {
            overviewEl.innerHTML = escapeHtml(item.Overview);
            overviewEl.classList.remove('hide');
            if (overviewExpandEl && item.Overview.length > 300) {
                overviewExpandEl.classList.remove('hide');
                overviewExpandEl.onclick = (e) => {
                    e.preventDefault();
                    overviewEl.classList.toggle('overview-expanded');
                    overviewExpandEl.textContent = overviewEl.classList.contains('overview-expanded')
                        ? globalize.translate('ShowLess') || 'Show Less'
                        : globalize.translate('ShowMore') || 'Show More';
                };
            } else if (overviewExpandEl) {
                overviewExpandEl.classList.add('hide');
            }
        } else {
            overviewEl.innerHTML = '';
            overviewEl.classList.add('hide');
            if (overviewExpandEl) overviewExpandEl.classList.add('hide');
        }
    }

    // Genres
    const genresEl = view.querySelector<HTMLElement>('.itemGenres');
    if (genresEl) {
        if (item.Genres && item.Genres.length) {
            genresEl.innerHTML = item.Genres.map((g: string) => `<span class="genreItem">${escapeHtml(g)}</span>`).join(' • ');
            genresEl.classList.remove('hide');
        } else {
            genresEl.innerHTML = '';
            genresEl.classList.add('hide');
        }
    }

    // Person Info
    const birthdayEl = view.querySelector<HTMLElement>('#itemBirthday');
    if (birthdayEl) {
        if (item.PremiereDate) {
            const parsed = datetime.parseISO8601Date(item.PremiereDate);
            birthdayEl.textContent = globalize.translate('BirthDateValue', datetime.toLocaleDateString(parsed));
            birthdayEl.classList.remove('hide');
        } else {
            birthdayEl.classList.add('hide');
        }
    }

    const birthLocationEl = view.querySelector<HTMLElement>('#itemBirthLocation');
    if (birthLocationEl) {
        if (item.ProductionLocations && item.ProductionLocations.length) {
            birthLocationEl.textContent = globalize.translate('BirthPlaceValue', item.ProductionLocations[0]);
            birthLocationEl.classList.remove('hide');
        } else {
            birthLocationEl.classList.add('hide');
        }
    }

    const deathDateEl = view.querySelector<HTMLElement>('#itemDeathDate');
    if (deathDateEl) {
        if (item.EndDate) {
            const parsed = datetime.parseISO8601Date(item.EndDate);
            deathDateEl.textContent = globalize.translate('DeathDateValue', datetime.toLocaleDateString(parsed));
            deathDateEl.classList.remove('hide');
        } else {
            deathDateEl.classList.add('hide');
        }
    }

    // Series air time
    const airTimeEl = view.querySelector<HTMLElement>('#seriesAirTime');
    if (airTimeEl) {
        if (item.AirDays && item.AirDays.length) {
            const text = item.AirDays.join(', ') + (item.AirTime ? ` ${item.AirTime}` : '');
            airTimeEl.textContent = text;
            airTimeEl.classList.remove('hide');
        } else {
            airTimeEl.classList.add('hide');
        }
    }

    // Details Group (Directors, Writers, Studios)
    const detailsGroup = view.querySelector<HTMLElement>('.itemDetailsGroup');
    if (detailsGroup) {
        let groupHtml = '';
        if (item.People && item.People.length) {
            const directors = item.People.filter((p: any) => p.Type === 'Director');
            if (directors.length) {
                groupHtml += `<div class="detailsGroupItem"><span class="label font-bold" style="font-weight:bold;">${escapeHtml(globalize.translate(directors.length > 1 ? 'Directors' : 'Director'))}:</span> <span class="value">${directors.map((p: any) => escapeHtml(p.Name)).join(', ')}</span></div>`;
            }
            const writers = item.People.filter((p: any) => p.Type === 'Writer');
            if (writers.length) {
                groupHtml += `<div class="detailsGroupItem"><span class="label font-bold" style="font-weight:bold;">${escapeHtml(globalize.translate(writers.length > 1 ? 'Writers' : 'Writer'))}:</span> <span class="value">${writers.map((p: any) => escapeHtml(p.Name)).join(', ')}</span></div>`;
            }
        }
        if (item.Studios && item.Studios.length) {
            groupHtml += `<div class="detailsGroupItem"><span class="label font-bold" style="font-weight:bold;">${escapeHtml(globalize.translate(item.Studios.length > 1 ? 'Studios' : 'Studio'))}:</span> <span class="value">${item.Studios.map((s: any) => escapeHtml(s.Name)).join(', ')}</span></div>`;
        }
        detailsGroup.innerHTML = groupHtml;
    }
}

function setupTrackSelections(view: HTMLElement, item: any): void {
    const trackSelectionsForm = view.querySelector<HTMLFormElement>('.trackSelections');
    if (!trackSelectionsForm) return;

    const sources = item.MediaSources || [];
    if (!sources.length) {
        trackSelectionsForm.classList.add('hide');
        return;
    }

    trackSelectionsForm.classList.remove('hide');

    const selectSource = view.querySelector<HTMLSelectElement>('.selectSource');
    const selectSourceContainer = view.querySelector<HTMLElement>('.selectSourceContainer');
    const selectAudio = view.querySelector<HTMLSelectElement>('.selectAudio');
    const selectAudioContainer = view.querySelector<HTMLElement>('.selectAudioContainer');
    const selectSubtitles = view.querySelector<HTMLSelectElement>('.selectSubtitles');
    const selectSubtitlesContainer = view.querySelector<HTMLElement>('.selectSubtitlesContainer');

    if (selectSource && selectSourceContainer) {
        if (sources.length > 1) {
            selectSourceContainer.classList.remove('hide');
            selectSource.innerHTML = sources.map((s: any, index: number) =>
                `<option value="${s.Id || index}">${escapeHtml(s.Name || s.Path || `Source ${index + 1}`)}</option>`
            ).join('');
        } else {
            selectSourceContainer.classList.add('hide');
        }
    }

    const currentSource = sources[0] || {};
    const streams = currentSource.MediaStreams || [];

    if (selectAudio && selectAudioContainer) {
        const audioStreams = streams.filter((s: any) => s.Type === 'Audio');
        if (audioStreams.length > 0) {
            selectAudioContainer.classList.remove('hide');
            selectAudio.innerHTML = audioStreams.map((s: any) => {
                const label = s.DisplayTitle || s.Title || s.Language || `Audio (${s.Codec || ''})`;
                const selected = s.Index === currentSource.DefaultAudioStreamIndex ? 'selected' : '';
                return `<option value="${s.Index}" ${selected}>${escapeHtml(label)}</option>`;
            }).join('');
        } else {
            selectAudioContainer.classList.add('hide');
        }
    }

    if (selectSubtitles && selectSubtitlesContainer) {
        const subStreams = streams.filter((s: any) => s.Type === 'Subtitle');
        if (subStreams.length > 0) {
            selectSubtitlesContainer.classList.remove('hide');
            let subHtml = `<option value="-1">${escapeHtml(globalize.translate('Off'))}</option>`;
            subHtml += subStreams.map((s: any) => {
                const label = s.DisplayTitle || s.Title || s.Language || `Subtitle (${s.Codec || ''})`;
                const selected = s.Index === currentSource.DefaultSubtitleStreamIndex ? 'selected' : '';
                return `<option value="${s.Index}" ${selected}>${escapeHtml(label)}</option>`;
            }).join('');
            selectSubtitles.innerHTML = subHtml;
        } else {
            selectSubtitlesContainer.classList.add('hide');
        }
    }
}

function setupButtons(view: HTMLElement, item: any, apiClient: any): void {
    const isPlayable = item.MediaType === 'Video'
        || item.MediaType === 'Audio'
        || item.MediaType === 'Book'
        || item.Type === 'Movie'
        || item.Type === 'Episode'
        || item.Type === 'Series'
        || item.Type === 'MusicAlbum'
        || item.Type === 'MusicArtist'
        || item.Type === 'Playlist'
        || item.Type === 'TvChannel'
        || item.Type === 'Program'
        || item.IsFolder;

    const btnPlay = view.querySelector<HTMLButtonElement>('.btnPlay');
    const btnReplay = view.querySelector<HTMLButtonElement>('.btnReplay');
    const btnPlayTrailer = view.querySelector<HTMLButtonElement>('.btnPlayTrailer');
    const btnInstantMix = view.querySelector<HTMLButtonElement>('.btnInstantMix');
    const btnShuffle = view.querySelector<HTMLButtonElement>('.btnShuffle');
    const btnPlaystate = view.querySelector<HTMLButtonElement>('.btnPlaystate');
    const btnUserRating = view.querySelector<HTMLButtonElement>('.btnUserRating');
    const btnMoreCommands = view.querySelector<HTMLButtonElement>('.btnMoreCommands');
    const btnDownload = view.querySelector<HTMLButtonElement>('.btnDownload');

    const isResumable = (item.UserData?.PlaybackPositionTicks ?? 0) > 0;

    const getSelectedMediaOptions = () => {
        const selectSource = view.querySelector<HTMLSelectElement>('.selectSource');
        const selectAudio = view.querySelector<HTMLSelectElement>('.selectAudio');
        const selectSubtitles = view.querySelector<HTMLSelectElement>('.selectSubtitles');

        const mediaSourceId = selectSource ? selectSource.value : undefined;
        const audioStreamIndex = selectAudio && selectAudio.value !== '' ? parseInt(selectAudio.value, 10) : undefined;
        const subVal = selectSubtitles ? selectSubtitles.value : undefined;
        const subtitleStreamIndex = subVal !== undefined && subVal !== '' ? parseInt(subVal, 10) : undefined;

        return {
            mediaSourceId: mediaSourceId || undefined,
            audioStreamIndex: isNaN(audioStreamIndex as number) ? undefined : audioStreamIndex,
            subtitleStreamIndex: subtitleStreamIndex === -1 ? -1 : (isNaN(subtitleStreamIndex as number) ? undefined : subtitleStreamIndex)
        };
    };

    if (btnPlay) {
        if (isPlayable) {
            btnPlay.classList.remove('hide');
            btnPlay.title = isResumable ? globalize.translate('ButtonResume') : globalize.translate('Play');
            btnPlay.setAttribute('data-action', isResumable ? 'resume' : 'play');
            btnPlay.onclick = () => {
                const startPositionTicks = isResumable ? (item.UserData?.PlaybackPositionTicks ?? 0) : 0;
                playbackManager.play({
                    items: [item],
                    startPositionTicks,
                    ...getSelectedMediaOptions()
                }).catch((err: unknown) => {
                    console.error('[itemDetails] play failed', err);
                });
            };
        } else {
            btnPlay.classList.add('hide');
        }
    }

    if (btnReplay) {
        if (isPlayable && isResumable) {
            btnReplay.classList.remove('hide');
            btnReplay.title = globalize.translate('Play');
            btnReplay.onclick = () => {
                playbackManager.play({
                    items: [item],
                    startPositionTicks: 0,
                    ...getSelectedMediaOptions()
                }).catch((err: unknown) => {
                    console.error('[itemDetails] replay failed', err);
                });
            };
        } else {
            btnReplay.classList.add('hide');
        }
    }

    if (btnPlayTrailer) {
        const hasTrailers = item.LocalTrailerCount || (item.RemoteTrailers && item.RemoteTrailers.length);
        if (hasTrailers) {
            btnPlayTrailer.classList.remove('hide');
            btnPlayTrailer.onclick = () => {
                playbackManager.playTrailers(item);
            };
        } else {
            btnPlayTrailer.classList.add('hide');
        }
    }

    if (btnInstantMix) {
        const supportsInstantMix = item.MediaType === 'Audio' || item.Type === 'MusicArtist' || item.Type === 'MusicAlbum' || item.Type === 'MusicGenre';
        if (supportsInstantMix) {
            btnInstantMix.classList.remove('hide');
            btnInstantMix.onclick = () => {
                playbackManager.instantMix(item);
            };
        } else {
            btnInstantMix.classList.add('hide');
        }
    }

    if (btnShuffle) {
        const canShuffle = item.IsFolder || item.Type === 'Series' || item.Type === 'MusicAlbum' || item.Type === 'Playlist' || item.Type === 'MusicArtist';
        if (canShuffle) {
            btnShuffle.classList.remove('hide');
            btnShuffle.onclick = () => {
                playbackManager.shuffle(item);
            };
        } else {
            btnShuffle.classList.add('hide');
        }
    }

    if (btnPlaystate) {
        if (itemHelper.canMarkPlayed(item)) {
            btnPlaystate.classList.remove('hide');
            if (typeof (btnPlaystate as any).setItem === 'function') {
                (btnPlaystate as any).setItem(item);
            }
        } else {
            btnPlaystate.classList.add('hide');
        }
    }

    if (btnUserRating) {
        btnUserRating.classList.remove('hide');
        if (typeof (btnUserRating as any).setItem === 'function') {
            (btnUserRating as any).setItem(item);
        }
    }

    if (btnMoreCommands) {
        btnMoreCommands.classList.remove('hide');
        btnMoreCommands.onclick = () => {
            apiClient.getCurrentUser().then((user: any) => {
                itemContextMenu.show({
                    item: item,
                    user: user,
                    positionTo: btnMoreCommands
                });
            }).catch(() => {
                itemContextMenu.show({
                    item: item,
                    user: {},
                    positionTo: btnMoreCommands
                });
            });
        };
    }

    if (btnDownload) {
        const canDownload = item.MediaType === 'Video' || item.MediaType === 'Audio' || item.Type === 'Episode' || item.Type === 'Movie';
        if (canDownload && typeof apiClient.getItemDownloadUrl === 'function') {
            btnDownload.classList.remove('hide');
            btnDownload.onclick = () => {
                window.open(apiClient.getItemDownloadUrl(item.Id), '_blank');
            };
        } else {
            btnDownload.classList.add('hide');
        }
    }
}

function loadSections(view: HTMLElement, item: any, apiClient: any): void {
    const userId = apiClient.getCurrentUserId();

    // 1. Next Up (Series)
    if (item.Type === 'Series') {
        const nextUpSection = view.querySelector<HTMLElement>('.nextUpSection');
        const nextUpItems = view.querySelector<HTMLElement>('.nextUpItems');
        if (nextUpSection && nextUpItems) {
            apiClient.getNextUpEpisodes({
                SeriesId: item.Id,
                UserId: userId
            }).then((result: any) => {
                const items = result.Items || [];
                if (items.length) {
                    nextUpSection.classList.remove('hide');
                    cardBuilder.buildCards(items, {
                        itemsContainer: nextUpItems,
                        shape: getBackdropShape(enableScrollX()),
                        preferThumb: true,
                        showTitle: true,
                        showParentTitle: false,
                        overlayPlayButton: true,
                        centerText: true
                    });
                } else {
                    nextUpSection.classList.add('hide');
                }
            }).catch(() => {
                nextUpSection.classList.add('hide');
            });
        }
    }

    // 2. Children / Seasons / Episodes
    const childrenCollapsible = view.querySelector<HTMLElement>('#childrenCollapsible');
    const childrenContainer = childrenCollapsible?.querySelector<HTMLElement>('.itemsContainer');

    if (item.Type === 'Series' && childrenContainer) {
        apiClient.getItems(userId, {
            ParentId: item.Id,
            IncludeItemTypes: 'Season',
            SortBy: 'SortName'
        }).then((result: any) => {
            const items = result.Items || [];
            if (items.length) {
                childrenCollapsible?.classList.remove('hide');
                cardBuilder.buildCards(items, {
                    itemsContainer: childrenContainer,
                    shape: getPortraitShape(enableScrollX()),
                    showTitle: true,
                    centerText: true,
                    overlayPlayButton: true
                });
            }
        });
    } else if (item.Type === 'Season' && childrenContainer) {
        apiClient.getEpisodes(item.SeriesId, {
            SeasonId: item.Id,
            UserId: userId,
            Fields: 'ItemCounts,PrimaryImageAspectRatio,BasicSyncInfo,CanDelete,MediaSourceCount,Overview'
        }).then((result: any) => {
            const items = result.Items || [];
            if (items.length) {
                childrenCollapsible?.classList.remove('hide');
                cardBuilder.buildCards(items, {
                    itemsContainer: childrenContainer,
                    shape: getBackdropShape(enableScrollX()),
                    preferThumb: true,
                    showTitle: true,
                    showParentTitle: false,
                    overlayPlayButton: true,
                    centerText: true
                });
            }
        });
    } else if ((item.Type === 'MusicAlbum' || item.Type === 'Playlist') && childrenContainer) {
        apiClient.getItems(userId, {
            ParentId: item.Id,
            SortBy: 'SortName'
        }).then((result: any) => {
            const items = result.Items || [];
            if (items.length) {
                childrenCollapsible?.classList.remove('hide');
                cardBuilder.buildCards(items, {
                    itemsContainer: childrenContainer,
                    shape: 'square',
                    showTitle: true,
                    centerText: true,
                    overlayPlayButton: true
                });
            }
        });
    }

    // 3. Cast & Crew
    const castCollapsible = view.querySelector<HTMLElement>('#castCollapsible');
    const castContent = view.querySelector<HTMLElement>('#castContent');
    if (castCollapsible && castContent) {
        if (item.People && item.People.length) {
            castCollapsible.classList.remove('hide');
            peoplecardbuilder.buildPeopleCards(item.People, {
                itemsContainer: castContent,
                serverId: item.ServerId
            } as any);
        } else {
            castCollapsible.classList.add('hide');
        }
    }

    // 4. Specials / Special Features
    const specialsCollapsible = view.querySelector<HTMLElement>('#specialsCollapsible');
    const specialsContent = view.querySelector<HTMLElement>('#specialsContent');
    if (specialsCollapsible && specialsContent) {
        apiClient.getSpecialFeatures(userId, item.Id).then((specials: any[]) => {
            if (specials && specials.length) {
                specialsCollapsible.classList.remove('hide');
                cardBuilder.buildCards(specials, {
                    itemsContainer: specialsContent,
                    shape: getBackdropShape(enableScrollX()),
                    preferThumb: true,
                    showTitle: true,
                    centerText: true,
                    overlayPlayButton: true
                });
            } else {
                specialsCollapsible.classList.add('hide');
            }
        }).catch(() => {
            specialsCollapsible.classList.add('hide');
        });
    }

    // 5. Additional Video Parts
    const partsCollapsible = view.querySelector<HTMLElement>('#additionalPartsCollapsible');
    const partsContent = view.querySelector<HTMLElement>('#additionalPartsContent');
    if (partsCollapsible && partsContent) {
        if (item.PartCount && item.PartCount > 1) {
            apiClient.getAdditionalVideoParts(userId, item.Id).then((result: any) => {
                const parts = result.Items || [];
                if (parts.length) {
                    partsCollapsible.classList.remove('hide');
                    cardBuilder.buildCards(parts, {
                        itemsContainer: partsContent,
                        shape: getBackdropShape(enableScrollX()),
                        showTitle: true,
                        centerText: true,
                        overlayPlayButton: true
                    });
                } else {
                    partsCollapsible.classList.add('hide');
                }
            }).catch(() => {
                partsCollapsible.classList.add('hide');
            });
        } else {
            partsCollapsible.classList.add('hide');
        }
    }

    // 6. Scenes / Chapters
    const scenesCollapsible = view.querySelector<HTMLElement>('#scenesCollapsible');
    const scenesContent = view.querySelector<HTMLElement>('#scenesContent');
    if (scenesCollapsible && scenesContent) {
        if (item.Chapters && item.Chapters.length) {
            scenesCollapsible.classList.remove('hide');
            const chapterItems = item.Chapters.map((ch: any, idx: number) => ({
                Id: item.Id,
                Name: ch.Name || `${globalize.translate('Chapter') || 'Chapter'} ${idx + 1}`,
                StartPositionTicks: ch.StartPositionTicks,
                ImageTag: ch.ImageTag,
                Type: 'Chapter',
                ServerId: item.ServerId
            }));
            cardBuilder.buildCards(chapterItems, {
                itemsContainer: scenesContent,
                shape: getBackdropShape(enableScrollX()),
                showTitle: true,
                centerText: true,
                overlayPlayButton: true
            });
        } else {
            scenesCollapsible.classList.add('hide');
        }
    }

    // 7. Similar Items
    const similarCollapsible = view.querySelector<HTMLElement>('#similarCollapsible');
    const similarContent = view.querySelector<HTMLElement>('.similarContent');
    if (similarCollapsible && similarContent) {
        apiClient.getSimilarItems(item.Id, {
            UserId: userId,
            Limit: 12
        }).then((result: any) => {
            const similarItems = result.Items || [];
            if (similarItems.length) {
                similarCollapsible.classList.remove('hide');
                cardBuilder.buildCards(similarItems, {
                    itemsContainer: similarContent,
                    shape: getPortraitShape(enableScrollX()),
                    showTitle: true,
                    centerText: true,
                    overlayPlayButton: true
                });
            } else {
                similarCollapsible.classList.add('hide');
            }
        }).catch(() => {
            similarCollapsible.classList.add('hide');
        });
    }
}

export default function (view: HTMLElement, params: ViewParams): void {
    function renderItem(item: any, apiClient: any): void {
        const displayName = itemHelper.getDisplayName(item);
        view.setAttribute('data-title', displayName);
        libraryMenu.setTitle(displayName);

        // Backdrop
        setBackdrops([item]);

        // Logo & Poster
        renderLogo(view, item, apiClient);
        renderPoster(view, item, apiClient);

        // Name & Metadata
        renderName(view, item);
        renderMetadata(view, item);

        // Track selections
        setupTrackSelections(view, item);

        // Action Buttons
        setupButtons(view, item, apiClient);

        // Child sections
        loadSections(view, item, apiClient);

        loading.hide();
        autoFocuser.autoFocus(view);
    }

    function loadData(): void {
        const itemId = params.id || params.itemId || params.seriesTimerId;
        if (!itemId) {
            loading.hide();
            return;
        }

        loading.show();
        const apiClient = (params.serverId ? ServerConnections.getApiClient(params.serverId) : null) || ApiClient;

        if (params.seriesTimerId) {
            apiClient.getLiveTvSeriesTimer(params.seriesTimerId).then((item: any) => {
                renderItem(item, apiClient);
            }).catch((err: unknown) => {
                console.error('[itemDetails] failed to load series timer', err);
                loading.hide();
            });
        } else {
            apiClient.getItem(apiClient.getCurrentUserId(), itemId).then((item: any) => {
                renderItem(item, apiClient);
            }).catch((err: unknown) => {
                console.error('[itemDetails] failed to load item', err);
                loading.hide();
            });
        }
    }

    view.addEventListener('viewshow', () => {
        loadData();
    });

    view.addEventListener('viewbeforehide', () => {
        clearBackdrop();
    });

    view.addEventListener('viewdestroy', () => {
        clearBackdrop();
    });
}
