namespace TripAdvisorAgent.Core.Models;

/// <summary>
/// Represents an application user.
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Google account subject identifier (unique per Google user).
    /// </summary>
    public string? GoogleId { get; set; }

    /// <summary>
    /// URL to the user's Google profile picture.
    /// </summary>
    public string? PictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
