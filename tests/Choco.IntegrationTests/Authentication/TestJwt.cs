using System.Security.Cryptography;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Choco.IntegrationTests.Authentication;

internal static class TestJwt
{
    public const string Issuer = "https://clerk.example.com";
    public const string AuthorizedParty = "https://app.example.com";

    public static readonly RsaSecurityKey SigningKey = new(RSA.Create(2048)) { KeyId = "signing-key-id" };

    public static string Create(
        DateTimeOffset issuedAt,
        string issuer = Issuer,
        string? authorizedParty = AuthorizedParty,
        SecurityKey? signingKey = null)
    {
        Dictionary<string, object> claims = new()
        {
            [JwtRegisteredClaimNames.Sub] = "identity-id",
            [JwtRegisteredClaimNames.Sid] = "session-id"
        };

        if (authorizedParty is not null)
        {
            claims[JwtRegisteredClaimNames.Azp] = authorizedParty;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Claims = claims,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = issuedAt.AddMinutes(1).UtcDateTime,
            SigningCredentials = new SigningCredentials(signingKey ?? SigningKey, SecurityAlgorithms.RsaSha256)
        });
    }
}
