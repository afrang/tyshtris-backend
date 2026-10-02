using Microsoft.Extensions.Options;

namespace TishtryaCMS.Modules.Identity.Options;

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int ExpirationMinutes { get; set; } = 10;
    public int CodeLength { get; set; } = 6;
    public int ResendCooldownSeconds { get; set; } = 60;
}

public sealed class OtpOptionsValidator : IValidateOptions<OtpOptions>
{
    public ValidateOptionsResult Validate(string? name, OtpOptions options)
    {
        if (options.ExpirationMinutes <= 0)
        {
            return ValidateOptionsResult.Fail("Otp:ExpirationMinutes must be greater than 0.");
        }

        if (options.CodeLength < 4 || options.CodeLength > 10)
        {
            return ValidateOptionsResult.Fail("Otp:CodeLength must be between 4 and 10 (inclusive).");
        }

        return ValidateOptionsResult.Success;
    }
}
