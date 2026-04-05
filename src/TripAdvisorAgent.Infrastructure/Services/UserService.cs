using Microsoft.EntityFrameworkCore;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;
using TripAdvisorAgent.Infrastructure.Data;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// SQLite-backed user service using EF Core.
/// </summary>
public class UserService(AppDbContext db) : IUserService
{
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.Users.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync(cancellationToken);

    public async Task<User> CreateAsync(string displayName, string email, CancellationToken cancellationToken = default)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = displayName,
            Email = email,
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<User> GetOrCreateByGoogleAsync(string googleId, string email, string displayName, string? pictureUrl, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);

        if (user is not null)
        {
            // Update profile info on each sign-in
            user.DisplayName = displayName;
            user.Email = email;
            user.PictureUrl = pictureUrl;
            await db.SaveChangesAsync(cancellationToken);
            return user;
        }

        user = new User
        {
            Id = Guid.NewGuid(),
            GoogleId = googleId,
            DisplayName = displayName,
            Email = email,
            PictureUrl = pictureUrl,
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.FindAsync([id], cancellationToken);
        if (user is null)
            return false;

        db.Users.Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
