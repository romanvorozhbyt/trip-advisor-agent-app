using TripAdvisorAgent.Core.Interfaces;

namespace TripAdvisorAgent.WebApi.Endpoints;

/// <summary>
/// Maps authentication-related HTTP endpoints.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/google", async (GoogleSignInRequest request, IAuthService authService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.IdToken))
                return Results.BadRequest(new { error = "IdToken is required." });

            try
            {
                var result = await authService.SignInWithGoogleAsync(request.IdToken, ct);
                return Results.Ok(new
                {
                    result.AccessToken,
                    result.ExpiresAt,
                    User = new
                    {
                        result.User.Id,
                        result.User.DisplayName,
                        result.User.Email,
                        result.User.PictureUrl,
                    },
                });
            }
            catch (Google.Apis.Auth.InvalidJwtException)
            {
                return Results.Unauthorized();
            }
        });
    }

    private record GoogleSignInRequest(string IdToken);
}
