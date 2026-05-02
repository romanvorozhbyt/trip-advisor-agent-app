using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Infrastructure.Configuration;

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

        // Accepts an expired (but otherwise valid) JWT so the client can rotate its token
        // without being forced to re-authenticate via Google.
        group.MapPost("/refresh", async (
            HttpContext httpContext,
            IAuthService authService,
            IOptions<JwtOptions> jwtOptions,
            CancellationToken ct) =>
        {
            var token = httpContext.Request.Headers.Authorization.ToString();
            if (!token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Results.Unauthorized();

            token = token["Bearer ".Length..].Trim();

            var jwt = jwtOptions.Value;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));

            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = false,   // allow expired tokens
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = key,
            };

            ClaimsPrincipal principal;
            try
            {
                principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParams, out _);
            }
            catch (SecurityTokenException)
            {
                return Results.Unauthorized();
            }

            var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrWhiteSpace(userId))
                return Results.Unauthorized();

            try
            {
                var result = await authService.RefreshTokenAsync(userId, ct);
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
            catch (InvalidOperationException)
            {
                return Results.Unauthorized();
            }
        });
    }

    private record GoogleSignInRequest(string IdToken);
}
