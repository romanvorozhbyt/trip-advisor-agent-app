using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Searches for real-time transportation options including flights.
/// </summary>
public interface ITransportationService
{
    /// <summary>
    /// Searches for available flights between two airports on a given date.
    /// </summary>
    /// <param name="originCode">IATA airport code for the origin (e.g., "JFK").</param>
    /// <param name="destinationCode">IATA airport code for the destination (e.g., "LHR").</param>
    /// <param name="departureDate">Date of departure (time component is ignored).</param>
    /// <param name="adults">Number of adult passengers.</param>
    /// <param name="currency">ISO 4217 currency code for prices (default: USD).</param>
    /// <param name="maxResults">Maximum number of offers to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TransportationSearchResult"/> with ranked options.</returns>
    Task<TransportationSearchResult> SearchFlightsAsync(
        string originCode,
        string destinationCode,
        DateOnly departureDate,
        int adults = 1,
        string currency = "USD",
        int maxResults = 10,
        CancellationToken cancellationToken = default);
}
