using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Forms.Domain;

public sealed class FormFieldTranslation : Entity
{
    public Guid FieldId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? Placeholder { get; private set; }
    public string? HelpText { get; private set; }

    public FormField? Field { get; private set; }

    private FormFieldTranslation()
    {
    }

    public static FormFieldTranslation Create(
        Guid fieldId,
        string languagePrefix,
        string label,
        string? placeholder,
        string? helpText)
    {
        return new FormFieldTranslation
        {
            Id = Guid.Empty,
            FieldId = fieldId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Label = NormalizeRequired(label, nameof(label)),
            Placeholder = NormalizeOptional(placeholder),
            HelpText = NormalizeOptional(helpText)
        };
    }

    public void Update(string label, string? placeholder, string? helpText)
    {
        Label = NormalizeRequired(label, nameof(label));
        Placeholder = NormalizeOptional(placeholder);
        HelpText = NormalizeOptional(helpText);
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

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
