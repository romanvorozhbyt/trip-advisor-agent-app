using Microsoft.EntityFrameworkCore;
using TripAdvisorAgent.Core.Models;

namespace TripAdvisorAgent.Infrastructure.Data;

/// <summary>
/// EF Core database context for SQLite-backed user data.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
}
