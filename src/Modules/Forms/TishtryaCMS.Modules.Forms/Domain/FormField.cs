using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Forms.Domain;

public sealed class FormField : Entity
{
    public Guid FormId { get; private set; }
    public string FieldKey { get; private set; } = string.Empty;
    public string FieldType { get; private set; } = FormFieldTypes.Text;
    public string Label { get; private set; } = string.Empty;
    public string? Placeholder { get; private set; }
    public string? HelpText { get; private set; }
    public string? OptionsJson { get; private set; }
    public bool IsRequired { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public FormDefinition? Form { get; private set; }
    public ICollection<FormFieldTranslation> Translations { get; private set; } = new List<FormFieldTranslation>();

    private FormField()
    {
    }

    public static FormField Create(
        Guid formId,
        string fieldKey,
        string fieldType,
        string label,
        string? placeholder,
        string? helpText,
        string? optionsJson,
        bool isRequired,
        int sortOrder)
    {
        var now = DateTime.UtcNow;
        return new FormField
        {
            Id = Guid.Empty,
            FormId = formId,
            FieldKey = NormalizeKey(fieldKey),
            FieldType = NormalizeType(fieldType),
            Label = NormalizeRequired(label, nameof(label)),
            Placeholder = NormalizeOptional(placeholder),
            HelpText = NormalizeOptional(helpText),
            OptionsJson = NormalizeOptions(optionsJson, fieldType),
            IsRequired = isRequired,
            SortOrder = sortOrder,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string fieldKey,
        string fieldType,
        string label,
        string? placeholder,
        string? helpText,
        string? optionsJson,
        bool isRequired,
        int sortOrder)
    {
        FieldKey = NormalizeKey(fieldKey);
        FieldType = NormalizeType(fieldType);
        Label = NormalizeRequired(label, nameof(label));
        Placeholder = NormalizeOptional(placeholder);
        HelpText = NormalizeOptional(helpText);
        OptionsJson = NormalizeOptions(optionsJson, fieldType);
        IsRequired = isRequired;
        SortOrder = sortOrder;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeType(string fieldType)
    {
        if (string.IsNullOrWhiteSpace(fieldType) || !FormFieldTypes.IsValid(fieldType))
        {
            throw new ArgumentException(
                "fieldType must be one of: text, email, tel, number, textarea, select, radio, checkbox, date, url.",
                nameof(fieldType));
        }

        return fieldType.Trim().ToLowerInvariant();
    }

    private static string NormalizeKey(string fieldKey)
    {
        var value = NormalizeRequired(fieldKey, nameof(fieldKey))
            .ToLowerInvariant()
            .Replace(' ', '_');

        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-z][a-z0-9_]*$"))
        {
            throw new ArgumentException(
                "fieldKey must start with a letter and contain only lowercase letters, numbers, and underscores.",
                nameof(fieldKey));
        }

        return value;
    }

    private static string? NormalizeOptions(string? optionsJson, string fieldType)
    {
        if (!FormFieldTypes.NeedsOptions(fieldType))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            throw new ArgumentException("optionsJson is required for select and radio fields.", nameof(optionsJson));
        }

        return optionsJson.Trim();
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
