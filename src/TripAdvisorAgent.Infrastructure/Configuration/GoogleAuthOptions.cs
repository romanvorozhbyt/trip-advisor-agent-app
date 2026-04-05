using System.ComponentModel.DataAnnotations;

namespace TripAdvisorAgent.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed configuration for Google OAuth authentication.
/// </summary>
public class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";

    /// <summary>
    /// Google OAuth Client ID from Google Cloud Console.
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;
}
