namespace TripAdvisorAgent.Core.Models;

/// <summary>
/// Represents a single flight offer returned by a transportation search.
/// </summary>
public class FlightOffer
{
    /// <summary>Gets or sets the airline carrier code (e.g., "AA", "LH").</summary>
    public string CarrierCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the full airline name.</summary>
    public string CarrierName { get; set; } = string.Empty;

    /// <summary>Gets or sets the flight number.</summary>
    public string FlightNumber { get; set; } = string.Empty;

    /// <summary>Gets or sets the IATA code of the departure airport.</summary>
    public string DepartureAirport { get; set; } = string.Empty;

    /// <summary>Gets or sets the IATA code of the arrival airport.</summary>
    public string ArrivalAirport { get; set; } = string.Empty;

    /// <summary>Gets or sets the local departure date and time.</summary>
    public DateTimeOffset DepartureAt { get; set; }

    /// <summary>Gets or sets the local arrival date and time.</summary>
    public DateTimeOffset ArrivalAt { get; set; }

    /// <summary>Gets or sets the total flight duration.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Gets or sets the number of stopovers (0 = direct).</summary>
    public int Stops { get; set; }

    /// <summary>Gets or sets the cabin class (e.g., "ECONOMY", "BUSINESS").</summary>
    public string CabinClass { get; set; } = string.Empty;

    /// <summary>Gets or sets the total price including all fees.</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Gets or sets the ISO 4217 currency code for the price.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Gets or sets the number of available seats at this price.</summary>
    public int AvailableSeats { get; set; }
}

/// <summary>
/// Aggregated result of a transportation search, grouped by ranked options.
/// </summary>
public class TransportationSearchResult
{
    /// <summary>Gets or sets the cheapest flight option found.</summary>
    public FlightOffer? Cheapest { get; set; }

    /// <summary>Gets or sets the fastest flight option found.</summary>
    public FlightOffer? Fastest { get; set; }

    /// <summary>Gets or sets the most comfortable flight option found (fewest stops + best cabin).</summary>
    public FlightOffer? MostComfortable { get; set; }

    /// <summary>Gets or sets all flight offers returned by the search.</summary>
    public IReadOnlyList<FlightOffer> AllOffers { get; set; } = [];
}
