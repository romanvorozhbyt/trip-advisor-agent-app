using System.ComponentModel.DataAnnotations;

namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for JWT token generation.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Secret key used to sign JWT tokens. Must be at least 32 characters.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Token issuer (typically the API URL).
    /// </summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Token audience (typically the client app URL).
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration in minutes.
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;
}
