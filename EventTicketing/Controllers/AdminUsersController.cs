using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EventTicketing.Data;
using EventTicketing.DTOs;
using EventTicketing.Services.Audit;

namespace EventTicketing.Controllers;

[ApiController]
[Route("admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditService _audit;
    public AdminUsersController(AppDbContext db, IAuditService audit) { _db = db; _audit = audit; }

    private bool TryGetUserId(out long userId)
    {
        var raw = User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(raw, out userId);
    }
    
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, [FromQuery] string? role,
        [FromQuery] bool? active, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize, 1, 100);

        var users = _db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var s = q.Trim();
            users = users.Where(u =>
                EF.Functions.Like(u.Email, $"%{s}%") ||
                EF.Functions.Like(u.FirstName, $"%{s}%") ||
                EF.Functions.Like(u.LastName, $"%{s}%"));
        }

        if (active.HasValue) users = users.Where(u => u.IsActive == active.Value);

        var query = users
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new {
                u.Id, u.Email, u.FirstName, u.LastName, u.IsActive, u.CreatedAt,
                Roles = u.UserRoles.Select(ur => ur.Role.Name).ToArray()
            });

        if (!string.IsNullOrWhiteSpace(role))
            query = query.Where(x => x.Roles.Contains(role));

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page-1)*pageSize).Take(pageSize)
            .Select(x => new UserListItemDto(x.Id, x.Email, x.FirstName, x.LastName, x.IsActive, x.Roles, x.CreatedAt))
            .ToListAsync(ct);

        return Ok(new { page, pageSize, total, items });
    }
 
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> UpdateStatus(long id, [FromBody] UpdateUserStatusDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (u is null) return NotFound();

        u.IsActive = dto.IsActive;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(adminId, "UserStatusUpdated", "User", id, new { dto.IsActive }, ct);
        return Ok(new { u.Id, u.IsActive });
    }
   
    [HttpPost("{id:long}/roles")]
    public async Task<IActionResult> ModifyRoles(long id, [FromBody] ModifyUserRolesDto dto, CancellationToken ct)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        var user = await _db.Users.Include(u => u.UserRoles).ThenInclude(ur => ur.Role).FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        
        var allRoles = await _db.Roles.ToDictionaryAsync(r => r.Name, r => r, ct);
      
        if (dto.Add is not null)
        {
            foreach (var r in dto.Add.Distinct())
            {
                if (!allRoles.TryGetValue(r, out var roleEntity)) continue;
                if (!user.UserRoles.Any(ur => ur.RoleId == roleEntity.Id))
                    _db.UserRoles.Add(new EventTicketing.Entities.UserRole { UserId = user.Id, RoleId = roleEntity.Id });
            }
        }
       
        if (dto.Remove is not null)
        {
            foreach (var r in dto.Remove.Distinct())
            {
                if (!allRoles.TryGetValue(r, out var roleEntity)) continue;
                var link = user.UserRoles.FirstOrDefault(ur => ur.RoleId == roleEntity.Id);
                if (link != null) _db.UserRoles.Remove(link);
            }
        }

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync(adminId, "UserRolesModified", "User", id, new { dto.Add, dto.Remove }, ct);
        return NoContent();
    }
}
