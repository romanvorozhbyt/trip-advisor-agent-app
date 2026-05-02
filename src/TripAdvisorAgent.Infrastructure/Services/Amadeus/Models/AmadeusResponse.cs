using System.Text.Json.Serialization;

namespace TripAdvisorAgent.Infrastructure.Services.Amadeus.Models;

internal record AmadeusFlightOffersResponse(
    [property: JsonPropertyName("data")] List<AmadeusFlightOffer>? Data);

internal record AmadeusFlightOffer(
    [property: JsonPropertyName("itineraries")]       List<AmadeusItinerary>?           Itineraries,
    [property: JsonPropertyName("price")]             AmadeusPrice?                     Price,
    [property: JsonPropertyName("travelerPricings")]  List<AmadeusTravelerPricing>?     TravelerPricings,
    [property: JsonPropertyName("numberOfBookableSeats")] int?                          NumberOfBookableSeats);

internal record AmadeusItinerary(
    [property: JsonPropertyName("duration")] string?              Duration,
    [property: JsonPropertyName("segments")] List<AmadeusSegment>? Segments);

internal record AmadeusSegment(
    [property: JsonPropertyName("departure")]    AmadeusLocation? Departure,
    [property: JsonPropertyName("arrival")]      AmadeusLocation? Arrival,
    [property: JsonPropertyName("carrierCode")]  string?          CarrierCode,
    [property: JsonPropertyName("number")]       string?          Number);

/// <summary>
/// Departure/arrival location in the Amadeus response.
/// Note: field is "iataCode" (camelCase compound), not "iata".
/// </summary>
internal record AmadeusLocation(
    [property: JsonPropertyName("iataCode")] string? IataCode,
    [property: JsonPropertyName("at")]       string? At);

internal record AmadeusPrice(
    [property: JsonPropertyName("total")]    string? Total,
    [property: JsonPropertyName("currency")] string? Currency);

internal record AmadeusTravelerPricing(
    [property: JsonPropertyName("fareDetailsBySegment")] List<AmadeusFareDetail>? FareDetailsBySegment);

internal record AmadeusFareDetail(
    [property: JsonPropertyName("cabin")] string? Cabin);

internal record AmadeusTokenResponse(
    [property: JsonPropertyName("access_token")] string  AccessToken,
    [property: JsonPropertyName("expires_in")]   int     ExpiresIn);
