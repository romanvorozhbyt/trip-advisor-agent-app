using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Handles authentication via external identity providers.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Validates a Google ID token, creates or retrieves the user, and returns a JWT + user info.
    /// </summary>
    Task<AuthResult> SignInWithGoogleAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a new JWT for an already-authenticated user identified by their user ID.
    /// </summary>
    Task<AuthResult> RefreshTokenAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a successful authentication operation.
/// </summary>
public record AuthResult(string AccessToken, DateTime ExpiresAt, User User);
