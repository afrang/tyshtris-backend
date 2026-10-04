namespace TishtryaCMS.SharedKernel.Turnstile;

public interface ITurnstileValidator
{
    /// <summary>
    /// Validates a Cloudflare Turnstile token.
    /// Returns null when valid (or when Turnstile is not configured).
    /// Returns an error message when validation fails.
    /// </summary>
    Task<string?> ValidateAsync(string? captchaToken, CancellationToken cancellationToken = default);
}
