using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Low-level interface for a specific flight data provider (e.g., Amadeus, AviationStack).
/// Each provider implements this interface independently; higher-level code selects a
/// concrete implementation through <see cref="IFlightSearchProviderFactory"/>.
/// </summary>
public interface IFlightSearchProvider
{
    /// <summary>
    /// Searches for available flights between two airports on a given date.
    /// </summary>
    Task<TransportationSearchResult> SearchFlightsAsync(
        string originCode,
        string destinationCode,
        DateOnly departureDate,
        int adults = 1,
        string currency = "USD",
        int maxResults = 10,
        CancellationToken cancellationToken = default);
}
