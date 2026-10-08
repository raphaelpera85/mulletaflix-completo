/**
 * Page state management and standardized state components.
 * 
 * These exports provide a standardized way to handle page states across the application:
 * - loading, empty, error with retry, offline/degraded, and success states.
 */

export { default as PageStateContainer, type PageState } from './PageStateContainer';
export { getRemotePageState } from './remotePageState';
export { default as OfflineState } from './OfflineState';
export { default as DetailPageSkeleton } from './DetailPageSkeleton';
export { default as ListPageSkeleton } from './ListPageSkeleton';
export { default as LoadErrorMessage } from './LoadErrorMessage';
export { EmptyState } from '../EmptyState';
