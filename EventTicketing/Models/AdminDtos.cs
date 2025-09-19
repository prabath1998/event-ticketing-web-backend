namespace EventTicketing.DTOs;

public record UserListItemDto(
    long Id, string Email, string FirstName, string LastName, bool IsActive,
    string[] Roles, DateTime CreatedAt);

public record UpdateUserStatusDto(bool IsActive);

public record ModifyUserRolesDto(string[] Add, string[] Remove);

public record EventAdminListItemDto(
    long Id, long OrganizerId, string Title, string VenueName, string? City,
    DateTime StartTime, DateTime EndTime, string Status, DateTime CreatedAt);

public record ChangeEventStatusDto(string Status); 

public record DiscountAdminListItemDto(
    long Id, string Code, string Type, int Value, string Scope,
    long? TicketTypeId, bool IsActive, DateTime? StartsAt, DateTime? EndsAt, int UsedCount);

public record ToggleActiveDto(bool IsActive);

public record AdminOverviewDto(
    DateTime From, DateTime To,
    long TotalUsers, long ActiveUsers,
    long TotalEvents, long PublishedEvents,
    long TotalOrders, long PaidOrders,
    long TotalTicketsIssued,
    long RevenueCents);

public record AuditLogListItemDto(
    long Id, long ActorUserId, string Action, string EntityType, long? EntityId, DateTime CreatedAt);