namespace TishtryaCMS.Modules.Identity.Application;

public sealed record LoginRequest(string Email, string Password, string? CaptchaToken = null);
