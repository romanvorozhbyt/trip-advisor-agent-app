using System.ComponentModel.DataAnnotations;

namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the Amadeus Travel APIs.
/// Obtain credentials at https://developers.amadeus.com/
/// </summary>
public class AmadeusOptions
{
    public const string SectionName = "Amadeus";

    /// <summary>Gets or sets the Amadeus API client ID (OAuth2 client_id).</summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Gets or sets the Amadeus API client secret (OAuth2 client_secret).</summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Gets or sets the base URL of the Amadeus API environment.</summary>
    /// <remarks>
    /// Use "https://test.api.amadeus.com" for sandbox and
    /// "https://api.amadeus.com" for production.
    /// </remarks>
    public string BaseUrl { get; set; } = "https://test.api.amadeus.com";
}
