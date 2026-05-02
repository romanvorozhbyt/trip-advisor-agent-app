using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TripAdvisorAgent.Core.Interfaces;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Resolves the active <see cref="IFlightSearchProvider"/> by reading
/// <c>FlightSearch:Provider</c> from application configuration.
/// Supported values: <c>AirLabs</c> (default), <c>Amadeus</c>.
/// Providers are registered as keyed services and resolved lazily so only the
/// active provider's options are validated at startup.
/// </summary>
public class FlightSearchProviderFactory(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    ILogger<FlightSearchProviderFactory> logger) : IFlightSearchProviderFactory
{
    /// <inheritdoc />
    public IFlightSearchProvider GetProvider()
    {
        var key = configuration["FlightSearch:Provider"] ?? "AirLabs";

        var providerKey = key.Equals("Amadeus", StringComparison.OrdinalIgnoreCase)
            ? "Amadeus"
            : "AirLabs";

        logger.LogInformation("Resolving flight search provider: {ProviderKey} (configured: '{ConfiguredKey}')",
            providerKey, key);

        return serviceProvider.GetRequiredKeyedService<IFlightSearchProvider>(providerKey);
    }
}
