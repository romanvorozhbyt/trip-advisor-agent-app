using System.ComponentModel.DataAnnotations;

namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for the AirLabs aviation data API.
/// Obtain a free key at https://airlabs.co/signup
/// Free tier: 1,000 req/month, supports dep_iata + arr_iata filtering,
/// schedules show next ~10 hours only.
/// </summary>
public class AirLabsOptions
{
    public const string SectionName = "AirLabs";

    /// <summary>Gets or sets the AirLabs API key.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the base URL of the AirLabs API.</summary>
    public string BaseUrl { get; set; } = "https://airlabs.co/api/v9";
}
