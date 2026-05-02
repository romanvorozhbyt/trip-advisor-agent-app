using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Facade over <see cref="IFlightSearchProviderFactory"/> that implements <see cref="ITransportationService"/>.
/// All callers (e.g., the Semantic Kernel plugin) depend only on this class; the actual
/// flight data provider is resolved at runtime by the factory from application configuration.
/// </summary>
public class TransportationService(IFlightSearchProviderFactory providerFactory) : ITransportationService
{
    /// <inheritdoc />
    public Task<TransportationSearchResult> SearchFlightsAsync(
        string originCode,
        string destinationCode,
        DateOnly departureDate,
        int adults = 1,
        string currency = "USD",
        int maxResults = 10,
        CancellationToken cancellationToken = default)
        => providerFactory.GetProvider().SearchFlightsAsync(
            originCode, destinationCode, departureDate, adults, currency, maxResults, cancellationToken);
}
