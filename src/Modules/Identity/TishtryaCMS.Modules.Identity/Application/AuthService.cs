using System.Net.Http.Json;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using TishtryaCMS.Modules.Identity.Domain;
using TishtryaCMS.Modules.Identity.Infrastructure;
using TishtryaCMS.Modules.Identity.Options;

namespace TishtryaCMS.Modules.Identity.Application;

public sealed class AuthService(
    IdentityDbContext db,
    JwtTokenService jwt,
    PasswordHasher<User> hasher,
    IConfiguration config,
    OtpCodeService otpSvc,
    IEmailSender emailSender,
    IOptions<OtpOptions> otpOpts)
{
    private static readonly HttpClient TurnstileClient = new()
    {
        BaseAddress = new Uri("https://challenges.cloudflare.com/")
    };

    public async Task<(LoginResponse? Response, string? Error)> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return (null, "Email and password are required.");
        }

        var captchaError = await ValidateCaptchaAsync(request.CaptchaToken, cancellationToken);
        if (captchaError is not null)
        {
            return (null, captchaError);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return (null, "Invalid email or password.");
        }

        if (!Roles.IsKnown(user.Role))
        {
            return (null, "Access denied.");
        }

        var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return (null, "Invalid email or password.");
        }

        var (token, expiresAt) = jwt.CreateToken(user);
        return (new LoginResponse(token, expiresAt, user.Email, user.DisplayName, user.Role, user.Id), null);
    }

    public async Task<(object? Response, string? Error, int StatusCode)> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return (null, "Email is required.", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return (null, "Password must be at least 6 characters.", StatusCodes.Status400BadRequest);
        }

        var email = request.Email.Trim().ToLowerInvariant();

        try
        {
            _ = new MailAddress(email);
        }
        catch (FormatException)
        {
            return (null, "Invalid email format.", StatusCodes.Status400BadRequest);
        }

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return (null, "A user with this email already exists.", StatusCodes.Status409Conflict);
        }

        var atIndex = email.IndexOf('@');
        var displayName = atIndex > 0 ? email[..atIndex] : email;

        var placeholder = User.Create(email, "pending", Roles.User, displayName);
        var hash = hasher.HashPassword(placeholder, request.Password);
        var user = User.Create(email, hash, Roles.User, displayName);
        user.SetActive(false);

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        var (code, otpError) = await otpSvc.CreateOtpAsync(user.Id, OtpPurposes.EmailVerification, cancellationToken);
        if (otpError is not null)
        {
            return (null, otpError, StatusCodes.Status500InternalServerError);
        }

        await emailSender.SendOtpEmailAsync(
            email,
            code!,
            OtpPurposes.EmailVerification,
            otpOpts.Value.ExpirationMinutes,
            cancellationToken);

        return (null, null, StatusCodes.Status202Accepted);
    }

    public async Task<(VerifyOtpResponse? Response, string? Error, int StatusCode)> VerifyEmailOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Code))
        {
            return (null, "Email and code are required.", StatusCodes.Status400BadRequest);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.IsActive)
        {
            return (null, "No pending verification.", StatusCodes.Status400BadRequest);
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose)
            ? OtpPurposes.EmailVerification
            : request.Purpose;

        var (valid, _) = await otpSvc.ValidateAndConsumeOtpAsync(
            user.Id,
            purpose,
            request.Code,
            cancellationToken);

        if (!valid)
        {
            return (null, "Invalid or expired verification code.", StatusCodes.Status400BadRequest);
        }

        user.SetActive(true);
        await db.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = jwt.CreateToken(user);
        return (new VerifyOtpResponse(
            true,
            token,
            expiresAt,
            user.Email,
            user.DisplayName,
            user.Role,
            user.Id), null, StatusCodes.Status200OK);
    }

    public async Task<(string? Error, int StatusCode)> ResendOtpAsync(
        ResendOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ("Email is required.", StatusCodes.Status400BadRequest);
        }

        var purpose = string.IsNullOrWhiteSpace(request.Purpose)
            ? OtpPurposes.EmailVerification
            : request.Purpose;

        if (!OtpPurposes.IsKnown(purpose))
        {
            return ("Invalid OTP purpose.", StatusCodes.Status400BadRequest);
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            return ("User not found.", StatusCodes.Status400BadRequest);
        }

        if (purpose == OtpPurposes.EmailVerification && user.IsActive)
        {
            return ("No pending verification.", StatusCodes.Status400BadRequest);
        }

        var (cooling, remainingSeconds) = await otpSvc.IsCooldownActiveAsync(
            user.Id,
            purpose,
            cancellationToken);

        if (cooling)
        {
            return ($"Please wait {remainingSeconds} seconds before requesting a new code.",
                StatusCodes.Status429TooManyRequests);
        }

        var unconsumed = await db.OtpCodes
            .Where(x => x.UserId == user.Id
                        && x.Purpose == purpose
                        && x.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var existingCode in unconsumed)
        {
            existingCode.MarkConsumed();
        }

        await db.SaveChangesAsync(cancellationToken);

        var (newCode, otpError) = await otpSvc.CreateOtpAsync(user.Id, purpose, cancellationToken);
        if (otpError is not null)
        {
            return (otpError, StatusCodes.Status500InternalServerError);
        }

        await emailSender.SendOtpEmailAsync(
            email,
            newCode!,
            purpose,
            otpOpts.Value.ExpirationMinutes,
            cancellationToken);

        return (null, StatusCodes.Status202Accepted);
    }

    public async Task<(string? Error, int StatusCode)> RequestLoginOtpAsync(
        RequestLoginOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return (null, StatusCodes.Status202Accepted);
        }

        var (cooling, _) = await otpSvc.IsCooldownActiveAsync(
            user.Id,
            OtpPurposes.Login,
            cancellationToken);

        if (cooling)
        {
            return (null, StatusCodes.Status202Accepted);
        }

        var (plainCode, _) = await otpSvc.CreateOtpAsync(user.Id, OtpPurposes.Login, cancellationToken);
        if (plainCode is not null)
        {
            await emailSender.SendOtpEmailAsync(
                user.Email,
                plainCode,
                OtpPurposes.Login,
                otpOpts.Value.ExpirationMinutes,
                cancellationToken);
        }

        return (null, StatusCodes.Status202Accepted);
    }

    public async Task<(LoginResponse? Response, string? Error)> LoginOtpAsync(
        LoginOtpRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return (null, "Invalid email or code.");
        }

        var (valid, _) = await otpSvc.ValidateAndConsumeOtpAsync(
            user.Id,
            OtpPurposes.Login,
            request.Code,
            cancellationToken);

        if (!valid)
        {
            return (null, "Invalid email or code.");
        }

        var (token, expiresAt) = jwt.CreateToken(user);
        return (new LoginResponse(token, expiresAt, user.Email, user.DisplayName, user.Role, user.Id), null);
    }

    private async Task<string?> ValidateCaptchaAsync(
        string? captchaToken,
        CancellationToken cancellationToken)
    {
        var secretKey = config["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(captchaToken))
        {
            return "Captcha challenge is required.";
        }

        try
        {
            var payload = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["secret"] = secretKey,
                ["response"] = captchaToken
            });

            using var response = await TurnstileClient.PostAsync(
                "turnstile/v0/siteverify",
                payload,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken);
            if (result is null || !result.Success)
            {
                return "Captcha verification failed. Please try again.";
            }

            return null;
        }
        catch
        {
            return "Captcha verification unavailable. Please try again shortly.";
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Local
    private sealed class TurnstileResponse
    {
        public bool Success { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string? ChallengeTs { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string? Hostname { get; set; }

        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public string[]? ErrorCodes { get; set; }
    }
}
