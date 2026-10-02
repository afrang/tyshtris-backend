using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Forms.Domain;

public sealed class FormDefinition : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string SubmitButtonText { get; private set; } = "Submit";
    public string? SuccessMessage { get; private set; }
    public bool IsPublished { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<FormTranslation> Translations { get; private set; } = new List<FormTranslation>();
    public ICollection<FormField> Fields { get; private set; } = new List<FormField>();
    public ICollection<FormSubmission> Submissions { get; private set; } = new List<FormSubmission>();

    private FormDefinition()
    {
    }

    public static FormDefinition Create(
        string title,
        string slug,
        string? description,
        string? submitButtonText,
        string? successMessage,
        bool isPublished,
        Guid? createdBy)
    {
        var now = DateTime.UtcNow;
        return new FormDefinition
        {
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Description = NormalizeOptional(description),
            SubmitButtonText = string.IsNullOrWhiteSpace(submitButtonText)
                ? "Submit"
                : submitButtonText.Trim(),
            SuccessMessage = NormalizeOptional(successMessage),
            IsPublished = isPublished,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string title,
        string slug,
        string? description,
        string? submitButtonText,
        string? successMessage,
        bool isPublished)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Description = NormalizeOptional(description);
        SubmitButtonText = string.IsNullOrWhiteSpace(submitButtonText)
            ? "Submit"
            : submitButtonText.Trim();
        SuccessMessage = NormalizeOptional(successMessage);
        IsPublished = isPublished;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Touch() => UpdatedAt = DateTime.UtcNow;

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
