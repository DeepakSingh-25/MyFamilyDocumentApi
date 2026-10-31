using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Documents.Core.Entities;
using Documents.Core.Interfaces;
using Documents.Core.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Documents.Infrastructure.Services
{
    public class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
    {
        /// <summary>JWT claim jo user ka PasswordVersion le jata hai (password badalne par purane token band).</summary>
        public const string PasswordVersionClaim = "pwv";

        public (string Token, DateTime ExpiresOn) CreateToken(UserEntity user)
        {
            var jwt = options.Value;
            var now = clock.GetUtcNow().UtcDateTime;
            var expires = now.AddMinutes(jwt.ExpiryMinutes);

            Claim[] claims =
            [
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Email, user.Email),
                new(JwtRegisteredClaimNames.Name, user.FullName),
                new(PasswordVersionClaim, user.PasswordVersion.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            ];

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey));
            var token = new JwtSecurityToken(
                issuer: jwt.Issuer,
                audience: jwt.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

            return (new JwtSecurityTokenHandler().WriteToken(token), expires);
        }
    }
}
