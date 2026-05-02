namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Configuration for the RSS news ingestion background service.
/// </summary>
public class NewsOptions
{
    public const string SectionName = "News";

    /// <summary>RSS feed URLs to poll for travel-relevant news and advisories.</summary>
    public string[] RssFeeds { get; set; } = [];

    /// <summary>
    /// Alternative to <see cref="RssFeeds"/> for environments (e.g. AWS Lambda) where
    /// an array cannot be expressed as a single environment variable. Provide a
    /// comma-separated list of URLs; the DI setup will split this into
    /// <see cref="RssFeeds"/> via PostConfigure when <see cref="RssFeeds"/> is empty.
    /// </summary>
    public string RssFeedsRaw { get; set; } = string.Empty;

    /// <summary>How often to refresh all feeds, in hours. Defaults to 6.</summary>
    public int FetchIntervalHours { get; set; } = 6;

    /// <summary>Maximum number of items to process per feed per cycle. Defaults to 20.</summary>
    public int MaxItemsPerFeed { get; set; } = 20;

    /// <summary>Maximum character length for the ingested article content. Defaults to 800.</summary>
    public int ArticleMaxLength { get; set; } = 800;
}
