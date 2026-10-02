using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Forms.Domain;

public sealed class FormSubmission : Entity
{
    public Guid FormId { get; private set; }
    public string PayloadJson { get; private set; } = "{}";
    public string? LanguagePrefix { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public FormDefinition? Form { get; private set; }

    private FormSubmission()
    {
    }

    public static FormSubmission Create(
        Guid formId,
        string payloadJson,
        string? languagePrefix,
        string? ipAddress,
        string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new ArgumentException("payloadJson is required.", nameof(payloadJson));
        }

        return new FormSubmission
        {
            Id = Guid.Empty,
            FormId = formId,
            PayloadJson = payloadJson.Trim(),
            LanguagePrefix = string.IsNullOrWhiteSpace(languagePrefix)
                ? null
                : languagePrefix.Trim().ToLowerInvariant().Replace('_', '-'),
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }
}
