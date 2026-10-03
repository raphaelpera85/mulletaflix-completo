#nullable disable

using System.IO;
using System.Xml;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using Microsoft.Extensions.Logging;

namespace MediaBrowser.XbmcMetadata.Savers
{
    /// <summary>
    /// Saves book metadata next to the book file.
    /// </summary>
    public sealed class BookNfoSaver : BaseNfoSaver
    {
        public BookNfoSaver(
            IFileSystem fileSystem,
            IServerConfigurationManager configurationManager,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IUserDataManager userDataManager,
            ILogger<BookNfoSaver> logger)
            : base(fileSystem, configurationManager, libraryManager, userManager, userDataManager, logger)
        {
        }

        protected override string GetLocalSavePath(BaseItem item)
            => Path.ChangeExtension(item.Path, ".nfo");

        protected override string GetRootElementName(BaseItem item)
            => "book";

        public override bool IsEnabledFor(BaseItem item, ItemUpdateType updateType)
            => item.SupportsLocalMetadata && item is Book && updateType >= MinimumUpdateType;

        protected override void WriteCustomElements(BaseItem item, XmlWriter writer)
        {
            if (item.TryGetProviderId("OpenLibrary", out var openLibraryId))
            {
                writer.WriteElementString("openlibraryid", openLibraryId);
            }

            if (item.TryGetProviderId("ISBN", out var isbn))
            {
                writer.WriteElementString("isbn", isbn);
            }
        }
    }
}
