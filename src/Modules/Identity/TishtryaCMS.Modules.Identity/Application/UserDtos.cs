namespace TishtryaCMS.Modules.Identity.Application;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    string Role);

public sealed record UpdateUserProfileRequest(
    string Email,
    string DisplayName,
    bool? IsActive);

public sealed record ChangeUserRoleRequest(string Role);

public sealed record SetUserPasswordRequest(string Password);

public sealed record ChangeOwnPasswordRequest(string CurrentPassword, string NewPassword);

public sealed record UpdateOwnProfileRequest(string Email, string DisplayName);
