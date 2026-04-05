using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TripAdvisorAgent.Core.Interfaces;
using TripAdvisorAgent.Core.Models;
using TripAdvisorAgent.Infrastructure.Configuration;

namespace TripAdvisorAgent.Infrastructure.Services;

/// <summary>
/// Validates Google ID tokens and issues JWT access tokens.
/// </summary>
public class AuthService(
    IUserService userService,
    IOptions<GoogleAuthOptions> googleOptions,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    public async Task<AuthResult> SignInWithGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [googleOptions.Value.ClientId],
        });

        var user = await userService.GetOrCreateByGoogleAsync(
            payload.Subject,
            payload.Email,
            payload.Name,
            payload.Picture,
            cancellationToken);

        var (accessToken, expiresAt) = GenerateJwt(user);

        return new AuthResult(accessToken, expiresAt, user);
    }

    private (string Token, DateTime ExpiresAt) GenerateJwt(User user)
    {
        var jwt = jwtOptions.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(jwt.ExpirationMinutes);

        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: jwt.Issuer,
            audience: jwt.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
