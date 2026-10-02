using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TishtryaCMS.Modules.Identity.Options;

namespace TishtryaCMS.Modules.Identity.Application;

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendOtpEmailAsync(
        string toEmail,
        string otpCode,
        string purpose,
        int expirationMinutes,
        CancellationToken cancellationToken = default)
    {
        var subject = $"Your OTP for {purpose}";
        var body = $"Your OTP code is: {otpCode}\nThis code will expire in {expirationMinutes} minutes.";

        using var message = new MailMessage();
        message.To.Add(toEmail);
        message.Subject = subject;
        message.Body = body;

        if (!string.IsNullOrWhiteSpace(_options.DefaultFromAddress))
        {
            message.From = new MailAddress(_options.DefaultFromAddress);
        }
        else if (!string.IsNullOrWhiteSpace(_options.Username) && _options.Username.Contains('@'))
        {
            message.From = new MailAddress(_options.Username);
        }

        using var client = new SmtpClient(_options.Host, _options.Port);
        client.EnableSsl = _options.EnableSsl;

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
