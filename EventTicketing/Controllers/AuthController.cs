using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using EventTicketing.Data;
using EventTicketing.Entities;
using EventTicketing.Models;
using EventTicketing.Services;
using EventTicketing.Services;

namespace EventTicketing.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IPasswordHasher<User> _hasher;
        private readonly ITokenService _tokens;

        public AuthController(AppDbContext db, IPasswordHasher<User> hasher, ITokenService tokens)
        {
            _db = db; _hasher = hasher; _tokens = tokens;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
        {
            if (await _db.Users.AnyAsync(u => u.Email == dto.Email, ct))
                return BadRequest("Email already registered.");

            var user = new User
            {
                Email = dto.Email,
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                IsActive = true
            };
            user.PasswordHash = _hasher.HashPassword(user, dto.Password);

            await _db.Users.AddAsync(user, ct);
            await _db.SaveChangesAsync(ct);

            var roleName = string.IsNullOrWhiteSpace(dto.Role) ? "Customer" : dto.Role!;
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, ct);
            if (role == null) return BadRequest($"Role '{roleName}' not found.");
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            await _db.SaveChangesAsync(ct);

            var roles = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
                                           .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                                           .ToListAsync(ct);
            var pair = await _tokens.CreateAsync(user, roles, ct);
            return Ok(pair);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, ct);
            if (user == null || !user.IsActive)
                return Unauthorized("Invalid credentials.");

            var res = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);
            if (res == PasswordVerificationResult.Failed)
                return Unauthorized("Invalid credentials.");

            var roles = await _db.UserRoles.Where(ur => ur.UserId == user.Id)
                                           .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                                           .ToListAsync(ct);

            var pair = await _tokens.CreateAsync(user, roles, ct);
            return Ok(pair);
        }

        public class RefreshDto { public string RefreshToken { get; set; } = default!; }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh(RefreshDto dto, CancellationToken ct)
        {
            var rt = await _db.RefreshTokens.Include(x => x.User)
                         .FirstOrDefaultAsync(x => x.Token == dto.RefreshToken, ct);
            if (rt == null || rt.RevokedAt != null || rt.ExpiresAt < DateTime.UtcNow)
                return Unauthorized("Invalid refresh token.");

            rt.RevokedAt = DateTime.UtcNow;

            var roles = await _db.UserRoles.Where(ur => ur.UserId == rt.UserId)
                                           .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                                           .ToListAsync(ct);

            var pair = await _tokens.CreateAsync(rt.User, roles, ct);
            await _db.SaveChangesAsync(ct);
            return Ok(pair);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout(RefreshDto dto, CancellationToken ct)
        {
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(x => x.Token == dto.RefreshToken, ct);
            if (rt != null) { rt.RevokedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
            return Ok();
        }
    }
}
