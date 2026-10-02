using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class Tag : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public ICollection<BlogPostTag> PostTags { get; private set; } = new List<BlogPostTag>();
    public ICollection<TagTranslation> Translations { get; private set; } = new List<TagTranslation>();

    private Tag()
    {
    }

    public static Tag Create(string title, string slug, string? description)
    {
        return new Tag
        {
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Description = NormalizeOptional(description)
        };
    }

    public void Update(string title, string slug, string? description)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Description = NormalizeOptional(description);
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = NormalizeRequired(slug, nameof(slug)).ToLowerInvariant();
        return normalized.Replace(' ', '-');
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
