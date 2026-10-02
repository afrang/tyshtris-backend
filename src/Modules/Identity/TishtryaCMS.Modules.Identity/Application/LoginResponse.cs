namespace TishtryaCMS.Modules.Identity.Application;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string Email,
    string DisplayName,
    string Role,
    Guid UserId);
