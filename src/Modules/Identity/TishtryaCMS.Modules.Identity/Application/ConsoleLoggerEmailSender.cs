using Microsoft.Extensions.Logging;

namespace TishtryaCMS.Modules.Identity.Application;

public sealed class ConsoleLoggerEmailSender(ILogger<ConsoleLoggerEmailSender> logger) : IEmailSender
{
    public Task SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        string purpose,
        int expirationMinutes,
        CancellationToken cancellationToken = default)
    {
        var message = $"[OTP Email] To: {toEmail} | Purpose: {purpose} | Code: {otpCode} | Expires in: {expirationMinutes} min";

        Console.WriteLine(message);
        logger.LogInformation("{OtpEmailMessage}", message);

        return Task.CompletedTask;
    }
}
