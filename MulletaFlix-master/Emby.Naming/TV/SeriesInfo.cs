namespace Emby.Naming.TV
{
    /// <summary>
    /// Holder object for Series information.
    /// </summary>
    public class SeriesInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SeriesInfo"/> class.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        public SeriesInfo(string path)
        {
            Path = path;
        }

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the name of the series.
        /// </summary>
        /// <value>The name of the series.</value>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the year parsed from the series folder name, when it carries one.
        /// </summary>
        /// <value>The release year, or null when the name carries none.</value>
        /// <remarks>
        /// Kept so the year reaches the item instead of being discarded together with the folder-name
        /// suffix. Two series that share a name but not a year ("A Agencia (2020)" and
        /// "A Agencia (2024)") are indistinguishable to every metadata provider without it.
        /// </remarks>
        public int? Year { get; set; }
    }
}
