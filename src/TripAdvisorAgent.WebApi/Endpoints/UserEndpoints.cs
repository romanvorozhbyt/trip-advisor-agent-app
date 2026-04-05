using TripAdvisorAgent.Core.Interfaces;

namespace TripAdvisorAgent.WebApi.Endpoints;

/// <summary>
/// Maps user-related HTTP endpoints.
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();

        group.MapGet("/", async (IUserService userService, CancellationToken ct) =>
        {
            var users = await userService.GetAllAsync(ct);
            return Results.Ok(users);
        });

        group.MapGet("/{id:guid}", async (Guid id, IUserService userService, CancellationToken ct) =>
        {
            var user = await userService.GetByIdAsync(id, ct);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        });

        group.MapPost("/", async (CreateUserRequest request, IUserService userService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.DisplayName) || string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { error = "DisplayName and Email are required." });

            var user = await userService.CreateAsync(request.DisplayName, request.Email, ct);
            return Results.Created($"/api/users/{user.Id}", user);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IUserService userService, CancellationToken ct) =>
        {
            return await userService.DeleteAsync(id, ct)
                ? Results.NoContent()
                : Results.NotFound();
        });
    }

    private record CreateUserRequest(string DisplayName, string Email);
}
