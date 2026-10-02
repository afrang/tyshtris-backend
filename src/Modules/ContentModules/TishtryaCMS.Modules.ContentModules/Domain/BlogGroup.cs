using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class BlogGroup : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }
    public Guid? ParentId { get; private set; }

    public BlogGroup? Parent { get; private set; }
    public ICollection<BlogGroup> Children { get; private set; } = new List<BlogGroup>();
    public ICollection<BlogGroupTranslation> Translations { get; private set; } = new List<BlogGroupTranslation>();

    private BlogGroup()
    {
    }

    public static BlogGroup Create(
        string title,
        string slug,
        string? keyword,
        string? description,
        Guid? parentId)
    {
        return new BlogGroup
        {
            // Guid.Empty lets SQL Server NEWSEQUENTIALID() generate the primary key.
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Keyword = NormalizeOptional(keyword),
            Description = NormalizeOptional(description),
            ParentId = parentId
        };
    }

    public void Update(
        string title,
        string slug,
        string? keyword,
        string? description,
        Guid? parentId)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
        ParentId = parentId;
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
