using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;
using TripAdvisorAgent.Infrastructure.Configuration;
using TripAdvisorAgent.Infrastructure.Services.AirLabs.Models;

namespace TripAdvisorAgent.Infrastructure.Services.AirLabs;

/// <summary>
/// Flight search provider backed by the AirLabs /schedules endpoint.
/// Free tier: 1,000 req/month, supports dep_iata + arr_iata filtering.
/// Note: the schedules endpoint only returns flights in the next ~10 hours,
/// so results are only available for today's upcoming departures.
/// </summary>
public class AirLabsFlightProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AirLabsOptions> options,
    ILogger<AirLabsFlightProvider> logger) : IFlightSearchProvider
{
    private readonly AirLabsOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <inheritdoc />
    public async Task<TransportationSearchResult> SearchFlightsAsync(
        string originCode,
        string destinationCode,
        DateOnly departureDate,
        int adults = 1,
        string currency = "USD",
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        using var client = httpClientFactory.CreateClient("airlabs");

        // Mask key for safe logging — show first 6 chars only
        var maskedKey = _options.ApiKey.Length > 6
            ? _options.ApiKey[..6] + new string('*', _options.ApiKey.Length - 6)
            : "(empty)";

        var url = $"{_options.BaseUrl}/schedules" +
                  $"?dep_iata={Uri.EscapeDataString(originCode.ToUpperInvariant())}" +
                  $"&arr_iata={Uri.EscapeDataString(destinationCode.ToUpperInvariant())}" +
                  $"&api_key={Uri.EscapeDataString(_options.ApiKey)}";

        logger.LogInformation(
            "Searching AirLabs schedules: {Origin} → {Destination} for {Date} | key={MaskedKey}",
            originCode, destinationCode, departureDate, maskedKey);

        var response = await client.GetAsync(url, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "AirLabs returned {StatusCode}. Response: {Body}",
                (int)response.StatusCode, json);
            return new TransportationSearchResult { AllOffers = [] };
        }

        logger.LogDebug("AirLabs raw response: {Body}", json);

        var envelope = JsonSerializer.Deserialize<AirLabsEnvelope<AirLabsScheduleEntry>>(json, JsonOptions);

        if (envelope?.Error is not null)
        {
            logger.LogError("AirLabs API error: {Message}", envelope.Error.Message);
            return new TransportationSearchResult { AllOffers = [] };
        }

        var offers = MapToOffers(envelope?.Response ?? [], departureDate);
        logger.LogInformation("AirLabs returned {Count} flights.", offers.Count);

        return BuildResult(offers);
    }

    private static IReadOnlyList<FlightOffer> MapToOffers(
        List<AirLabsScheduleEntry> data, DateOnly requestedDate)
    {
        var offers = new List<FlightOffer>(data.Count);
        foreach (var entry in data)
        {
            try
            {
                var offer = MapSingleEntry(entry, requestedDate);
                if (offer is not null)
                    offers.Add(offer);
            }
            catch (Exception)
            {
                // Skip malformed entries
            }
        }
        return offers;
    }

    private static FlightOffer? MapSingleEntry(AirLabsScheduleEntry e, DateOnly requestedDate)
    {
        if (string.IsNullOrEmpty(e.DepIata) || string.IsNullOrEmpty(e.ArrIata))
            return null;

        var departureAt = ResolveTime(e.DepActual ?? e.DepEstimated ?? e.DepTime, requestedDate);
        var duration    = e.Duration.HasValue ? TimeSpan.FromMinutes(e.Duration.Value) : TimeSpan.Zero;
        var arrivalAt   = departureAt != default && duration > TimeSpan.Zero
            ? departureAt + duration
            : ResolveTime(e.ArrActual ?? e.ArrEstimated ?? e.ArrTime, requestedDate);

        return new FlightOffer
        {
            CarrierCode      = e.AirlineIata ?? string.Empty,
            CarrierName      = e.AirlineIata ?? string.Empty, // name not in free tier; IATA is recognisable enough
            FlightNumber     = e.FlightIata  ?? e.FlightNumber ?? string.Empty,

            DepartureAirport = e.DepIata,
            ArrivalAirport   = e.ArrIata,

            DepartureAt      = departureAt,
            ArrivalAt        = arrivalAt,
            Duration         = duration,

            Stops            = 0,           // /schedules returns per-segment direct flights
            CabinClass       = string.Empty,
            TotalPrice       = 0,           // AirLabs does not provide booking prices
            Currency         = string.Empty,
            AvailableSeats   = 0,
        };
    }

    /// <summary>
    /// Parses times in "yyyy-MM-dd HH:mm" or "HH:mm" format.
    /// When only a time component is present the supplied requestedDate is used as the date.
    /// </summary>
    private static DateTimeOffset ResolveTime(string? raw, DateOnly requestedDate)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return default;

        // Full datetime
        if (DateTimeOffset.TryParseExact(raw, "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var full))
            return full;

        // Time-only (HH:mm)
        if (TimeOnly.TryParseExact(raw, "HH:mm", out var time))
            return new DateTimeOffset(requestedDate.ToDateTime(time), TimeSpan.Zero);

        return default;
    }

    private static TransportationSearchResult BuildResult(IReadOnlyList<FlightOffer> offers)
    {
        if (offers.Count == 0)
            return new TransportationSearchResult { AllOffers = offers };

        var fastest        = offers.MinBy(o => o.Duration);
        var mostComfortable = offers
            .OrderBy(o => o.Stops)
            .ThenByDescending(o => o.Duration) // longer = wider body aircraft usually
            .FirstOrDefault();

        return new TransportationSearchResult
        {
            Cheapest        = null,            // price data not available from AirLabs
            Fastest         = fastest,
            MostComfortable = mostComfortable,
            AllOffers       = offers,
        };
    }
}
