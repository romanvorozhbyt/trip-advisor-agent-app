using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripAdvisorAgent.Infrastructure.Configuration;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Hosted background service that schedules <see cref="NewsIngestionRunner"/> on a
/// periodic timer. The actual feed-fetching logic lives in <see cref="NewsIngestionRunner"/>
/// so that the same code path can be invoked from the AWS Lambda scheduler function.
/// </summary>
public sealed class NewsIngestionService(
    NewsIngestionRunner runner,
    IOptions<NewsOptions> options,
    ILogger<NewsIngestionService> logger) : BackgroundService
{
    private readonly NewsOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.RssFeeds.Length == 0)
        {
            logger.LogWarning("No RSS feeds configured (News:RssFeeds). News ingestion is disabled.");
            return;
        }

        logger.LogInformation(
            "News ingestion service started. Interval: {Hours}h, Feeds: {Count}",
            _options.FetchIntervalHours, _options.RssFeeds.Length);

        // Fetch immediately on startup, then on the configured interval.
        await runner.RunAsync(stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(_options.FetchIntervalHours));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await runner.RunAsync(stoppingToken);
        }
    }
}
