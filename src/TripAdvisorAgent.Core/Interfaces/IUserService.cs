using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Core.Interfaces;

/// <summary>
/// Manages user data persistence.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Gets a user by ID.
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all users.
    /// </summary>
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new user.
    /// </summary>
    Task<User> CreateAsync(string displayName, string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds or creates a user by their Google account ID.
    /// </summary>
    Task<User> GetOrCreateByGoogleAsync(string googleId, string email, string displayName, string? pictureUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a user by ID. Returns true if the user was found and deleted.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
