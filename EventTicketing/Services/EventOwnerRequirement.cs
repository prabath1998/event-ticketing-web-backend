using Microsoft.AspNetCore.Authorization;

namespace EventTicketing.Services
{
    public class EventOwnerRequirement : IAuthorizationRequirement { }
}