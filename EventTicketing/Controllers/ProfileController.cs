using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.Entities;
using EventTicketing.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace EventTicketing.Controllers;

[ApiController]
[Route("me")]
[Authorize] 
public class ProfileController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;

    public ProfileController(AppDbContext db, IPasswordHasher<User> hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }

    [HttpGet]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (!TryGetUserId(out var uid)) return Unauthorized();
        var u = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct);
        if (u is null) return NotFound();

        return Ok(new MeDto(u.Id, u.Email, u.FirstName ?? "", u.LastName ?? ""));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var uid)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 8)
            return BadRequest("New password must be at least 8 characters.");

        if (dto.NewPassword != dto.ConfirmNewPassword)
            return BadRequest("New password and confirmation do not match.");

        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct);
        if (user is null) return NotFound();
       
        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, dto.CurrentPassword);
        if (verify == PasswordVerificationResult.Failed)
            return BadRequest("Current password is incorrect.");
       
        user.PasswordHash = _hasher.HashPassword(user, dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Password updated successfully." });
    }
}
