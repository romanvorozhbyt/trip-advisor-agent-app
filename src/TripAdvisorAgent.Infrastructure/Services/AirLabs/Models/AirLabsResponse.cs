using System.Text.Json.Serialization;

namespace TripAdvisorAgent.Infrastructure.Services.AirLabs.Models;

/// <summary>
/// Standard AirLabs API envelope — all endpoints wrap results in "response"
/// and surface errors in "error".
/// </summary>
internal record AirLabsEnvelope<T>(
    [property: JsonPropertyName("response")] List<T>? Response,
    [property: JsonPropertyName("error")]    AirLabsApiError? Error);

internal record AirLabsApiError(
    [property: JsonPropertyName("message")] string? Message);

/// <summary>
/// One entry from the AirLabs /schedules endpoint.
/// Fields marked "Available in the Free plan" are guaranteed present;
/// others may be null on the free tier.
/// </summary>
internal record AirLabsScheduleEntry(
    // Airline — only IATA code is free
    [property: JsonPropertyName("airline_iata")]  string? AirlineIata,
    [property: JsonPropertyName("airline_icao")]  string? AirlineIcao,

    // Flight
    [property: JsonPropertyName("flight_iata")]   string? FlightIata,   // free
    [property: JsonPropertyName("flight_icao")]   string? FlightIcao,
    [property: JsonPropertyName("flight_number")] string? FlightNumber, // free

    // Departure
    [property: JsonPropertyName("dep_iata")]      string? DepIata,      // free
    [property: JsonPropertyName("dep_icao")]      string? DepIcao,
    [property: JsonPropertyName("dep_terminal")]  string? DepTerminal,
    [property: JsonPropertyName("dep_gate")]      string? DepGate,
    [property: JsonPropertyName("dep_time")]      string? DepTime,      // local, free — format "yyyy-MM-dd HH:mm"
    [property: JsonPropertyName("dep_time_ts")]   long?   DepTimeTs,    // UNIX timestamp
    [property: JsonPropertyName("dep_time_utc")]  string? DepTimeUtc,
    [property: JsonPropertyName("dep_estimated")] string? DepEstimated,
    [property: JsonPropertyName("dep_actual")]    string? DepActual,
    [property: JsonPropertyName("dep_delayed")]   int?    DepDelayed,

    // Arrival
    [property: JsonPropertyName("arr_iata")]      string? ArrIata,      // free
    [property: JsonPropertyName("arr_icao")]      string? ArrIcao,
    [property: JsonPropertyName("arr_terminal")]  string? ArrTerminal,
    [property: JsonPropertyName("arr_gate")]      string? ArrGate,
    [property: JsonPropertyName("arr_baggage")]   string? ArrBaggage,
    [property: JsonPropertyName("arr_time")]      string? ArrTime,      // local, free
    [property: JsonPropertyName("arr_time_ts")]   long?   ArrTimeTs,
    [property: JsonPropertyName("arr_time_utc")]  string? ArrTimeUtc,
    [property: JsonPropertyName("arr_estimated")] string? ArrEstimated,
    [property: JsonPropertyName("arr_actual")]    string? ArrActual,
    [property: JsonPropertyName("arr_delayed")]   int?    ArrDelayed,

    // Meta
    [property: JsonPropertyName("status")]        string? Status,
    [property: JsonPropertyName("duration")]      int?    Duration);    // minutes
