using Microsoft.Extensions.Options;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Infrastructure.Configuration;
using TripAdvisorAgent.Infrastructure.Services;

namespace TripAdvisorAgent.WebApi.Endpoints;

public static class NewsIngestionEndpoints
{
    public static void MapNewsIngestionEndpoints(this WebApplication app)
    {
        // Restricted to Development so this trigger is never exposed in production.
        if (!app.Environment.IsDevelopment())
            return;

        var group = app.MapGroup("/api/dev/news-ingestion");

        // POST /api/dev/news-ingestion/run
        // Manually triggers a full ingestion cycle and returns a summary.
        group.MapPost("/run", async (
            NewsIngestionRunner runner,
            IOptions<NewsOptions> options,
            CancellationToken ct) =>
        {
            var feeds = options.Value.RssFeeds;

            if (feeds.Length == 0)
                return Results.BadRequest(new
                {
                    error = "No RSS feeds configured. Add entries to News:RssFeeds in appsettings.Development.json."
                });

            var started = DateTimeOffset.UtcNow;
            await runner.RunAsync(ct);

            return Results.Ok(new
            {
                message = "Ingestion cycle complete.",
                feedsProcessed = feeds.Length,
                feeds,
                durationMs = (DateTimeOffset.UtcNow - started).TotalMilliseconds
            });
        })
        .WithName("TriggerNewsIngestion")
        .WithSummary("(Dev only) Manually trigger a news-ingestion cycle.")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status400BadRequest);

        // GET /api/dev/news-ingestion/config
        // Returns the active NewsOptions so you can verify configuration without restarting.
        group.MapGet("/config", (IOptions<NewsOptions> options) =>
        {
            var o = options.Value;
            return Results.Ok(new
            {
                fetchIntervalHours = o.FetchIntervalHours,
                maxItemsPerFeed = o.MaxItemsPerFeed,
                articleMaxLength = o.ArticleMaxLength,
                rssFeedsRaw = o.RssFeedsRaw,
                rssFeeds = o.RssFeeds,
                feedCount = o.RssFeeds.Length
            });
        })
        .WithName("GetNewsIngestionConfig")
        .WithSummary("(Dev only) Returns the active news-ingestion configuration.");

        // GET /api/dev/news-ingestion/search?q=travel+advisory&topK=5
        // Runs a semantic search against Qdrant to confirm articles were actually stored.
        group.MapGet("/search", async (
            string q,
            IKnowledgeBaseService knowledgeBase,
            int topK = 5,
            CancellationToken ct = default) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.BadRequest(new { error = "Query parameter 'q' is required." });

            var results = await knowledgeBase.SearchAsync(q, topK, ct);

            return Results.Ok(new
            {
                query = q,
                topK,
                count = results.Count,
                results = results.Select((text, i) => new
                {
                    rank = i + 1,
                    preview = text.Length > 300 ? text[..300] + "…" : text,
                    fullText = text
                })
            });
        })
        .WithName("SearchNewsIngestion")
        .WithSummary("(Dev only) Semantic search over ingested knowledge base — use to verify ingestion worked.")
        .Produces<object>(StatusCodes.Status200OK)
        .Produces<object>(StatusCodes.Status400BadRequest);
    }
}
