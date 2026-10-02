namespace TishtryaCMS.Modules.Identity.Application;

public interface IEmailSender
{
    Task SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        string purpose,
        int expirationMinutes,
        CancellationToken cancellationToken = default);
}
