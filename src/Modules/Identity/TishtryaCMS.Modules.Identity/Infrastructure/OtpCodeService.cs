using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TishtryaCMS.Modules.Identity.Domain;
using TishtryaCMS.Modules.Identity.Options;

namespace TishtryaCMS.Modules.Identity.Infrastructure;

public sealed class OtpCodeService(
    IdentityDbContext db,
    IOptions<OtpOptions> otpOptions)
{
    private readonly OtpOptions _options = otpOptions.Value;

    public static string GenerateNumericCode(int length)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than 0.");
        }

        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(chars);
    }

    public static string HashCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }

        return sb.ToString();
    }

    public async Task<(string? PlainCode, string? Error)> CreateOtpAsync(
        Guid userId,
        string purpose,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return (null, "User id is required.");
        }

        if (!OtpPurposes.IsKnown(purpose))
        {
            return (null, "Invalid OTP purpose.");
        }

        var previousCodes = await db.OtpCodes
            .Where(x => x.UserId == userId
                        && x.Purpose == purpose
                        && x.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var code in previousCodes)
        {
            code.MarkConsumed();
        }

        var plainCode = GenerateNumericCode(_options.CodeLength);
        var codeHash = HashCode(plainCode);
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);

        try
        {
            var otp = OtpCode.Create(userId, codeHash, purpose, expiresAt);
            db.OtpCodes.Add(otp);
            await db.SaveChangesAsync(cancellationToken);
            return (plainCode, null);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message);
        }
    }

    public async Task<(bool Valid, string? Error)> ValidateAndConsumeOtpAsync(
        Guid userId,
        string purpose,
        string enteredCode,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return (false, "User id is required.");
        }

        if (!OtpPurposes.IsKnown(purpose))
        {
            return (false, "Invalid OTP purpose.");
        }

        if (string.IsNullOrWhiteSpace(enteredCode))
        {
            return (false, "Entered code is required.");
        }

        var enteredHash = HashCode(enteredCode);
        var now = DateTime.UtcNow;

        var match = await db.OtpCodes
            .FirstOrDefaultAsync(x => x.UserId == userId
                                      && x.Purpose == purpose
                                      && x.CodeHash == enteredHash
                                      && x.ConsumedAtUtc == null
                                      && x.ExpiresAtUtc > now,
                cancellationToken);

        if (match is null)
        {
            return (false, "Invalid or expired code.");
        }

        match.MarkConsumed();
        await db.SaveChangesAsync(cancellationToken);
        return (true, null);
    }

    public async Task<(bool Cooling, int RemainingSeconds)> IsCooldownActiveAsync(
        Guid userId,
        string purpose,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || !OtpPurposes.IsKnown(purpose))
        {
            return (false, 0);
        }

        var mostRecent = await db.OtpCodes
            .Where(x => x.UserId == userId && x.Purpose == purpose)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (mostRecent == default)
        {
            return (false, 0);
        }

        var cooldownEnd = mostRecent.AddSeconds(_options.ResendCooldownSeconds);
        var now = DateTime.UtcNow;
        if (now >= cooldownEnd)
        {
            return (false, 0);
        }

        var remaining = (int)Math.Ceiling((cooldownEnd - now).TotalSeconds);
        return (true, remaining);
    }
}
