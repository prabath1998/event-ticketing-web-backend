using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EventTicketing.Data;
using EventTicketing.Services;

namespace JWTAuth.Services
{
    public class EventOwnerHandler : AuthorizationHandler<EventOwnerRequirement>
    {
        private readonly AppDbContext _db;
        private readonly IHttpContextAccessor _http;

        public EventOwnerHandler(AppDbContext db, IHttpContextAccessor http)
        {
            _db = db; _http = http;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, EventOwnerRequirement requirement)
        {
            // user id from token
            var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier) // fallback
                            ?? context.User.FindFirstValue(ClaimTypes.Name);
            // better: use sub
            userIdStr ??= context.User.FindFirstValue("sub");
            if (userIdStr == null || !long.TryParse(userIdStr, out var userId))
                return;

            // get eventId from route: /organizer/events/{id}
            var httpContext = (_http?.HttpContext)!;
            if (!httpContext.Request.RouteValues.TryGetValue("id", out var idObj) ||
                !long.TryParse(idObj?.ToString(), out var eventId))
                return;

            // organizer profile id of current user
            var organizer = await _db.OrganizerProfiles.FirstOrDefaultAsync(x => x.UserId == userId);
            if (organizer == null) return;

            var isOwner = await _db.Events.AnyAsync(e => e.Id == eventId && e.OrganizerId == organizer.Id);
            if (isOwner) context.Succeed(requirement);
        }
    }
}