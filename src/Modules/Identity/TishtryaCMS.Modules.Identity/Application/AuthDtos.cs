namespace TishtryaCMS.Modules.Identity.Application;

public sealed record RegisterRequest(string Email, string Password);

public sealed record VerifyOtpRequest(
    string Email,
    string Code,
    string? Purpose = "EmailVerification");

public sealed record ResendOtpRequest(
    string Email,
    string? Purpose = "EmailVerification");

public sealed record RequestLoginOtpRequest(string Email);

public sealed record LoginOtpRequest(string Email, string Code);

public sealed record VerifyOtpResponse(
    bool Success,
    string? AccessToken,
    DateTime? ExpiresAtUtc,
    string? Email,
    string? DisplayName,
    string? Role,
    Guid? UserId);
