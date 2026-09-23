using System;
using System.Collections.Generic;

namespace MediaBrowser.Providers.Plugins.ShortMax;

public sealed class ShortMaxSeries
{
    public string SeriesId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    public string Cover { get; set; } = string.Empty;
    public string PagePath { get; set; } = string.Empty;
    public IReadOnlyList<ShortMaxEpisode> Episodes { get; set; } = Array.Empty<ShortMaxEpisode>();
}

public sealed class ShortMaxEpisode
{
    public int Number { get; set; }
    public string PagePath { get; set; } = string.Empty;
}

public sealed class ShortMaxMatch
{
    public ShortMaxMatch(ShortMaxSeries series, double score)
    {
        Series = series;
        Score = score;
    }

    public ShortMaxSeries Series { get; }
    public double Score { get; }
}
