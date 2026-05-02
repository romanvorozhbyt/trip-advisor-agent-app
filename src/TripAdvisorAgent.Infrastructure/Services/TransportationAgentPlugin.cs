using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.SemanticKernel;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Semantic Kernel plugin that acts as a dedicated transportation-search agent.
/// The "brain" (LLM) invokes these functions automatically when the user asks about
/// flights, travel options, prices, or routes.
/// </summary>
[Description("Searches for real-time transportation options such as flights between destinations.")]
public class TransportationAgentPlugin(ITransportationService transportationService)
{
    /// <summary>
    /// Searches for available flights between two airports and returns the cheapest,
    /// fastest, and most comfortable options.
    /// </summary>
    /// <param name="origin">IATA airport code for the origin city (e.g., JFK, LHR, SYD).</param>
    /// <param name="destination">IATA airport code for the destination city (e.g., CDG, DXB, NRT).</param>
    /// <param name="departureDate">
    /// Departure date in YYYY-MM-DD format (e.g., 2025-12-01).
    /// If the user mentions a relative date like "next Friday", convert it before calling.
    /// </param>
    /// <param name="adults">Number of adult passengers (default: 1).</param>
    /// <returns>
    /// A formatted summary of available flight options with schedules and durations.
    /// Pricing information is not available.
    /// </returns>
    [KernelFunction("search_flights")]
    [Description(
        "Search for available flights between two airports on a specific date. " +
        "Returns flight options with schedules and durations. Pricing is not available. " +
        "Use IATA airport codes (3-letter codes like JFK, LHR, CDG) for origin and destination.")]
    public async Task<string> SearchFlightsAsync(
        [Description("IATA airport code for the origin airport (e.g., JFK, LHR, CDG, SYD)")] string origin,
        [Description("IATA airport code for the destination airport (e.g., JFK, LHR, CDG, SYD)")] string destination,
        [Description("Departure date in YYYY-MM-DD format")] string departureDate,
        [Description("Number of adult passengers (default: 1)")] int adults = 1)
    {
        if (!DateOnly.TryParseExact(departureDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return $"Invalid departure date format '{departureDate}'. Please use YYYY-MM-DD.";
        }

        TransportationSearchResult result;
        try
        {
            result = await transportationService.SearchFlightsAsync(
                origin, destination, date, adults, "USD");
        }
        catch (HttpRequestException ex)
        {
            return $"Flight search is temporarily unavailable: {ex.Message}";
        }

        if (result.AllOffers.Count == 0)
        {
            return $"No flights found from {origin.ToUpperInvariant()} to {destination.ToUpperInvariant()} " +
                   $"on {departureDate} for {adults} passenger(s). " +
                   "Try different dates or nearby airports.";
        }

        return FormatSearchResult(result, origin, destination, departureDate, adults);
    }

    private static string FormatSearchResult(
        TransportationSearchResult result,
        string origin,
        string destination,
        string departureDate,
        int adults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"✈️ Flight results: {origin.ToUpperInvariant()} → {destination.ToUpperInvariant()}");
        sb.AppendLine($"   Date: {departureDate} | Passengers: {adults}");
        sb.AppendLine("   Note: Pricing information is not available.");
        sb.AppendLine();

        if (result.Fastest is not null)
        {
            AppendOffer(sb, "⚡ Fastest Option", result.Fastest);
        }

        if (result.MostComfortable is not null && result.MostComfortable != result.Fastest)
        {
            AppendOffer(sb, "🛋️ Most Comfortable Option", result.MostComfortable);
        }

        if (result.Cheapest is not null
            && result.Cheapest != result.Fastest
            && result.Cheapest != result.MostComfortable)
        {
            AppendOffer(sb, "🔀 Alternative Option", result.Cheapest);
        }

        sb.AppendLine($"   Total options found: {result.AllOffers.Count}");
        return sb.ToString().TrimEnd();
    }

    private static void AppendOffer(StringBuilder sb, string label, FlightOffer offer)
    {
        var stops = offer.Stops == 0 ? "Direct" : $"{offer.Stops} stop(s)";
        sb.AppendLine($"{label}:");
        sb.AppendLine($"   Flight:    {offer.FlightNumber} ({offer.CarrierName})");
        sb.AppendLine($"   Departs:   {offer.DepartureAt:HH:mm} ({offer.DepartureAirport})  →  Arrives: {offer.ArrivalAt:HH:mm} ({offer.ArrivalAirport})");
        sb.AppendLine($"   Duration:  {FormatDuration(offer.Duration)} | {stops}");
        if (!string.IsNullOrEmpty(offer.CabinClass))
            sb.AppendLine($"   Class:     {offer.CabinClass}");
        sb.AppendLine();
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.Hours > 0
            ? $"{duration.Hours}h {duration.Minutes}m"
            : $"{duration.Minutes}m";
    }
}
