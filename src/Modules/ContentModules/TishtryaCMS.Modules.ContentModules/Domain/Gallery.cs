using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class Gallery : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<GalleryTranslation> Translations { get; private set; } = new List<GalleryTranslation>();

    private Gallery()
    {
    }

    public static Gallery Create(
        string title,
        string slug,
        string? keyword,
        string? description,
        Guid? createdBy)
    {
        var now = DateTime.UtcNow;
        return new Gallery
        {
            // Guid.Empty lets SQL Server NEWSEQUENTIALID() generate the primary key.
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Keyword = NormalizeOptional(keyword),
            Description = NormalizeOptional(description),
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string title,
        string slug,
        string? keyword,
        string? description)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
        UpdatedAt = DateTime.UtcNow;
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
