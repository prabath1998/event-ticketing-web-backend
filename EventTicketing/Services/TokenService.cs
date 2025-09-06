using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using EventTicketing.Entities;
using EventTicketing.Data;
using EventTicketing.Services;

namespace EventTicketing.Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        public TokenService(IConfiguration config, AppDbContext db)
        {
            _config = config;
            _db = db;
        }

        public async Task<TokenPair> CreateAsync(User user, IEnumerable<string> roles, CancellationToken ct = default)
        {
            var jwt = _config.GetSection("Jwt");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim("given_name", user.FirstName),
                new Claim("family_name", user.LastName)
            };
           
            claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["AccessTokenMinutes"]!));

            var token = new JwtSecurityToken(
                issuer: jwt["Issuer"],
                audience: jwt["Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expires,
                signingCredentials: creds
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            var refreshToken = GenerateRefreshToken();
            _db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(jwt["RefreshTokenDays"]!))
            });
            await _db.SaveChangesAsync(ct);

            return new TokenPair
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAtUtc = expires
            };
        }

        public string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            Random.Shared.NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
