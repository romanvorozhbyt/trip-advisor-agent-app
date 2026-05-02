using System.Net;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Infrastructure.Configuration;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Core news-ingestion logic: fetches travel-relevant RSS feeds and upserts new
/// articles into the Qdrant knowledge base. Idempotent via deterministic record IDs.
/// Consumed by both <see cref="NewsIngestionService"/> (hosted background service)
/// and the AWS Lambda scheduler function.
/// </summary>
public sealed partial class NewsIngestionRunner(
    IKnowledgeBaseService knowledgeBaseService,
    IOptions<NewsOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<NewsIngestionRunner> logger)
{
    private readonly NewsOptions _options = options.Value;

    /// <summary>
    /// Runs a full ingestion cycle across all configured RSS feeds.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (_options.RssFeeds.Length == 0)
        {
            logger.LogWarning("No RSS feeds configured (News:RssFeeds). Ingestion skipped.");
            return;
        }

        logger.LogInformation("Starting news ingestion cycle at {Time:u}", DateTimeOffset.UtcNow);

        await knowledgeBaseService.InitializeAsync(cancellationToken);

        var totalIngested = 0;
        var totalSkipped = 0;

        foreach (var feedUrl in _options.RssFeeds)
        {
            try
            {
                var (ingested, skipped) = await ProcessFeedAsync(feedUrl, cancellationToken);
                totalIngested += ingested;
                totalSkipped += skipped;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Failed to process RSS feed: {Url}", feedUrl);
            }
        }

        logger.LogInformation(
            "News ingestion cycle complete. New articles ingested: {Ingested}, Already known: {Skipped}",
            totalIngested, totalSkipped);
    }

    private async Task<(int Ingested, int Skipped)> ProcessFeedAsync(
        string feedUrl, CancellationToken cancellationToken)
    {
        using var client = httpClientFactory.CreateClient("news");

        // Read the full body as text so we can sanitize malformed entities (e.g. bare '&'
        // in attribute values like ?foo=bar&baz=qux) before handing to the strict XmlReader.
        var raw = await client.GetStringAsync(feedUrl, cancellationToken);
        var sanitized = SanitizeXmlEntities(raw);

        using var xmlReader = XmlReader.Create(
            new StringReader(sanitized),
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                MaxCharactersFromEntities = 1024,
            });

        var feed = SyndicationFeed.Load(xmlReader);
        var feedTitle = feed.Title?.Text ?? feedUrl;

        var ingested = 0;
        var skipped = 0;

        foreach (var item in feed.Items.Take(_options.MaxItemsPerFeed))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var itemKey = item.Id
                ?? item.Links.FirstOrDefault()?.Uri?.ToString()
                ?? item.Title?.Text;

            if (string.IsNullOrWhiteSpace(itemKey))
            {
                logger.LogDebug("Skipping news item with no identifier from feed: {Feed}", feedTitle);
                continue;
            }

            var recordId = CreateDeterministicGuid("news:" + itemKey);

            if (await knowledgeBaseService.RecordExistsAsync(recordId, cancellationToken))
            {
                skipped++;
                continue;
            }

            var content = FormatNewsItem(item, feedTitle);
            var category = DetermineCategory(feedUrl, item);

            await IngestWithRetryAsync(content, category, recordId, cancellationToken);
            ingested++;

            logger.LogDebug("Ingested: [{Category}] {Title}", category, item.Title?.Text);
        }

        logger.LogInformation(
            "Feed '{Feed}': +{Ingested} new, {Skipped} already known",
            feedTitle, ingested, skipped);

        return (ingested, skipped);
    }

    /// <summary>
    /// Wraps <see cref="IKnowledgeBaseService.IngestAsync"/> with simple exponential-backoff
    /// retry for HTTP 429 (embedding rate-limit) responses. At most 3 attempts.
    /// </summary>
    private async Task IngestWithRetryAsync(
        string content, string category, Guid recordId, CancellationToken cancellationToken)
    {
        const int maxAttempts = 3;
        var delay = TimeSpan.FromSeconds(15);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await knowledgeBaseService.IngestAsync(content, category, recordId, cancellationToken);
                return;
            }
            catch (Exception ex) when (IsRateLimit(ex) && attempt < maxAttempts)
            {
                logger.LogWarning(
                    "Embedding rate-limited (attempt {Attempt}/{Max}). Waiting {Delay}s before retry.",
                    attempt, maxAttempts, delay.TotalSeconds);

                await Task.Delay(delay, cancellationToken);
                delay *= 2; // exponential back-off: 15s → 30s
            }
        }
    }

    private static bool IsRateLimit(Exception ex) =>
        ex is System.ClientModel.ClientResultException cre && cre.Status == (int)HttpStatusCode.TooManyRequests;

    private string FormatNewsItem(SyndicationItem item, string feedTitle)
    {
        var title = item.Title?.Text?.Trim() ?? "Untitled";

        var date = item.PublishDate != DateTimeOffset.MinValue
            ? item.PublishDate.ToString("yyyy-MM-dd")
            : item.LastUpdatedTime != DateTimeOffset.MinValue
                ? item.LastUpdatedTime.ToString("yyyy-MM-dd")
                : DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");

        var rawSummary = item.Summary?.Text
            ?? (item.Content is TextSyndicationContent tc ? tc.Text : null)
            ?? string.Empty;

        var summary = StripHtml(rawSummary);
        if (summary.Length > _options.ArticleMaxLength)
            summary = summary[.._options.ArticleMaxLength] + "...";

        var link = item.Links.FirstOrDefault()?.Uri?.ToString() ?? string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine($"[TRAVEL NEWS] {title}");
        sb.AppendLine($"Date: {date}");
        sb.AppendLine($"Source: {feedTitle}");
        if (!string.IsNullOrEmpty(link))
            sb.AppendLine($"URL: {link}");
        if (!string.IsNullOrWhiteSpace(summary))
        {
            sb.AppendLine();
            sb.Append(summary);
        }

        return sb.ToString().Trim();
    }

    private static string DetermineCategory(string feedUrl, SyndicationItem item)
    {
        if (feedUrl.Contains("travel.state.gov", StringComparison.OrdinalIgnoreCase)
            || feedUrl.Contains("gov.uk/foreign-travel-advice", StringComparison.OrdinalIgnoreCase)
            || feedUrl.Contains("smartraveller.gov.au", StringComparison.OrdinalIgnoreCase))
        {
            return "travel-advisory";
        }

        var combined = $"{item.Title?.Text} {item.Summary?.Text}".ToLowerInvariant();

        if (AdvisoryKeywords().IsMatch(combined))
            return "travel-advisory";

        return "world-news";
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return html;

        var text = HtmlTagRegex().Replace(html, " ");
        text = text
            .Replace("&amp;", "&", StringComparison.Ordinal)
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&#39;", "'", StringComparison.Ordinal)
            .Replace("&nbsp;", " ", StringComparison.Ordinal);

        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    private static Guid CreateDeterministicGuid(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }

    /// <summary>
    /// Replaces bare <c>&amp;</c> characters that are not part of a valid XML entity reference
    /// (<c>&amp;name;</c> or <c>&amp;#nnn;</c>) with <c>&amp;amp;</c>.
    /// This makes feeds like travel.state.gov (which embed raw query strings in attribute
    /// values) parseable by the strict <see cref="XmlReader"/>.
    /// </summary>
    private static string SanitizeXmlEntities(string xml) =>
        BareAmpersandRegex().Replace(xml, "&amp;");

    // Matches '&' that is NOT followed by a valid entity: alphanumeric name + ';'  or  '#' digits + ';'
    [GeneratedRegex("&(?!(?:[a-zA-Z][a-zA-Z0-9]*|#[0-9]+|#x[0-9a-fA-F]+);)", RegexOptions.Compiled)]
    private static partial Regex BareAmpersandRegex();

    [GeneratedRegex(
        @"war|conflict|crisis|attack|terrorism|coup|sanction|embargo|" +
        @"travel\s+ban|travel\s+warning|travel\s+advisory|do\s+not\s+travel|" +
        @"evacuation|missile|bombing|earthquake|tsunami|hurricane|volcano|" +
        @"protest|riot|civil\s+unrest|border\s+closed|embassy|martial\s+law",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AdvisoryKeywords();

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
