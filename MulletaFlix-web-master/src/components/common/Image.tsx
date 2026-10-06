import React, { type FC, memo, useCallback, useState } from 'react';
import { BlurhashCanvas } from 'react-blurhash';
import { LazyLoadImage } from 'react-lazy-load-image-component';

const imageStyle: React.CSSProperties = {
    position: 'absolute',
    top: 0,
    bottom: 0,
    left: 0,
    right: 0,
    width: '100%',
    height: '100%',
    zIndex: 0
};

interface ImageProps {
    imgUrl: string;
    blurhash?: string;
    containImage: boolean;
    /**
     * Marks this image as above-the-fold / the primary visible image
     * (e.g. the first cards in a grid, or a page's hero image). When true,
     * the image is rendered immediately at a high fetch priority instead of
     * waiting for the IntersectionObserver-based lazy loader, so it doesn't
     * compete with itself for LCP.
     */
    priority?: boolean;
}

const Image: FC<ImageProps> = ({
    imgUrl,
    blurhash,
    containImage,
    priority = false
}) => {
    const [isLoaded, setIsLoaded] = useState(false);
    const [isLoadStarted, setIsLoadStarted] = useState(priority);
    const handleLoad = useCallback(() => {
        setIsLoaded(true);
    }, []);

    const handleLoadStarted = useCallback(() => {
        setIsLoadStarted(true);
    }, []);

    return (
        <div>
            {!isLoaded && isLoadStarted && blurhash && (
                <BlurhashCanvas
                    hash={blurhash}
                    width= {20}
                    height={20}
                    punch={1}
                    style={{
                        ...imageStyle,
                        borderRadius: '0.2em',
                        pointerEvents: 'none'
                    }}
                />
            )}
            <LazyLoadImage
                key={imgUrl}
                src={imgUrl}
                threshold={1200}
                delayMethod='debounce'
                delayTime={40}
                visibleByDefault={priority}
                loading={priority ? 'eager' : 'lazy'}
                fetchPriority={priority ? 'high' : 'auto'}
                wrapperProps={{
                    style: imageStyle
                }}
                style={{
                    ...imageStyle,
                    objectFit: containImage ? 'contain' : 'cover'
                }}
                decoding='async'
                onLoad={handleLoad}
                beforeLoad={handleLoadStarted}
            />

        </div>
    );
};

export default memo(Image);
