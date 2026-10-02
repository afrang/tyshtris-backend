using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Identity.Domain;

public sealed class OtpCode : Entity
{
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

    private OtpCode()
    {
    }

    public static OtpCode Create(Guid userId, string codeHash, string purpose, DateTime expiresAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("Code hash is required.", nameof(codeHash));
        }

        if (!OtpPurposes.IsKnown(purpose))
        {
            throw new ArgumentException("Invalid OTP purpose.", nameof(purpose));
        }

        if (expiresAt <= DateTime.UtcNow)
        {
            throw new ArgumentException("Expiration time must be in the future.", nameof(expiresAt));
        }

        return new OtpCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CodeHash = codeHash,
            Purpose = purpose,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresAt,
            ConsumedAtUtc = null
        };
    }

    public void MarkConsumed() => ConsumedAtUtc = DateTime.UtcNow;
}
