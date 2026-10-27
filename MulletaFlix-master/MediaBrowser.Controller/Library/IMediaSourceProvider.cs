#pragma warning disable CA1002, CS1591

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Dto;

namespace MediaBrowser.Controller.Library
{
    public interface IMediaSourceProvider
    {
        /// <summary>
        /// Gets the media sources.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task&lt;IEnumerable&lt;MediaSourceInfo&gt;&gt;.</returns>
        Task<IEnumerable<MediaSourceInfo>> GetMediaSources(BaseItem item, CancellationToken cancellationToken);

        /// <summary>
        /// Opens the media source.
        /// </summary>
        /// <param name="openToken">Token to use.</param>
        /// <param name="currentLiveStreams">List of live streams.</param>
        /// <param name="cancellationToken">CancellationToken to use for operation.</param>
        /// <returns>The media source wrapped as an awaitable task.</returns>
        Task<ILiveStream> OpenMediaSource(string openToken, List<ILiveStream> currentLiveStreams, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the key that identifies opens which must observe one another's registered streams.
        /// </summary>
        /// <param name="openToken">Token passed to <see cref="OpenMediaSource"/>.</param>
        /// <returns>A provider-scoped concurrency key. The token is used when no narrower key is defined.</returns>
        string GetOpenMediaSourceLockKey(string openToken) => openToken;
    }
}
