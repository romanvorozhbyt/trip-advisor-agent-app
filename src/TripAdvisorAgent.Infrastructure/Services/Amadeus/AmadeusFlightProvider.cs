using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;
using TripAdvisorAgent.Infrastructure.Configuration;
using TripAdvisorAgent.Infrastructure.Services.Amadeus.Models;

namespace TripAdvisorAgent.Infrastructure.Services.Amadeus;

/// <summary>
/// Flight search provider backed by the Amadeus Travel APIs (https://developers.amadeus.com).
/// Returns priced booking offers with seat availability and cabin class.
/// Requires valid <see cref="AmadeusOptions"/> OAuth credentials.
/// </summary>
public class AmadeusFlightProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<AmadeusOptions> options,
    ILogger<AmadeusFlightProvider> logger) : IFlightSearchProvider
{
    private readonly AmadeusOptions _options = options.Value;

    // Simple in-memory token cache — safe for singleton lifetime.
    private string? _accessToken;
    private DateTimeOffset _tokenExpiry = DateTimeOffset.MinValue;

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
        var token = await GetAccessTokenAsync(cancellationToken);

        using var client = httpClientFactory.CreateClient("amadeus");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"{_options.BaseUrl}/v2/shopping/flight-offers" +
                  $"?originLocationCode={Uri.EscapeDataString(originCode.ToUpperInvariant())}" +
                  $"&destinationLocationCode={Uri.EscapeDataString(destinationCode.ToUpperInvariant())}" +
                  $"&departureDate={departureDate:yyyy-MM-dd}" +
                  $"&adults={adults}" +
                  $"&currencyCode={Uri.EscapeDataString(currency.ToUpperInvariant())}" +
                  $"&max={maxResults}";

        logger.LogInformation("Searching Amadeus flights: {Origin} → {Destination} on {Date}",
            originCode, destinationCode, departureDate);

        var response = await client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var apiResponse = JsonSerializer.Deserialize<AmadeusFlightOffersResponse>(json, JsonOptions);
        var offers = MapToOffers(apiResponse?.Data ?? []);

        return BuildResult(offers);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _tokenExpiry)
            return _accessToken;

        using var client = httpClientFactory.CreateClient("amadeus");
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"]    = "client_credentials",
            ["client_id"]     = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        var response = await client.PostAsync($"{_options.BaseUrl}/v1/security/oauth2/token", body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var token = JsonSerializer.Deserialize<AmadeusTokenResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Amadeus token response could not be parsed.");

        _accessToken = token.AccessToken;
        _tokenExpiry = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 30);

        logger.LogDebug("Amadeus access token refreshed, expires in {Seconds}s.", token.ExpiresIn);
        return _accessToken;
    }

    private static IReadOnlyList<FlightOffer> MapToOffers(List<AmadeusFlightOffer> data)
    {
        var offers = new List<FlightOffer>(data.Count);
        foreach (var item in data)
        {
            try
            {
                var offer = MapSingleOffer(item);
                if (offer is not null)
                    offers.Add(offer);
            }
            catch (Exception)
            {
                // Skip malformed entries rather than failing the whole search.
            }
        }
        return offers;
    }

    private static FlightOffer? MapSingleOffer(AmadeusFlightOffer item)
    {
        var itinerary = item.Itineraries?.FirstOrDefault();
        var segments  = itinerary?.Segments;
        if (segments is null or { Count: 0 })
            return null;

        var first = segments[0];
        var last  = segments[^1];

        if (first.Departure is null || last.Arrival is null)
            return null;

        var duration = ParseIsoDuration(itinerary?.Duration ?? "PT0H");

        decimal.TryParse(
            item.Price?.Total,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var totalPrice);

        // Amadeus uses "iataCode" (compound camelCase), not "iata"
        var cabinClass = item.TravelerPricings
            ?.FirstOrDefault()
            ?.FareDetailsBySegment
            ?.FirstOrDefault()
            ?.Cabin ?? "ECONOMY";

        DateTimeOffset.TryParse(first.Departure.At, out var departureAt);
        DateTimeOffset.TryParse(last.Arrival.At,    out var arrivalAt);

        var carrierCode  = first.CarrierCode  ?? string.Empty;
        var flightNumber = first.Number       ?? string.Empty;

        return new FlightOffer
        {
            CarrierCode      = carrierCode,
            CarrierName      = carrierCode,   // name lookup would require Amadeus Airlines reference endpoint
            FlightNumber     = $"{carrierCode}{flightNumber}",
            DepartureAirport = first.Departure.IataCode ?? string.Empty,
            ArrivalAirport   = last.Arrival.IataCode    ?? string.Empty,
            DepartureAt      = departureAt,
            ArrivalAt        = arrivalAt,
            Duration         = duration,
            Stops            = segments.Count - 1,
            CabinClass       = cabinClass,
            TotalPrice       = totalPrice,
            Currency         = item.Price?.Currency ?? "USD",
            AvailableSeats   = item.NumberOfBookableSeats ?? 9
        };
    }

    /// <summary>Parses an ISO 8601 duration string (e.g. "PT2H30M", "PT10H", "PT45M").</summary>
    private static TimeSpan ParseIsoDuration(string iso)
    {
        var span = TimeSpan.Zero;
        var s = iso.AsSpan().TrimStart('P');
        if (s.StartsWith("T")) s = s[1..];

        var remaining = s;
        var hIdx = remaining.IndexOf('H');
        if (hIdx >= 0)
        {
            if (int.TryParse(remaining[..hIdx], out var hours))
                span += TimeSpan.FromHours(hours);
            remaining = remaining[(hIdx + 1)..];
        }

        var mIdx = remaining.IndexOf('M');
        if (mIdx >= 0 && int.TryParse(remaining[..mIdx], out var minutes))
            span += TimeSpan.FromMinutes(minutes);

        return span;
    }

    private static TransportationSearchResult BuildResult(IReadOnlyList<FlightOffer> offers)
    {
        if (offers.Count == 0)
            return new TransportationSearchResult { AllOffers = offers };

        var cheapest = offers.MinBy(o => o.TotalPrice);
        var fastest  = offers.MinBy(o => o.Duration);
        var mostComfortable = offers
            .OrderBy(o => o.Stops)
            .ThenByDescending(o => CabinScore(o.CabinClass))
            .ThenBy(o => o.Duration)
            .First();

        return new TransportationSearchResult
        {
            Cheapest        = cheapest,
            Fastest         = fastest,
            MostComfortable = mostComfortable,
            AllOffers       = offers
        };
    }

    private static int CabinScore(string cabin) => cabin.ToUpperInvariant() switch
    {
        "FIRST"           => 4,
        "BUSINESS"        => 3,
        "PREMIUM_ECONOMY" => 2,
        _                 => 1
    };
}
