import React, { type FC, type ReactNode } from 'react';
import LoadingComponent from 'components/loading/LoadingComponent';
import LoadErrorMessage from 'components/common/LoadErrorMessage';
import OfflineState from 'components/common/OfflineState';
import { EmptyState } from 'components/EmptyState';

export type PageState = 'loading' | 'offline' | 'degraded' | 'error' | 'empty' | 'success';

interface PageStateContainerProps {
    /**
     * Current state of the page.
     * - loading: show loading spinner
     * - offline: show offline alert
     * - degraded: show a degraded warning and retain any already-loaded children
     * - error: show error message with retry
     * - empty: show empty state message
     * - success: render children normally
     */
    state: PageState;

    /**
     * Content to render when state is 'success'.
     */
    children?: ReactNode;

    /**
     * Optional callback when user clicks retry (error/offline states).
     */
    onRetry?: () => void;

    /**
     * Optional custom error message (defaults to generic error).
     */
    errorMessage?: string;

    /**
     * Optional custom offline message (defaults to generic offline).
     */
    offlineMessage?: string;

    /**
     * Empty state configuration (title, description, icon, action).
     */
    emptyState?: {
        title?: string;
        description?: string;
        icon?: ReactNode;
        action?: ReactNode;
        className?: string;
    };

    /**
     * Optional custom loading component (defaults to LoadingComponent).
     */
    loadingComponent?: ReactNode;
}

/**
 * Standardized page state manager that renders the appropriate UI for each page state.
 * Ensures consistent loading, error, offline, and empty state displays across all pages.
 *
 * Usage:
 * ```tsx
 * <PageStateContainer
 *   state={isPending ? 'loading' : isError ? 'error' : data?.length === 0 ? 'empty' : 'success'}
 *   onRetry={refetch}
 *   errorMessage='Failed to load items'
 *   emptyState={{ title: 'No items found', icon: <EmptyIcon /> }}
 * >
 *   <YourContent data={data} />
 * </PageStateContainer>
 * ```
 */
const PageStateContainer: FC<PageStateContainerProps> = ({
    state,
    children,
    onRetry,
    errorMessage,
    offlineMessage,
    emptyState,
    loadingComponent
}) => {
    switch (state) {
        case 'loading':
            return React.createElement(React.Fragment, null, loadingComponent || <LoadingComponent />);

        case 'offline':
            return (
                <OfflineState
                    isDegraded={false}
                    message={offlineMessage}
                    onRetry={onRetry}
                />
            );

        case 'degraded':
            return (
                <>
                    <OfflineState
                        isDegraded
                        message={offlineMessage}
                        onRetry={onRetry}
                    />
                    {children}
                </>
            );

        case 'error':
            return (
                <LoadErrorMessage
                    message={errorMessage}
                    onRetry={onRetry}
                />
            );

        case 'empty':
            return (
                <EmptyState
                    title={emptyState?.title}
                    description={emptyState?.description}
                    icon={emptyState?.icon}
                    action={emptyState?.action}
                    className={emptyState?.className}
                />
            );

        case 'success':
            return React.createElement(React.Fragment, null, children);

        default: {
            const _exhaustive: never = state;
            return _exhaustive;
        }
    }
};

export default PageStateContainer;
