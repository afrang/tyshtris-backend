using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Forms.Domain;

public sealed class FormTranslation : Entity
{
    public Guid FormId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string SubmitButtonText { get; private set; } = "Submit";
    public string? SuccessMessage { get; private set; }

    public FormDefinition? Form { get; private set; }

    private FormTranslation()
    {
    }

    public static FormTranslation Create(
        Guid formId,
        string languagePrefix,
        string title,
        string slug,
        string? description,
        string? submitButtonText,
        string? successMessage)
    {
        return new FormTranslation
        {
            Id = Guid.Empty,
            FormId = formId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Description = NormalizeOptional(description),
            SubmitButtonText = string.IsNullOrWhiteSpace(submitButtonText)
                ? "Submit"
                : submitButtonText.Trim(),
            SuccessMessage = NormalizeOptional(successMessage)
        };
    }

    public void Update(
        string title,
        string slug,
        string? description,
        string? submitButtonText,
        string? successMessage)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Description = NormalizeOptional(description);
        SubmitButtonText = string.IsNullOrWhiteSpace(submitButtonText)
            ? "Submit"
            : submitButtonText.Trim();
        SuccessMessage = NormalizeOptional(successMessage);
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("languagePrefix is required.", nameof(prefix));
        }

        return prefix.Trim().ToLowerInvariant().Replace('_', '-');
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string NormalizeSlug(string slug) =>
        NormalizeRequired(slug, nameof(slug)).ToLowerInvariant().Replace(' ', '-');

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
