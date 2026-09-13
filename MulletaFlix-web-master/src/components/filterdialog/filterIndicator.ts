import './filterIndicator.scss';

interface FilterQuery {
    Filters?: string;
    IsFavorite?: boolean;
    VideoTypes?: string;
    SeriesStatus?: string;
    Is4K?: boolean;
    IsHD?: number | null;
    IsSD?: boolean;
    Is3D?: boolean;
    HasSubtitles?: boolean;
    HasTrailer?: boolean;
    HasSpecialFeature?: boolean;
    HasThemeSong?: boolean;
    HasThemeVideo?: boolean;
    IsMissing?: boolean;
    ParentIndexNumber?: number;
    Genres?: string;
    Tags?: string;
    Years?: string;
    OfficialRatings?: string;
    IsUnaired?: boolean;
}

export function getFilterStatus(query: object): boolean {
    const filterQuery = query as FilterQuery;
    return Boolean(
        filterQuery.Filters
            || filterQuery.IsFavorite
            || filterQuery.VideoTypes
            || filterQuery.SeriesStatus
            || filterQuery.Is4K
            || (filterQuery.IsHD !== undefined && filterQuery.IsHD !== null)
            || filterQuery.IsSD
            || filterQuery.Is3D
            || filterQuery.HasSubtitles
            || filterQuery.HasTrailer
            || filterQuery.HasSpecialFeature
            || filterQuery.HasThemeSong
            || filterQuery.HasThemeVideo
            || filterQuery.IsMissing
            || filterQuery.ParentIndexNumber
            || filterQuery.Genres
            || filterQuery.Tags
            || filterQuery.Years
            || filterQuery.OfficialRatings
            || filterQuery.IsUnaired
    );
}

export function setFilterStatus(page: HTMLElement, query: object): void {
    const hasFilters = getFilterStatus(query);

    const btnFilterWrapper = page.querySelector('.btnFilter-wrapper') as HTMLElement | null;

    if (btnFilterWrapper) {
        let indicatorElem = btnFilterWrapper.querySelector('.filterIndicator') as HTMLElement | null;

        if (!indicatorElem && hasFilters) {
            btnFilterWrapper.insertAdjacentHTML(
                'afterbegin',
                '<div class="filterIndicator">!</div>'
            );
            btnFilterWrapper.classList.add('btnFilterWithIndicator');
            indicatorElem = btnFilterWrapper.querySelector('.filterIndicator');
        }

        if (indicatorElem) {
            indicatorElem.classList.toggle('hide', !hasFilters);
        }
    }
}
