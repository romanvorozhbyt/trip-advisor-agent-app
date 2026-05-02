using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TripAdvisorAgent.Infrastructure;
using TripAdvisorAgent.Infrastructure.Services;

// ── Bootstrap ──────────────────────────────────────────────────────────────
// Build configuration from environment variables (Lambda sets all app config
// as env vars; map them with the double-underscore (__) hierarchy separator).
var configuration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();

services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();          // CloudWatch Logs captures stdout
    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Information);
});

services.AddNewsIngestionServices(configuration);

await using var serviceProvider = services.BuildServiceProvider();

// ── Lambda Handler ─────────────────────────────────────────────────────────
// The handler is invoked by EventBridge Scheduler every 3 hours.
// The event payload is an empty JSON object ({}) — we ignore it.
Func<ILambdaContext, Task> handler = async (ILambdaContext context) =>
{
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation(
        "Lambda invocation started. RequestId={RequestId} RemainingTime={Remaining}",
        context.AwsRequestId,
        context.RemainingTime);

    var runner = serviceProvider.GetRequiredService<NewsIngestionRunner>();

    using var cts = new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(10));
    await runner.RunAsync(cts.Token);

    logger.LogInformation("Lambda invocation complete. RequestId={RequestId}", context.AwsRequestId);
};

await LambdaBootstrapBuilder
    .Create(handler)   // context-only overload; no serializer needed (no input payload)
    .Build()
    .RunAsync();

// Top-level statement class used only for ILogger<T> generic type parameter.
public partial class Program { }
