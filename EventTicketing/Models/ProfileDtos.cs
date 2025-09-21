namespace EventTicketing.Models;

public record MeDto(long Id, string Email, string FirstName, string LastName);

public record ChangePasswordDto(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);