#pragma warning disable CS1591

using MediaBrowser.Controller.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.Library
{
    public interface IMetadataFileSaver : IMetadataSaver
    {
        /// <summary>
        /// Gets the save path.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>System.String.</returns>
        string GetSavePath(BaseItem item);

        /// <summary>
        /// Saves the metadata to an explicit path.
        /// </summary>
        Task SaveAsync(BaseItem item, string path, CancellationToken cancellationToken);
    }
}
