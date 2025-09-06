using EventTicketing.Entities;

namespace EventTicketing.Services
{
    public class TokenPair
    {
        public string AccessToken { get; set; } = default!;
        public string RefreshToken { get; set; } = default!;
        public DateTime ExpiresAtUtc { get; set; }
    }

    public interface ITokenService
    {
        Task<TokenPair> CreateAsync(User user, IEnumerable<string> roles, CancellationToken ct = default);
        string GenerateRefreshToken();
    }
}